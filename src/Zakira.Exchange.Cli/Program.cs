using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Zakira.Exchange.Cli.Tools;
using Zakira.Exchange.Core.Configuration;
using Zakira.Exchange.Core.Models;
using Zakira.Exchange.Core.Services;

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true,
};

// Global options
var databaseOption = new Option<string>("--database-path")
{
    Description = "SQLite database file path",
    DefaultValueFactory = _ => Environment.GetEnvironmentVariable("ZAKIRA_DATABASE_PATH") ?? "zakira.db"
};
databaseOption.Aliases.Add("--db");
databaseOption.Aliases.Add("-d");

var accessModeOption = new Option<string>("--access-mode")
{
    Description = "Access mode: full, read-only, append-only, no-delete",
    DefaultValueFactory = _ => Environment.GetEnvironmentVariable("ZAKIRA_ACCESS_MODE") ?? "full"
};
accessModeOption.Aliases.Add("--mode");
accessModeOption.Aliases.Add("-m");

var constCategoryOption = new Option<string?>("--category")
{
    Description = "Lock all operations to this category",
    DefaultValueFactory = _ => Environment.GetEnvironmentVariable("ZAKIRA_CATEGORY")
};
constCategoryOption.Aliases.Add("-c");

var modelPathOption = new Option<string?>("--model-path")
{
    Description = "Path to the ONNX model file",
    DefaultValueFactory = _ => Environment.GetEnvironmentVariable("ZAKIRA_MODEL_PATH")
};
modelPathOption.Aliases.Add("--model");

// --- Create command ---
var createCategoryArg = new Argument<string>("category") { Description = "Category/table for the memory" };
var createKeyArg = new Argument<string>("key") { Description = "Unique key within the category" };
var createDataOption = new Option<string>("--data") { Description = "The memory content (text)", Required = true };
var createAuthorOption = new Option<string?>("--author") { Description = "Who/what created this" };
var createReasonOption = new Option<string?>("--reason") { Description = "Why this was created" };
var createTagsOption = new Option<string?>("--tags") { Description = "Comma-separated tags" };
var createCustomOption = new Option<string?>("--custom") { Description = "JSON object of custom key-value metadata" };

var createCommand = new Command("create", "Create a new memory entry");
createCommand.Arguments.Add(createCategoryArg);
createCommand.Arguments.Add(createKeyArg);
createCommand.Options.Add(createDataOption);
createCommand.Options.Add(createAuthorOption);
createCommand.Options.Add(createReasonOption);
createCommand.Options.Add(createTagsOption);
createCommand.Options.Add(createCustomOption);

createCommand.SetAction(parseResult =>
{
    var db = parseResult.GetValue(databaseOption)!;
    var mode = parseResult.GetValue(accessModeOption)!;
    var constCat = parseResult.GetValue(constCategoryOption);
    var modelPath = parseResult.GetValue(modelPathOption);

    using var service = BuildService(db, mode, constCat, modelPath);
    CheckAccess(service.Options.AccessMode, "create");

    var category = parseResult.GetValue(createCategoryArg)!;
    var key = parseResult.GetValue(createKeyArg)!;
    var data = parseResult.GetValue(createDataOption)!;
    var author = parseResult.GetValue(createAuthorOption);
    var reason = parseResult.GetValue(createReasonOption);
    var tags = parseResult.GetValue(createTagsOption);
    var custom = parseResult.GetValue(createCustomOption);

    var tagList = ParseTags(tags);
    var customDict = ParseCustom(custom, jsonOptions);
    var entry = service.Create(category, key, data, author, reason, tagList, customDict);
    Console.WriteLine($"Created memory entry [{entry.Category}] {entry.Key}");
    PrintEntry(entry, jsonOptions);
});

// --- Edit command ---
var editCategoryArg = new Argument<string>("category") { Description = "Category of the entry to edit" };
var editKeyArg = new Argument<string>("key") { Description = "Key of the entry to edit" };
var editDataOption = new Option<string?>("--data") { Description = "Updated content" };
var editAuthorOption = new Option<string?>("--author") { Description = "Updated author" };
var editReasonOption = new Option<string?>("--reason") { Description = "Updated reason" };
var editTagsOption = new Option<string?>("--tags") { Description = "Updated comma-separated tags" };
var editCustomOption = new Option<string?>("--custom") { Description = "Updated JSON object of custom metadata" };
var editExpectedOption = new Option<string?>("--expected-modified") { Description = "Optional ISO 8601 (UTC) timestamp for optimistic concurrency. Edit only applies if the entry's current lastModifiedAt matches; otherwise exits 2 (conflict)." };

