using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;
using Zakira.Exchange.Core.Configuration;
using Zakira.Exchange.Core.Models;
using Zakira.Exchange.Core.Services;

namespace Zakira.Exchange.Cli.Tools;

/// <summary>
/// Builds MCP tools dynamically based on access mode and const-category configuration.
/// Tools are created programmatically using McpServerTool.Create so we can control
/// which tools are registered and which parameters they expose.
/// </summary>
public static class ToolBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    /// <summary>
    /// Builds the list of MCP tools based on the configured access mode and const-category.
    /// </summary>
    public static List<McpServerTool> BuildTools(MemoryService memoryService)
    {
        var options = memoryService.Options;
        var tools = new List<McpServerTool>();

        // Read tools are always available
        tools.Add(BuildGetMemoryTool(memoryService, options));
        tools.Add(BuildListMemoriesTool(memoryService, options));
        tools.Add(BuildSearchMemoriesTool(memoryService, options));

        // Write tools depend on access mode
        if (options.AccessMode.CanCreate())
        {
            tools.Add(BuildCreateMemoryTool(memoryService, options));
        }

        if (options.AccessMode.CanEdit())
        {
            tools.Add(BuildEditMemoryTool(memoryService, options));
        }

        if (options.AccessMode.CanDelete())
        {
            tools.Add(BuildDeleteMemoryTool(memoryService, options));
        }

        return tools;
    }

    private static McpServerTool BuildCreateMemoryTool(MemoryService service, ZakiraOptions options)
    {
        if (options.HasConstCategory)
        {
            return McpServerTool.Create(
                (
                    [Description("Unique key for this memory entry within the category")] string key,
                    [Description("The memory content (text data)")] string data,
                    [Description("Who or what is creating this memory (e.g. agent name, user name)")] string? author,
                    [Description("Why this memory is being created")] string? reason,
                    [Description("Comma-separated tags for categorization (e.g. 'important,architecture,decision')")] string? tags,
                    [Description("JSON object of custom key-value metadata (e.g. '{\"project\":\"myapp\"}')")] string? custom
                ) =>
                {
                    var tagList = ParseTags(tags);
                    var customDict = ParseCustom(custom);
                    var entry = service.Create(options.ConstCategory!, key, data, author, reason, tagList, customDict);
                    return FormatEntry(entry);
                },
                new McpServerToolCreateOptions
                {
                    Name = "create_memory",
                    Description = $"Creates a new memory entry in the '{options.ConstCategory}' category. " +
                                  "The key must be unique within this category. " +
                                  "Use this to save knowledge, decisions, context, or any information worth remembering."
                });
        }

        return McpServerTool.Create(
            (
                [Description("Category (table/namespace) to store the memory in. Use categories to group related memories (e.g. 'decisions', 'architecture', 'preferences')")] string category,
                [Description("Unique key for this memory entry within the category")] string key,
                [Description("The memory content (text data)")] string data,
                [Description("Who or what is creating this memory (e.g. agent name, user name)")] string? author,
                [Description("Why this memory is being created")] string? reason,
                [Description("Comma-separated tags for categorization (e.g. 'important,architecture,decision')")] string? tags,
                [Description("JSON object of custom key-value metadata (e.g. '{\"project\":\"myapp\"}')")] string? custom
            ) =>
            {
                var tagList = ParseTags(tags);
                var customDict = ParseCustom(custom);
                var entry = service.Create(category, key, data, author, reason, tagList, customDict);
                return FormatEntry(entry);
            },
            new McpServerToolCreateOptions
            {
                Name = "create_memory",
                Description = "Creates a new memory entry. The (category, key) pair must be unique. " +
                              "Use categories to organize memories by type (e.g. 'decisions', 'architecture', 'preferences'). " +
                              "Use this to save knowledge, decisions, context, or any information worth remembering."
            });
    }

    private static McpServerTool BuildEditMemoryTool(MemoryService service, ZakiraOptions options)
    {
        if (options.HasConstCategory)
        {
            return McpServerTool.Create(
                (
                    [Description("Key of the memory entry to edit")] string key,
                    [Description("Updated memory content (text). If omitted, existing content is kept.")] string? data,
                    [Description("Updated author. If omitted, existing value is kept.")] string? author,
                    [Description("Updated reason. If omitted, existing value is kept.")] string? reason,
                    [Description("Updated comma-separated tags. If omitted, existing tags are kept.")] string? tags,
                    [Description("Updated JSON object of custom key-value metadata. If omitted, existing custom data is kept.")] string? custom,
                    [Description("Optional optimistic-concurrency check. Pass the entry's current lastModifiedAt (ISO 8601, UTC) from a prior get_memory; the edit will only apply if the value still matches, otherwise the call returns a conflict with the current value so you can re-fetch and retry. Omit for last-write-wins.")] string? expectedLastModifiedAt
                ) =>
                {
                    var tagList = tags is not null ? ParseTags(tags) : null;
                    var customDict = custom is not null ? ParseCustom(custom) : null;
                    var expected = ParseTimestamp(expectedLastModifiedAt);
                    var result = service.EditWithConcurrency(options.ConstCategory!, key, data, author, reason, tagList, customDict, expected);
                    return FormatEditResult(result, options.ConstCategory!, key);
                },
                new McpServerToolCreateOptions
                {
                    Name = "edit_memory",
                    Description = $"Edits an existing memory entry in the '{options.ConstCategory}' category. " +
                                  "Only provided fields are updated; omitted fields keep their current values. " +
                                  "The lastModifiedAt timestamp is updated automatically. " +
                                  "Pass expectedLastModifiedAt to enable optimistic-concurrency control (see parameter docs)."
                });
        }

        return McpServerTool.Create(
            (
                [Description("Category of the memory entry to edit")] string category,
                [Description("Key of the memory entry to edit")] string key,
                [Description("Updated memory content (text). If omitted, existing content is kept.")] string? data,
                [Description("Updated author. If omitted, existing value is kept.")] string? author,
                [Description("Updated reason. If omitted, existing value is kept.")] string? reason,
                [Description("Updated comma-separated tags. If omitted, existing tags are kept.")] string? tags,
                [Description("Updated JSON object of custom key-value metadata. If omitted, existing custom data is kept.")] string? custom,
                [Description("Optional optimistic-concurrency check. Pass the entry's current lastModifiedAt (ISO 8601, UTC) from a prior get_memory; the edit will only apply if the value still matches, otherwise the call returns a conflict with the current value so you can re-fetch and retry. Omit for last-write-wins.")] string? expectedLastModifiedAt
            ) =>
            {
                var tagList = tags is not null ? ParseTags(tags) : null;
                var customDict = custom is not null ? ParseCustom(custom) : null;
                var expected = ParseTimestamp(expectedLastModifiedAt);
                var result = service.EditWithConcurrency(category, key, data, author, reason, tagList, customDict, expected);
                return FormatEditResult(result, category, key);
            },
            new McpServerToolCreateOptions
            {
                Name = "edit_memory",
                Description = "Edits an existing memory entry. Only provided fields are updated; omitted fields keep their current values. " +
                              "The lastModifiedAt timestamp is updated automatically. " +
                              "Pass expectedLastModifiedAt to enable optimistic-concurrency control (see parameter docs)."
            });
    }

    private static McpServerTool BuildDeleteMemoryTool(MemoryService service, ZakiraOptions options)
    {
        if (options.HasConstCategory)
        {
            return McpServerTool.Create(
                (
                    [Description("Key of the memory entry to delete")] string key
                ) =>
                {
                    var deleted = service.Delete(options.ConstCategory!, key);
                    return deleted
                        ? $"Memory entry with key '{key}' deleted from category '{options.ConstCategory}'."
                        : $"Memory entry with key '{key}' not found in category '{options.ConstCategory}'.";
                },
                new McpServerToolCreateOptions
                {
                    Name = "delete_memory",
                    Description = $"Deletes a memory entry from the '{options.ConstCategory}' category. This action is permanent."
                });
        }

        return McpServerTool.Create(
            (
                [Description("Category of the memory entry to delete")] string category,
                [Description("Key of the memory entry to delete")] string key
            ) =>
            {
                var deleted = service.Delete(category, key);
                return deleted
                    ? $"Memory entry with category '{category}' and key '{key}' deleted."
                    : $"Memory entry with category '{category}' and key '{key}' not found.";
            },
            new McpServerToolCreateOptions
            {
                Name = "delete_memory",
                Description = "Deletes a memory entry by its category and key. This action is permanent."
            });
    }

    private static McpServerTool BuildGetMemoryTool(MemoryService service, ZakiraOptions options)
    {
        if (options.HasConstCategory)
        {
            return McpServerTool.Create(
                (
                    [Description("Key of the memory entry to retrieve")] string key
                ) =>
                {
                    var entry = service.Get(options.ConstCategory!, key);
                    return entry is not null
                        ? FormatEntry(entry)
                        : $"Memory entry with key '{key}' not found in category '{options.ConstCategory}'.";
                },
                new McpServerToolCreateOptions
                {
                    Name = "get_memory",
                    Description = $"Retrieves a specific memory entry by its key from the '{options.ConstCategory}' category."
                });
        }

        return McpServerTool.Create(
            (
                [Description("Category of the memory entry")] string category,
                [Description("Key of the memory entry to retrieve")] string key
            ) =>
            {
                var entry = service.Get(category, key);
                return entry is not null
                    ? FormatEntry(entry)
                    : $"Memory entry with category '{category}' and key '{key}' not found.";
            },
            new McpServerToolCreateOptions
            {
                Name = "get_memory",
                Description = "Retrieves a specific memory entry by its category and key."
            });
    }

    private static McpServerTool BuildListMemoriesTool(MemoryService service, ZakiraOptions options)
    {
        if (options.HasConstCategory)
        {
            return McpServerTool.Create(
                (
                    [Description("Maximum number of results (default: 50)")] int? top,
                    [Description("Filter by author")] string? author,
                    [Description("Filter by tags (comma-separated, matches any)")] string? tags,
                    [Description("Only entries modified before this ISO 8601 timestamp")] string? before,
                    [Description("Only entries modified after this ISO 8601 timestamp")] string? after
                ) =>
                {
                    var filter = BuildListFilter(null, top, author, tags, before, after);
                    var entries = service.List(filter);
                    return FormatEntryList(entries, service.GetCount());
                },
                new McpServerToolCreateOptions
                {
                    Name = "list_memories",
                    Description = $"Lists memory entries in the '{options.ConstCategory}' category with optional filtering. " +
                                  "Returns entries ordered by last modified date (newest first)."
                });
        }

        return McpServerTool.Create(
            (
                [Description("Filter by category (omit to list across all categories)")] string? category,
                [Description("Maximum number of results (default: 50)")] int? top,
                [Description("Filter by author")] string? author,
                [Description("Filter by tags (comma-separated, matches any)")] string? tags,
                [Description("Only entries modified before this ISO 8601 timestamp")] string? before,
                [Description("Only entries modified after this ISO 8601 timestamp")] string? after
            ) =>
            {
                var filter = BuildListFilter(category, top, author, tags, before, after);
                var entries = service.List(filter);
                return FormatEntryList(entries, service.GetCount());
            },
            new McpServerToolCreateOptions
            {
                Name = "list_memories",
                Description = "Lists memory entries with optional filtering by category, author, tags, and time range. " +
                              "Returns entries ordered by last modified date (newest first)."
            });
    }

    private static McpServerTool BuildSearchMemoriesTool(MemoryService service, ZakiraOptions options)
    {
        if (options.HasConstCategory)
        {
            return McpServerTool.Create(
                (
                    [Description("Search query - describe what you're looking for in natural language")] string query,
                    [Description("Maximum number of results (default: 10)")] int? top,
                    [Description("Filter by author")] string? author,
                    [Description("Filter by tags (comma-separated, matches any)")] string? tags,
                    [Description("How to combine query tokens: 'any' (default, OR), 'all' (AND), or 'phrase' (exact contiguous phrase). Use 'all' or 'phrase' for stricter precision when 'any' is too broad.")] string? mode
                ) =>
                {
                    var filter = new SearchFilter
                    {
                        Query = query,
                        Top = top ?? 10,
                        Author = author,
                        Tags = ParseTags(tags),
                        Mode = ParseSearchMode(mode),
                    };
                    var results = service.Search(filter);
                    return FormatSearchResults(results);
                },
                new McpServerToolCreateOptions
                {
                    Name = "search_memories",
                    Description = $"Searches for memory entries in the '{options.ConstCategory}' category using hybrid semantic + keyword search. " +
                                  "Uses natural language understanding - you don't need exact keyword matches. " +
                                  "For example, 'caching strategy' will find entries about 'Redis TTL configuration'."
                });
        }

        return McpServerTool.Create(
            (
                [Description("Search query - describe what you're looking for in natural language")] string query,
                [Description("Filter by category (omit to search across all categories)")] string? category,
                [Description("Maximum number of results (default: 10)")] int? top,
                [Description("Filter by author")] string? author,
                [Description("Filter by tags (comma-separated, matches any)")] string? tags,
                [Description("How to combine query tokens: 'any' (default, OR), 'all' (AND), or 'phrase' (exact contiguous phrase). Use 'all' or 'phrase' for stricter precision when 'any' is too broad.")] string? mode
            ) =>
            {
                var filter = new SearchFilter
                {
                    Query = query,
                    Category = category,
                    Top = top ?? 10,
                    Author = author,
                    Tags = ParseTags(tags),
                    Mode = ParseSearchMode(mode),
                };
                var results = service.Search(filter);
                return FormatSearchResults(results);
            },
            new McpServerToolCreateOptions
            {
                Name = "search_memories",
                Description = "Searches for memory entries using hybrid semantic + keyword search (BM25 + vector embeddings merged via RRF). " +
                              "Uses natural language understanding - you don't need exact keyword matches. " +
                              "For example, 'caching strategy' will find entries about 'Redis TTL configuration'."
            });
    }

    // --- Formatting helpers ---

    private static string FormatEntry(MemoryEntry entry)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## [{entry.Category}] {entry.Key}");
        sb.AppendLine();
        sb.AppendLine(entry.Data);
        sb.AppendLine();
        sb.AppendLine("### Metadata");
        if (entry.Metadata.Author is not null) sb.AppendLine($"- **Author:** {entry.Metadata.Author}");
        if (entry.Metadata.Reason is not null) sb.AppendLine($"- **Reason:** {entry.Metadata.Reason}");
        if (entry.Metadata.Tags.Count > 0) sb.AppendLine($"- **Tags:** {string.Join(", ", entry.Metadata.Tags)}");
        if (entry.Metadata.Custom.Count > 0) sb.AppendLine($"- **Custom:** {JsonSerializer.Serialize(entry.Metadata.Custom, JsonOptions)}");
        sb.AppendLine($"- **Created:** {entry.Metadata.CreatedAt:O}");
        sb.AppendLine($"- **Last Modified:** {entry.Metadata.LastModifiedAt:O}");
        return sb.ToString();
    }

    private static string FormatEntryList(List<MemoryEntry> entries, long totalCount)
    {
        if (entries.Count == 0)
        {
            return "No memory entries found matching the specified filters.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Found {entries.Count} entries (total in store: {totalCount}):");
        sb.AppendLine();

        foreach (var entry in entries)
        {
            sb.AppendLine($"- **[{entry.Category}] {entry.Key}** (by {entry.Metadata.Author ?? "unknown"}, {entry.Metadata.LastModifiedAt:yyyy-MM-dd HH:mm})");
            // Show first 100 chars of data as preview
            var preview = entry.Data.Length > 100 ? entry.Data[..100] + "..." : entry.Data;
            sb.AppendLine($"  {preview}");
            if (entry.Metadata.Tags.Count > 0) sb.AppendLine($"  Tags: {string.Join(", ", entry.Metadata.Tags)}");
        }

        return sb.ToString();
    }

    private static string FormatSearchResults(List<SearchResult> results)
    {
        if (results.Count == 0)
        {
            return "No memory entries found matching the search query.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Found {results.Count} results:");
        sb.AppendLine();

        for (var i = 0; i < results.Count; i++)
        {
            var r = results[i];
            sb.AppendLine($"### {i + 1}. [{r.Entry.Category}] {r.Entry.Key} (score: {r.Score:F4})");
            sb.AppendLine();
            sb.AppendLine(r.Entry.Data);
            sb.AppendLine();
            if (r.Entry.Metadata.Author is not null) sb.AppendLine($"- **Author:** {r.Entry.Metadata.Author}");
            if (r.Entry.Metadata.Tags.Count > 0) sb.AppendLine($"- **Tags:** {string.Join(", ", r.Entry.Metadata.Tags)}");
            sb.AppendLine($"- **Last Modified:** {r.Entry.Metadata.LastModifiedAt:yyyy-MM-dd HH:mm}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    // --- Parsing helpers ---

    private static List<string>? ParseTags(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags))
        {
            return null;
        }

        return tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();
    }

    private static Dictionary<string, string>? ParseCustom(string? custom)
    {
        if (string.IsNullOrWhiteSpace(custom))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(custom, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Parses a search mode string into a <see cref="SearchMode"/>. Accepts
    /// "any" (default), "all", or "phrase" (case-insensitive). Unknown or null
    /// values fall back to <see cref="SearchMode.Any"/> so callers never error.
    /// </summary>
    private static SearchMode ParseSearchMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return SearchMode.Any;
        }

        return mode.Trim().ToLowerInvariant() switch
        {
            "all"    => SearchMode.All,
            "phrase" => SearchMode.Phrase,
            _        => SearchMode.Any,
        };
    }

    /// <summary>
    /// Parses an ISO 8601 timestamp (round-trip "O" format) supplied by an agent.
    /// Returns null when the input is null/whitespace or unparseable - the caller
    /// then treats it as "no optimistic-concurrency check", which is safe.
    /// </summary>
    private static DateTimeOffset? ParseTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind | DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed
            : (DateTimeOffset?)null;
    }

    /// <summary>
    /// Formats an <see cref="EditResult"/> as the human-readable text returned by
    /// the <c>edit_memory</c> MCP tool.
    /// </summary>
    private static string FormatEditResult(EditResult result, string category, string key)
    {
        return result.Outcome switch
        {
            EditOutcome.Updated when result.Entry is not null
                => FormatEntry(result.Entry),
            EditOutcome.NotFound
                => $"Memory entry with category '{category}' and key '{key}' not found.",
            EditOutcome.Conflict
                => $"Conflict editing memory entry [{category}] {key}: " +
                   $"the entry was modified since you last read it " +
                   $"(current lastModifiedAt: {result.CurrentLastModifiedAt?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture) ?? "unknown"}). " +
                   "Re-fetch with get_memory and merge your changes, " +
                   "or call edit_memory again with the new expectedLastModifiedAt.",
            _ => $"Memory entry with category '{category}' and key '{key}' not found.",
        };
    }

    private static ListFilter BuildListFilter(string? category, int? top, string? author, string? tags, string? before, string? after)
    {
        return new ListFilter
        {
            Category = category,
            Top = top ?? 50,
            Author = author,
            Tags = ParseTags(tags),
            Before = before is not null ? DateTimeOffset.Parse(before) : null,
            After = after is not null ? DateTimeOffset.Parse(after) : null,
        };
    }
}
