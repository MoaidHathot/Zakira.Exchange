using Zakira.Exchange.Core.Configuration;
using Zakira.Exchange.Core.Models;
using Zakira.Exchange.Core.Search;
using Zakira.Exchange.Core.Storage;

namespace Zakira.Exchange.Core.Services;

/// <summary>
/// Main service that orchestrates memory CRUD operations and search.
/// Handles embedding generation on write, search orchestration, and access mode enforcement.
/// The ONNX model is loaded lazily - only when an operation that requires embeddings is invoked
/// (create, edit, search). Operations like list, get, delete, and categories work without the model.
/// </summary>
public sealed class MemoryService : IDisposable
{
    private readonly MemoryStore _store;
    private readonly ZakiraOptions _options;
    private readonly object _embeddingLock = new();
    private EmbeddingService? _embeddingService;
    private HybridSearchEngine? _searchEngine;

    public MemoryService(ZakiraOptions options)
    {
        _options = options;

        // Ensure database directory exists
        var dbDir = Path.GetDirectoryName(Path.GetFullPath(options.DatabasePath));
        if (!string.IsNullOrEmpty(dbDir) && !Directory.Exists(dbDir))
        {
            Directory.CreateDirectory(dbDir);
        }

        _store = new MemoryStore(options.DatabasePath);
    }

    /// <summary>
    /// Ensures the embedding service and search engine are initialized.
    /// Called lazily on first create, edit, or search operation.
    /// Thread-safe via double-checked locking.
    /// </summary>
    private void EnsureEmbeddingInitialized()
    {
        if (_embeddingService is not null)
        {
            return;
        }

        lock (_embeddingLock)
        {
            if (_embeddingService is not null)
            {
                return;
            }

            var (modelPath, vocabPath) = ResolveModelPaths(_options.ModelPath);
            _embeddingService = new EmbeddingService(modelPath, vocabPath);
            _searchEngine = new HybridSearchEngine(_store, _embeddingService);
        }
    }

    /// <summary>
    /// Creates a new memory entry. Generates embedding and sets timestamps.
    /// </summary>
    public MemoryEntry Create(string category, string key, string data, string? author = null,
        string? reason = null, List<string>? tags = null, Dictionary<string, string>? custom = null)
    {
        EnsureEmbeddingInitialized();

        var now = DateTimeOffset.UtcNow;

        // Apply const-category if configured
        category = ResolveCategory(category);

        var entry = new MemoryEntry
        {
            Category = category,
            Key = key,
            Data = data,
            Metadata = new MemoryMetadata
            {
                Author = author,
                Reason = reason,
                Tags = tags ?? [],
                Custom = custom ?? [],
                CreatedAt = now,
                LastModifiedAt = now,
            }
        };

        // Generate embedding from the combined text of data + key + tags + reason
        var embeddingText = BuildEmbeddingText(entry);
        var embedding = _embeddingService!.Embed(embeddingText);

        _store.Create(entry, embedding);
        return entry;
    }

    /// <summary>
    /// Edits an existing memory entry. Only updates provided (non-null) fields.
    /// Re-generates embedding and updates lastModifiedAt. Backward-compatible
    /// wrapper around <see cref="EditWithConcurrency"/>; last-write-wins.
    /// </summary>
    public MemoryEntry? Edit(string category, string key, string? data = null,
        string? author = null, string? reason = null, List<string>? tags = null,
        Dictionary<string, string>? custom = null)
    {
        var result = EditWithConcurrency(category, key, data, author, reason, tags, custom, expectedLastModifiedAt: null);
        return result.Outcome == EditOutcome.Updated ? result.Entry : null;
    }

    /// <summary>
    /// Edits an existing memory entry with optional optimistic-concurrency control.
    /// When <paramref name="expectedLastModifiedAt"/> is provided, the edit only
    /// applies if the entry's current <c>LastModifiedAt</c> matches; otherwise
    /// returns <see cref="EditOutcome.Conflict"/> with the current value so the
    /// caller can re-fetch and retry. When null, last-write-wins.
    /// Only updates provided (non-null) fields.
    /// </summary>
    public EditResult EditWithConcurrency(string category, string key, string? data,
        string? author, string? reason, List<string>? tags,
        Dictionary<string, string>? custom, DateTimeOffset? expectedLastModifiedAt)
    {
        category = ResolveCategory(category);

        var existing = _store.Get(category, key);
        if (existing is null)
        {
            return new EditResult { Outcome = EditOutcome.NotFound };
        }

        // Early conflict check: avoid the embedding pass when we already know the
        // expected value is stale. The SQL UPDATE's WHERE clause is still the
        // authoritative check against a racing writer.
        if (expectedLastModifiedAt is not null
            && existing.Metadata.LastModifiedAt != expectedLastModifiedAt.Value)
        {
            return new EditResult
            {
                Outcome = EditOutcome.Conflict,
                CurrentLastModifiedAt = existing.Metadata.LastModifiedAt,
            };
        }

        EnsureEmbeddingInitialized();

        // Update only provided fields
        if (data is not null)
        {
            existing.Data = data;
        }
        if (author is not null)
        {
            existing.Metadata.Author = author;
        }
        if (reason is not null)
        {
            existing.Metadata.Reason = reason;
        }
        if (tags is not null)
        {
            existing.Metadata.Tags = tags;
        }
        if (custom is not null)
        {
            existing.Metadata.Custom = custom;
        }

        existing.Metadata.LastModifiedAt = DateTimeOffset.UtcNow;

        var embeddingText = BuildEmbeddingText(existing);
        var embedding = _embeddingService!.Embed(embeddingText);

        var outcome = _store.Update(existing, embedding, expectedLastModifiedAt);
        return outcome switch
        {
            UpdateOutcome.Updated => new EditResult { Outcome = EditOutcome.Updated, Entry = existing },
            UpdateOutcome.NotFound => new EditResult { Outcome = EditOutcome.NotFound },
            UpdateOutcome.Conflict => new EditResult
            {
                Outcome = EditOutcome.Conflict,
                CurrentLastModifiedAt = _store.Get(category, key)?.Metadata.LastModifiedAt,
            },
            _ => new EditResult { Outcome = EditOutcome.NotFound },
        };
    }

