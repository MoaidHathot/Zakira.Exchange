using Microsoft.Data.Sqlite;
using Zakira.Exchange.Core.Models;
using Zakira.Exchange.Core.Storage;

namespace Zakira.Exchange.Tests.Storage;

public class MemoryStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly MemoryStore _store;

    public MemoryStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"zakira_test_{Guid.NewGuid():N}.db");
        _store = new MemoryStore(_dbPath);
    }

    public void Dispose()
    {
        _store.Dispose();
        SqliteConnection.ClearAllPools();
        TryDeleteFile(_dbPath);
        TryDeleteFile(_dbPath + "-wal");
        TryDeleteFile(_dbPath + "-shm");
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
    }

    private static MemoryEntry CreateEntry(string category = "test", string key = "key1",
        string data = "test data", string? author = null, string? reason = null,
        List<string>? tags = null, Dictionary<string, string>? custom = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new MemoryEntry
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
    }

    // --- Create Tests ---

    [Fact]
    public void Create_NewEntry_Succeeds()
    {
        var entry = CreateEntry();
        _store.Create(entry, null);

        var retrieved = _store.Get("test", "key1");
        Assert.NotNull(retrieved);
        Assert.Equal("test", retrieved.Category);
        Assert.Equal("key1", retrieved.Key);
        Assert.Equal("test data", retrieved.Data);
    }

    [Fact]
    public void Create_DuplicateKey_ThrowsInvalidOperationException()
    {
        var entry = CreateEntry();
        _store.Create(entry, null);

        var duplicate = CreateEntry();
        Assert.Throws<InvalidOperationException>(() => _store.Create(duplicate, null));
    }

    [Fact]
    public void Create_WithMetadata_StoresAllFields()
    {
        var entry = CreateEntry(
            author: "agent-1",
            reason: "testing purposes",
            tags: ["tag1", "tag2"],
            custom: new Dictionary<string, string> { ["env"] = "test" }
        );
        _store.Create(entry, null);

        var retrieved = _store.Get("test", "key1");
        Assert.NotNull(retrieved);
        Assert.Equal("agent-1", retrieved.Metadata.Author);
        Assert.Equal("testing purposes", retrieved.Metadata.Reason);
        Assert.Equal(2, retrieved.Metadata.Tags.Count);
        Assert.Contains("tag1", retrieved.Metadata.Tags);
        Assert.Contains("tag2", retrieved.Metadata.Tags);
        Assert.Single(retrieved.Metadata.Custom);
        Assert.Equal("test", retrieved.Metadata.Custom["env"]);
    }

    [Fact]
    public void Create_WithEmbedding_StoresAndRetrievesCorrectly()
    {
        var entry = CreateEntry();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        _store.Create(entry, embedding);

        var embeddings = _store.GetAllEmbeddings(null);
        Assert.Single(embeddings);
        Assert.Equal("test", embeddings[0].Category);
        Assert.Equal("key1", embeddings[0].Key);
        Assert.Equal(4, embeddings[0].Embedding.Length);
        Assert.Equal(0.1f, embeddings[0].Embedding[0], 0.0001f);
        Assert.Equal(0.4f, embeddings[0].Embedding[3], 0.0001f);
    }

    // --- Get Tests ---

    [Fact]
    public void Get_NonExistent_ReturnsNull()
    {
        var result = _store.Get("nonexistent", "nokey");
        Assert.Null(result);
    }

    [Fact]
    public void Get_PreservesTimestamps()
    {
        var now = DateTimeOffset.UtcNow;
        var entry = CreateEntry();
        entry.Metadata.CreatedAt = now;
        entry.Metadata.LastModifiedAt = now;
        _store.Create(entry, null);

        var retrieved = _store.Get("test", "key1");
        Assert.NotNull(retrieved);
        // Timestamps round-trip should be close (within a second due to serialization precision)
        Assert.True(Math.Abs((retrieved.Metadata.CreatedAt - now).TotalSeconds) < 1);
        Assert.True(Math.Abs((retrieved.Metadata.LastModifiedAt - now).TotalSeconds) < 1);
    }

    // --- Update Tests ---

    [Fact]
    public void Update_ExistingEntry_ReturnsTrue()
    {
        var entry = CreateEntry();
        _store.Create(entry, null);

        entry.Data = "updated data";
        entry.Metadata.LastModifiedAt = DateTimeOffset.UtcNow;
        var result = _store.Update(entry, null);

        Assert.True(result);
        var retrieved = _store.Get("test", "key1");
        Assert.NotNull(retrieved);
        Assert.Equal("updated data", retrieved.Data);
    }

    [Fact]
    public void Update_NonExistent_ReturnsFalse()
    {
        var entry = CreateEntry(category: "nonexistent", key: "nokey");
        var result = _store.Update(entry, null);
        Assert.False(result);
    }

    [Fact]
    public void Update_WithNewEmbedding_ReplacesOld()
    {
        var entry = CreateEntry();
        var oldEmbedding = new float[] { 1.0f, 2.0f };
        _store.Create(entry, oldEmbedding);

        var newEmbedding = new float[] { 3.0f, 4.0f };
        entry.Metadata.LastModifiedAt = DateTimeOffset.UtcNow;
        _store.Update(entry, newEmbedding);

        var embeddings = _store.GetAllEmbeddings(null);
        Assert.Single(embeddings);
        Assert.Equal(3.0f, embeddings[0].Embedding[0], 0.0001f);
        Assert.Equal(4.0f, embeddings[0].Embedding[1], 0.0001f);
    }

    // --- Delete Tests ---

    [Fact]
    public void Delete_ExistingEntry_ReturnsTrue()
    {
        _store.Create(CreateEntry(), null);
        var result = _store.Delete("test", "key1");
        Assert.True(result);
    }

    [Fact]
    public void Delete_ExistingEntry_RemovesFromStore()
    {
        _store.Create(CreateEntry(), null);
        _store.Delete("test", "key1");

        var retrieved = _store.Get("test", "key1");
        Assert.Null(retrieved);
    }

    [Fact]
    public void Delete_NonExistent_ReturnsFalse()
    {
        var result = _store.Delete("nonexistent", "nokey");
        Assert.False(result);
    }

    // --- List Tests ---

    [Fact]
    public void List_EmptyStore_ReturnsEmptyList()
    {
        var result = _store.List(new ListFilter());
        Assert.Empty(result);
    }

    [Fact]
    public void List_AllEntries_ReturnsAll()
    {
        _store.Create(CreateEntry(key: "key1"), null);
        _store.Create(CreateEntry(key: "key2"), null);
        _store.Create(CreateEntry(key: "key3"), null);

        var result = _store.List(new ListFilter());
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void List_FilterByCategory_ReturnsOnlyMatching()
    {
        _store.Create(CreateEntry(category: "cat-a", key: "key1"), null);
        _store.Create(CreateEntry(category: "cat-b", key: "key2"), null);
        _store.Create(CreateEntry(category: "cat-a", key: "key3"), null);

        var result = _store.List(new ListFilter { Category = "cat-a" });
        Assert.Equal(2, result.Count);
        Assert.All(result, e => Assert.Equal("cat-a", e.Category));
    }

    [Fact]
    public void List_FilterByAuthor_ReturnsOnlyMatching()
    {
        _store.Create(CreateEntry(key: "key1", author: "alice"), null);
        _store.Create(CreateEntry(key: "key2", author: "bob"), null);
        _store.Create(CreateEntry(key: "key3", author: "alice"), null);

        var result = _store.List(new ListFilter { Author = "alice" });
        Assert.Equal(2, result.Count);
        Assert.All(result, e => Assert.Equal("alice", e.Metadata.Author));
    }

    [Fact]
    public void List_FilterByTags_ReturnsMatchingAny()
    {
        _store.Create(CreateEntry(key: "key1", tags: ["important", "arch"]), null);
        _store.Create(CreateEntry(key: "key2", tags: ["minor"]), null);
        _store.Create(CreateEntry(key: "key3", tags: ["arch", "design"]), null);

        var result = _store.List(new ListFilter { Tags = ["important"] });
        Assert.Single(result);
        Assert.Equal("key1", result[0].Key);
    }

    [Fact]
    public void List_FilterByTop_LimitsResults()
    {
        for (var i = 0; i < 10; i++)
        {
            _store.Create(CreateEntry(key: $"key{i}"), null);
        }

        var result = _store.List(new ListFilter { Top = 3 });
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void List_FilterByBefore_ReturnsOnlyOlderEntries()
    {
        var early = DateTimeOffset.UtcNow.AddHours(-2);
        var late = DateTimeOffset.UtcNow;

        var entry1 = CreateEntry(key: "old");
        entry1.Metadata.LastModifiedAt = early;
        entry1.Metadata.CreatedAt = early;
        _store.Create(entry1, null);

        var entry2 = CreateEntry(key: "new");
        entry2.Metadata.LastModifiedAt = late;
        entry2.Metadata.CreatedAt = late;
        _store.Create(entry2, null);

        var result = _store.List(new ListFilter { Before = DateTimeOffset.UtcNow.AddHours(-1) });
        Assert.Single(result);
        Assert.Equal("old", result[0].Key);
    }

    [Fact]
    public void List_FilterByAfter_ReturnsOnlyNewerEntries()
    {
        var early = DateTimeOffset.UtcNow.AddHours(-2);
        var late = DateTimeOffset.UtcNow;

        var entry1 = CreateEntry(key: "old");
        entry1.Metadata.LastModifiedAt = early;
        entry1.Metadata.CreatedAt = early;
        _store.Create(entry1, null);

        var entry2 = CreateEntry(key: "new");
        entry2.Metadata.LastModifiedAt = late;
        entry2.Metadata.CreatedAt = late;
        _store.Create(entry2, null);

        var result = _store.List(new ListFilter { After = DateTimeOffset.UtcNow.AddHours(-1) });
        Assert.Single(result);
        Assert.Equal("new", result[0].Key);
    }

    // --- GetCategories Tests ---

    [Fact]
    public void GetCategories_EmptyStore_ReturnsEmpty()
    {
        var categories = _store.GetCategories();
        Assert.Empty(categories);
    }

    [Fact]
    public void GetCategories_ReturnsDistinctCategories()
    {
        _store.Create(CreateEntry(category: "alpha", key: "k1"), null);
        _store.Create(CreateEntry(category: "beta", key: "k2"), null);
        _store.Create(CreateEntry(category: "alpha", key: "k3"), null);

        var categories = _store.GetCategories();
        Assert.Equal(2, categories.Count);
        Assert.Contains("alpha", categories);
        Assert.Contains("beta", categories);
    }

    [Fact]
    public void GetCategories_ReturnsSorted()
    {
        _store.Create(CreateEntry(category: "zebra", key: "k1"), null);
        _store.Create(CreateEntry(category: "apple", key: "k2"), null);
        _store.Create(CreateEntry(category: "mango", key: "k3"), null);

        var categories = _store.GetCategories();
        Assert.Equal(["apple", "mango", "zebra"], categories);
    }

    // --- GetCount Tests ---

    [Fact]
    public void GetCount_EmptyStore_ReturnsZero()
    {
        Assert.Equal(0, _store.GetCount());
    }

    [Fact]
    public void GetCount_ReturnsTotal()
    {
        _store.Create(CreateEntry(key: "k1"), null);
        _store.Create(CreateEntry(key: "k2"), null);
        Assert.Equal(2, _store.GetCount());
    }

    [Fact]
    public void GetCount_WithCategory_ReturnsFiltered()
    {
        _store.Create(CreateEntry(category: "a", key: "k1"), null);
        _store.Create(CreateEntry(category: "b", key: "k2"), null);
        _store.Create(CreateEntry(category: "a", key: "k3"), null);

        Assert.Equal(2, _store.GetCount("a"));
        Assert.Equal(1, _store.GetCount("b"));
        Assert.Equal(0, _store.GetCount("nonexistent"));
    }

    // --- KeywordSearch Tests ---

    [Fact]
    public void KeywordSearch_MatchingTerm_ReturnsResults()
    {
        _store.Create(CreateEntry(key: "architecture", data: "microservices design pattern"), null);
        _store.Create(CreateEntry(key: "cooking", data: "chocolate cake recipe"), null);

        var results = _store.KeywordSearch("microservices", null);
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Key == "architecture");
    }

    [Fact]
    public void KeywordSearch_NoMatch_ReturnsEmpty()
    {
        _store.Create(CreateEntry(key: "test", data: "hello world"), null);

        var results = _store.KeywordSearch("xyznonexistent", null);
        Assert.Empty(results);
    }

    [Fact]
    public void KeywordSearch_WithCategoryFilter_ReturnsOnlyMatchingCategory()
    {
        _store.Create(CreateEntry(category: "tech", key: "k1", data: "microservices architecture"), null);
        _store.Create(CreateEntry(category: "food", key: "k2", data: "microservices cookbook"), null);

        var results = _store.KeywordSearch("microservices", "tech");
        Assert.Single(results);
        Assert.Equal("tech", results[0].Category);
    }

    [Fact]
    public void KeywordSearch_EmptyQuery_ReturnsEmpty()
    {
        _store.Create(CreateEntry(data: "some data"), null);

        var results = _store.KeywordSearch("", null);
        Assert.Empty(results);
    }

    [Fact]
    public void KeywordSearch_SpecialCharactersInQuery_HandlesGracefully()
    {
        _store.Create(CreateEntry(data: "test data with special chars"), null);

        // FTS5 special characters should be sanitized
        var results = _store.KeywordSearch("test*AND\"special", null);
        // Should not throw; may or may not return results depending on sanitization
        Assert.NotNull(results);
    }

    // --- GetAllEmbeddings Tests ---

    [Fact]
    public void GetAllEmbeddings_NoEmbeddings_ReturnsEmpty()
    {
        _store.Create(CreateEntry(), null);
        var embeddings = _store.GetAllEmbeddings(null);
        Assert.Empty(embeddings);
    }

    [Fact]
    public void GetAllEmbeddings_WithCategory_FiltersCorrectly()
    {
        _store.Create(CreateEntry(category: "a", key: "k1"), new float[] { 1.0f });
        _store.Create(CreateEntry(category: "b", key: "k2"), new float[] { 2.0f });

        var embeddings = _store.GetAllEmbeddings("a");
        Assert.Single(embeddings);
        Assert.Equal("a", embeddings[0].Category);
    }

    // --- FTS Sync Tests ---

    [Fact]
    public void FtsSync_DeletedEntry_NotReturnedInSearch()
    {
        _store.Create(CreateEntry(key: "deleteme", data: "unique searchable content"), null);
        _store.Delete("test", "deleteme");

        var results = _store.KeywordSearch("unique searchable content", null);
        Assert.Empty(results);
    }

    [Fact]
    public void FtsSync_UpdatedEntry_ReflectsNewContent()
    {
        var entry = CreateEntry(key: "updateme", data: "original content");
        _store.Create(entry, null);

        entry.Data = "completely different text";
        entry.Metadata.LastModifiedAt = DateTimeOffset.UtcNow;
        _store.Update(entry, null);

        var oldResults = _store.KeywordSearch("original", null);
        var newResults = _store.KeywordSearch("completely different", null);

        Assert.Empty(oldResults);
        Assert.NotEmpty(newResults);
    }
}