var editCommand = new Command("edit", "Edit an existing memory entry");
editCommand.Arguments.Add(editCategoryArg);
editCommand.Arguments.Add(editKeyArg);
editCommand.Options.Add(editDataOption);
editCommand.Options.Add(editAuthorOption);
editCommand.Options.Add(editReasonOption);
editCommand.Options.Add(editTagsOption);
editCommand.Options.Add(editCustomOption);
editCommand.Options.Add(editExpectedOption);

editCommand.SetAction(parseResult =>
{
    var db = parseResult.GetValue(databaseOption)!;
    var mode = parseResult.GetValue(accessModeOption)!;
    var constCat = parseResult.GetValue(constCategoryOption);
    var modelPath = parseResult.GetValue(modelPathOption);

    using var service = BuildService(db, mode, constCat, modelPath);
    CheckAccess(service.Options.AccessMode, "edit");

    var category = parseResult.GetValue(editCategoryArg)!;
    var key = parseResult.GetValue(editKeyArg)!;
    var data = parseResult.GetValue(editDataOption);
    var author = parseResult.GetValue(editAuthorOption);
    var reason = parseResult.GetValue(editReasonOption);
    var tags = parseResult.GetValue(editTagsOption);
    var custom = parseResult.GetValue(editCustomOption);
    var expectedRaw = parseResult.GetValue(editExpectedOption);

    var tagList = tags is not null ? ParseTags(tags) : null;
    var customDict = custom is not null ? ParseCustom(custom, jsonOptions) : null;
    var expected = ParseTimestamp(expectedRaw);

    if (expectedRaw is not null && expected is null)
    {
        Console.Error.WriteLine($"Invalid --expected-modified value: '{expectedRaw}'. Use an ISO 8601 timestamp like 2026-06-07T15:32:11.234Z.");
        return 1;
    }

    var result = service.EditWithConcurrency(category, key, data, author, reason, tagList, customDict, expected);
    switch (result.Outcome)
    {
        case EditOutcome.Updated when result.Entry is not null:
            Console.WriteLine($"Updated memory entry [{result.Entry.Category}] {result.Entry.Key}");
            PrintEntry(result.Entry, jsonOptions);
            return 0;
        case EditOutcome.NotFound:
            Console.Error.WriteLine($"Memory entry [{category}] {key} not found.");
            return 1;
        case EditOutcome.Conflict:
            var current = result.CurrentLastModifiedAt?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture) ?? "unknown";
            Console.Error.WriteLine($"Conflict editing memory entry [{category}] {key}: " +
                $"the entry was modified since you last read it (current lastModifiedAt: {current}). " +
                "Re-fetch with `get` and merge your changes, or re-run with the new --expected-modified.");
            return 2;
        default:
            Console.Error.WriteLine($"Memory entry [{category}] {key} not found.");
            return 1;
    }
});

// --- Delete command ---
var deleteCategoryArg = new Argument<string>("category") { Description = "Category of the entry to delete" };
var deleteKeyArg = new Argument<string>("key") { Description = "Key of the entry to delete" };

var deleteCommand = new Command("delete", "Delete a memory entry");
deleteCommand.Arguments.Add(deleteCategoryArg);
deleteCommand.Arguments.Add(deleteKeyArg);

deleteCommand.SetAction(parseResult =>
{
    var db = parseResult.GetValue(databaseOption)!;
    var mode = parseResult.GetValue(accessModeOption)!;
    var constCat = parseResult.GetValue(constCategoryOption);
    var modelPath = parseResult.GetValue(modelPathOption);

    using var service = BuildService(db, mode, constCat, modelPath);
    CheckAccess(service.Options.AccessMode, "delete");

    var category = parseResult.GetValue(deleteCategoryArg)!;
    var key = parseResult.GetValue(deleteKeyArg)!;

    var deleted = service.Delete(category, key);
    if (deleted)
    {
        Console.WriteLine($"Deleted memory entry [{category}] {key}.");
    }
    else
    {
        Console.Error.WriteLine($"Memory entry [{category}] {key} not found.");
        return 1;
    }
    return 0;
});

// --- Get command ---
var getCategoryArg = new Argument<string>("category") { Description = "Category of the entry" };
var getKeyArg = new Argument<string>("key") { Description = "Key of the entry" };

var getCommand = new Command("get", "Get a specific memory entry");
getCommand.Arguments.Add(getCategoryArg);
getCommand.Arguments.Add(getKeyArg);