    /// <summary>
    /// Deletes a memory entry by (category, key).
    /// </summary>
    public bool Delete(string category, string key)
    {
        category = ResolveCategory(category);
        return _store.Delete(category, key);
    }

    /// <summary>
    /// Gets a single memory entry by (category, key).
    /// </summary>
    public MemoryEntry? Get(string category, string key)
    {
        category = ResolveCategory(category);
        return _store.Get(category, key);
    }

    /// <summary>
    /// Lists memory entries with filtering.
    /// </summary>
    public List<MemoryEntry> List(ListFilter filter)
    {
        // Apply const-category if configured
        if (_options.HasConstCategory)
        {
            filter.Category = _options.ConstCategory;
        }

        return _store.List(filter);
    }

    /// <summary>
    /// Performs hybrid semantic + keyword search.
    /// </summary>
    public List<SearchResult> Search(SearchFilter filter)
    {
        EnsureEmbeddingInitialized();

        // Apply const-category if configured
        if (_options.HasConstCategory)
        {
            filter.Category = _options.ConstCategory;
        }

        return _searchEngine!.Search(filter);
    }

    /// <summary>
    /// Gets the list of distinct categories.
    /// </summary>
    public List<string> GetCategories()
    {
        return _store.GetCategories();
    }

    /// <summary>
    /// Gets the total count of entries.
    /// </summary>
    public long GetCount(string? category = null)
    {
        if (_options.HasConstCategory)
        {
            category = _options.ConstCategory;
        }
        return _store.GetCount(category);
    }

    /// <summary>
    /// The configured options.
    /// </summary>
    public ZakiraOptions Options => _options;

    /// <summary>
    /// Resolves the category, applying const-category if configured.
    /// </summary>
    private string ResolveCategory(string category)
    {
        return _options.HasConstCategory ? _options.ConstCategory! : category;
    }

    /// <summary>
    /// Builds the text used for embedding generation from a memory entry.
    /// Combines key, data, tags, and reason for broad semantic coverage.
    /// </summary>
    private static string BuildEmbeddingText(MemoryEntry entry)
    {
        var parts = new List<string> { entry.Key, entry.Data };

        if (entry.Metadata.Tags.Count > 0)
        {
            parts.Add(string.Join(" ", entry.Metadata.Tags));
        }

        if (!string.IsNullOrWhiteSpace(entry.Metadata.Reason))
        {
            parts.Add(entry.Metadata.Reason);
        }

        return string.Join(" | ", parts);
    }

    /// <summary>
    /// Resolves the ONNX model and vocabulary file paths.
    /// Searches in order: custom path, alongside the executable, bundled in the package.
    /// </summary>
    private static (string ModelPath, string VocabPath) ResolveModelPaths(string? customModelPath)
    {
        if (customModelPath is not null)
        {
            var customVocab = Path.Combine(Path.GetDirectoryName(customModelPath) ?? ".", "vocab.txt");
            if (File.Exists(customModelPath) && File.Exists(customVocab))
            {
                return (customModelPath, customVocab);
            }
            throw new FileNotFoundException($"Custom model or vocab not found. Model: {customModelPath}, Vocab: {customVocab}");
        }

        // Search relative to the executing assembly
        var assemblyDir = Path.GetDirectoryName(typeof(MemoryService).Assembly.Location) ?? ".";
        var searchPaths = new[]
        {
            Path.Combine(assemblyDir, "Models"),
            Path.Combine(assemblyDir, "..", "Models"),
            Path.Combine(assemblyDir),
            Path.Combine(AppContext.BaseDirectory, "Models"),
        };

        foreach (var dir in searchPaths)
        {
            var modelPath = Path.Combine(dir, "all-MiniLM-L6-v2.onnx");
            var vocabPath = Path.Combine(dir, "vocab.txt");
            if (File.Exists(modelPath) && File.Exists(vocabPath))
            {
                return (modelPath, vocabPath);
            }
        }

        throw new FileNotFoundException(
            "ONNX model files not found. Please download them using the download-model script, " +
            "or specify a custom model path with --model-path. " +
            "Expected files: all-MiniLM-L6-v2.onnx and vocab.txt");
    }

    public void Dispose()
    {
        _embeddingService?.Dispose();
        _store.Dispose();
    }
}