getCommand.SetAction(parseResult =>
{
    var db = parseResult.GetValue(databaseOption)!;
    var mode = parseResult.GetValue(accessModeOption)!;
    var constCat = parseResult.GetValue(constCategoryOption);
    var modelPath = parseResult.GetValue(modelPathOption);

    using var service = BuildService(db, mode, constCat, modelPath);

    var category = parseResult.GetValue(getCategoryArg)!;
    var key = parseResult.GetValue(getKeyArg)!;

    var entry = service.Get(category, key);
    if (entry is null)
    {
        Console.Error.WriteLine($"Memory entry [{category}] {key} not found.");
        return 1;
    }

    PrintEntry(entry, jsonOptions);
    return 0;
});

// --- List command ---
var listCategoryOption = new Option<string?>("--cat") { Description = "Filter by category" };
var listTopOption = new Option<int>("--top") { Description = "Max results", DefaultValueFactory = _ => 50 };
listTopOption.Aliases.Add("-n");
var listAuthorOption = new Option<string?>("--author") { Description = "Filter by author" };
var listTagsOption = new Option<string?>("--tags") { Description = "Filter by tags (comma-separated)" };
var listBeforeOption = new Option<string?>("--before") { Description = "Only entries before this ISO 8601 timestamp" };
var listAfterOption = new Option<string?>("--after") { Description = "Only entries after this ISO 8601 timestamp" };

var listCommand = new Command("list", "List memory entries with filtering");
listCommand.Options.Add(listCategoryOption);
listCommand.Options.Add(listTopOption);
listCommand.Options.Add(listAuthorOption);
listCommand.Options.Add(listTagsOption);
listCommand.Options.Add(listBeforeOption);
listCommand.Options.Add(listAfterOption);

listCommand.SetAction(parseResult =>
{
    var db = parseResult.GetValue(databaseOption)!;
    var mode = parseResult.GetValue(accessModeOption)!;
    var constCat = parseResult.GetValue(constCategoryOption);
    var modelPath = parseResult.GetValue(modelPathOption);

    using var service = BuildService(db, mode, constCat, modelPath);

    var category = parseResult.GetValue(listCategoryOption);
    var top = parseResult.GetValue(listTopOption);
    var author = parseResult.GetValue(listAuthorOption);
    var tags = parseResult.GetValue(listTagsOption);
    var before = parseResult.GetValue(listBeforeOption);
    var after = parseResult.GetValue(listAfterOption);

    var filter = new ListFilter
    {
        Category = category,
        Top = top,
        Author = author,
        Tags = ParseTags(tags),
        Before = before is not null ? DateTimeOffset.Parse(before) : null,
        After = after is not null ? DateTimeOffset.Parse(after) : null,
    };

    var entries = service.List(filter);
    var total = service.GetCount();

    Console.WriteLine($"Found {entries.Count} entries (total: {total}):");
    Console.WriteLine();
    foreach (var entry in entries)
    {
        var preview = entry.Data.Length > 80 ? entry.Data[..80] + "..." : entry.Data;
        Console.WriteLine($"  [{entry.Category}] {entry.Key}  (by {entry.Metadata.Author ?? "unknown"}, {entry.Metadata.LastModifiedAt:yyyy-MM-dd HH:mm})");
        Console.WriteLine($"    {preview}");
        if (entry.Metadata.Tags.Count > 0)
            Console.WriteLine($"    Tags: {string.Join(", ", entry.Metadata.Tags)}");
    }
});

// --- Search command ---
var searchQueryArg = new Argument<string>("query") { Description = "Search query (natural language)" };
var searchCategoryOption = new Option<string?>("--cat") { Description = "Filter by category" };
var searchTopOption = new Option<int>("--top") { Description = "Max results", DefaultValueFactory = _ => 10 };
searchTopOption.Aliases.Add("-n");
var searchAuthorOption = new Option<string?>("--author") { Description = "Filter by author" };
var searchTagsOption = new Option<string?>("--tags") { Description = "Filter by tags (comma-separated)" };
var searchModeOption = new Option<string?>("--mode") { Description = "How to combine query tokens: any (default, OR), all (AND), or phrase (exact contiguous phrase)" };

var searchCommand = new Command("search", "Search memories using hybrid semantic + keyword search");
searchCommand.Arguments.Add(searchQueryArg);
searchCommand.Options.Add(searchCategoryOption);
searchCommand.Options.Add(searchTopOption);
searchCommand.Options.Add(searchAuthorOption);
searchCommand.Options.Add(searchTagsOption);
searchCommand.Options.Add(searchModeOption);

searchCommand.SetAction(parseResult =>
{
    var db = parseResult.GetValue(databaseOption)!;
    var mode = parseResult.GetValue(accessModeOption)!;
    var constCat = parseResult.GetValue(constCategoryOption);
    var modelPath = parseResult.GetValue(modelPathOption);

    using var service = BuildService(db, mode, constCat, modelPath);

    var query = parseResult.GetValue(searchQueryArg)!;
    var category = parseResult.GetValue(searchCategoryOption);
    var top = parseResult.GetValue(searchTopOption);
    var author = parseResult.GetValue(searchAuthorOption);
    var tags = parseResult.GetValue(searchTagsOption);
    var searchMode = parseResult.GetValue(searchModeOption);

    var filter = new SearchFilter
    {
        Query = query,
        Category = category,
        Top = top,
        Author = author,
        Tags = ParseTags(tags),
        Mode = ParseSearchMode(searchMode),
    };

    var results = service.Search(filter);

    Console.WriteLine($"Found {results.Count} results:");
    Console.WriteLine();
    for (var i = 0; i < results.Count; i++)
    {
        var r = results[i];
        Console.WriteLine($"  {i + 1}. [{r.Entry.Category}] {r.Entry.Key}  (score: {r.Score:F4})");
        var preview = r.Entry.Data.Length > 80 ? r.Entry.Data[..80] + "..." : r.Entry.Data;
        Console.WriteLine($"     {preview}");
        if (r.Entry.Metadata.Author is not null) Console.WriteLine($"     Author: {r.Entry.Metadata.Author}");
        if (r.Entry.Metadata.Tags.Count > 0) Console.WriteLine($"     Tags: {string.Join(", ", r.Entry.Metadata.Tags)}");
        Console.WriteLine();
    }
});

// --- Categories command ---
var categoriesCommand = new Command("categories", "List all categories");

categoriesCommand.SetAction(parseResult =>
{
    var db = parseResult.GetValue(databaseOption)!;
    var mode = parseResult.GetValue(accessModeOption)!;
    var constCat = parseResult.GetValue(constCategoryOption);
    var modelPath = parseResult.GetValue(modelPathOption);

    using var service = BuildService(db, mode, constCat, modelPath);
    var categories = service.GetCategories();
    Console.WriteLine($"Categories ({categories.Count}):");
    foreach (var cat in categories)
    {
        var count = service.GetCount(cat);
        Console.WriteLine($"  {cat} ({count} entries)");
    }
});

// --- MCP command ---
var mcpCommand = new Command("mcp", "Start as an MCP server (stdio transport) for AI agent integration");

mcpCommand.SetAction(async parseResult =>
{
    var db = parseResult.GetValue(databaseOption)!;
    var mode = parseResult.GetValue(accessModeOption)!;
    var constCat = parseResult.GetValue(constCategoryOption);
    var modelPath = parseResult.GetValue(modelPathOption);

    var options = BuildOptions(db, mode, constCat, modelPath);

    // Log startup diagnostics to stderr (visible in MCP client logs)
    Console.Error.WriteLine("[Zakira.Exchange] Starting MCP server...");
    Console.Error.WriteLine($"[Zakira.Exchange] Database: {Path.GetFullPath(options.DatabasePath)}");
    Console.Error.WriteLine($"[Zakira.Exchange] Access mode: {options.AccessMode.ToString().ToLowerInvariant()}");
    if (options.HasConstCategory) Console.Error.WriteLine($"[Zakira.Exchange] Const category: {options.ConstCategory}");
    if (options.ModelPath is not null) Console.Error.WriteLine($"[Zakira.Exchange] Custom model: {options.ModelPath}");

    // Initialize the memory service
    using var memoryService = new MemoryService(options);

    var entryCount = memoryService.GetCount();
    Console.Error.WriteLine($"[Zakira.Exchange] Database initialized. {entryCount} entries found.");

    // Build tools based on access mode and const-category
    var tools = ToolBuilder.BuildTools(memoryService);
    Console.Error.WriteLine($"[Zakira.Exchange] Registered {tools.Count} tools: {string.Join(", ", tools.Select(t => t.ProtocolTool.Name))}");

    // Build and run the MCP server
    var builder = Host.CreateApplicationBuilder();
    builder.Logging.AddConsole(consoleLogOptions =>
    {
        consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
    });

    builder.Services
        .AddSingleton(memoryService)
        .AddMcpServer(mcpOptions =>
        {
            mcpOptions.ServerInfo = new()
            {
                Name = "zakira-exchange",
                Version = typeof(ToolBuilder).Assembly.GetName().Version?.ToString() ?? "1.0.0",
            };
        })
        .WithStdioServerTransport()
        .WithTools(tools);

    await builder.Build().RunAsync();
});

// --- Root command ---
var rootCommand = new RootCommand("Zakira.Exchange - AI Agent Memory Storage & Semantic Search");
rootCommand.Subcommands.Add(createCommand);
rootCommand.Subcommands.Add(editCommand);
rootCommand.Subcommands.Add(deleteCommand);
rootCommand.Subcommands.Add(getCommand);
rootCommand.Subcommands.Add(listCommand);
rootCommand.Subcommands.Add(searchCommand);
rootCommand.Subcommands.Add(categoriesCommand);
rootCommand.Subcommands.Add(mcpCommand);

// Add global options (recursive to all subcommands)
databaseOption.Recursive = true;
rootCommand.Options.Add(databaseOption);
accessModeOption.Recursive = true;
rootCommand.Options.Add(accessModeOption);
constCategoryOption.Recursive = true;
rootCommand.Options.Add(constCategoryOption);
modelPathOption.Recursive = true;
rootCommand.Options.Add(modelPathOption);

return rootCommand.Parse(args).Invoke();

// --- Helper methods ---

static ZakiraOptions BuildOptions(string db, string mode, string? constCat, string? modelPath)
{
    return new ZakiraOptions
    {
        DatabasePath = db,
        AccessMode = mode.ToLowerInvariant() switch
        {
            "full" => AccessMode.Full,
            "read-only" or "readonly" => AccessMode.ReadOnly,
            "append-only" or "appendonly" => AccessMode.AppendOnly,
            "no-delete" or "nodelete" => AccessMode.NoDelete,
            _ => AccessMode.Full
        },
        ConstCategory = constCat,
        ModelPath = modelPath,
    };
}

static MemoryService BuildService(string db, string mode, string? constCat, string? modelPath)
{
    return new MemoryService(BuildOptions(db, mode, constCat, modelPath));
}

static void CheckAccess(AccessMode mode, string operation)
{
    var allowed = operation switch
    {
        "create" => mode.CanCreate(),
        "edit" => mode.CanEdit(),
        "delete" => mode.CanDelete(),
        _ => true
    };

    if (!allowed)
    {
        Console.Error.WriteLine($"Operation '{operation}' is not allowed in '{mode}' access mode.");
        Environment.Exit(1);
    }
}

static List<string>? ParseTags(string? tags)
{
    if (string.IsNullOrWhiteSpace(tags)) return null;
    return tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(t => !string.IsNullOrWhiteSpace(t))
        .ToList();
}

static SearchMode ParseSearchMode(string? mode)
{
    if (string.IsNullOrWhiteSpace(mode)) return SearchMode.Any;
    return mode.Trim().ToLowerInvariant() switch
    {
        "all"    => SearchMode.All,
        "phrase" => SearchMode.Phrase,
        _        => SearchMode.Any,
    };
}

static DateTimeOffset? ParseTimestamp(string? value)
{
    if (string.IsNullOrWhiteSpace(value)) return null;
    return DateTimeOffset.TryParse(
        value,
        CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind | DateTimeStyles.AssumeUniversal,
        out var parsed)
        ? parsed
        : (DateTimeOffset?)null;
}

static Dictionary<string, string>? ParseCustom(string? custom, JsonSerializerOptions jsonOptions)
{
    if (string.IsNullOrWhiteSpace(custom)) return null;
    try { return JsonSerializer.Deserialize<Dictionary<string, string>>(custom, jsonOptions); }
    catch { return null; }
}

static void PrintEntry(MemoryEntry entry, JsonSerializerOptions jsonOptions)
{
    Console.WriteLine($"  Category:      {entry.Category}");
    Console.WriteLine($"  Key:           {entry.Key}");
    Console.WriteLine($"  Data:          {entry.Data}");
    if (entry.Metadata.Author is not null) Console.WriteLine($"  Author:        {entry.Metadata.Author}");
    if (entry.Metadata.Reason is not null) Console.WriteLine($"  Reason:        {entry.Metadata.Reason}");
    if (entry.Metadata.Tags.Count > 0) Console.WriteLine($"  Tags:          {string.Join(", ", entry.Metadata.Tags)}");
    if (entry.Metadata.Custom.Count > 0) Console.WriteLine($"  Custom:        {JsonSerializer.Serialize(entry.Metadata.Custom, jsonOptions)}");
    Console.WriteLine($"  Created:       {entry.Metadata.CreatedAt:O}");
    Console.WriteLine($"  Last Modified: {entry.Metadata.LastModifiedAt:O}");
}
