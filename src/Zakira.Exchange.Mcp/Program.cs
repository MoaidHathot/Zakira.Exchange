using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Zakira.Exchange.Core.Configuration;
using Zakira.Exchange.Core.Services;
using Zakira.Exchange.Mcp.Tools;

// Parse CLI arguments
var options = ParseArguments(args);

// Log startup diagnostics to stderr (visible in MCP client logs)
var accessModeStr = options.AccessMode.ToString().ToLowerInvariant();
Console.Error.WriteLine($"[Zakira.Exchange] Starting MCP server...");
Console.Error.WriteLine($"[Zakira.Exchange] Database: {Path.GetFullPath(options.DatabasePath)}");
Console.Error.WriteLine($"[Zakira.Exchange] Access mode: {accessModeStr}");
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

// --- Argument parsing ---

static ZakiraOptions ParseArguments(string[] args)
{
    var options = new ZakiraOptions();

    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i].ToLowerInvariant())
        {
            case "--database-path" or "--db" or "-d":
                if (i + 1 < args.Length) options.DatabasePath = args[++i];
                break;

            case "--access-mode" or "--mode" or "-m":
                if (i + 1 < args.Length)
                {
                    options.AccessMode = args[++i].ToLowerInvariant() switch
                    {
                        "full" => AccessMode.Full,
                        "read-only" or "readonly" => AccessMode.ReadOnly,
                        "append-only" or "appendonly" => AccessMode.AppendOnly,
                        "no-delete" or "nodelete" => AccessMode.NoDelete,
                        _ => throw new ArgumentException($"Unknown access mode: {args[i]}. Valid values: full, read-only, append-only, no-delete")
                    };
                }
                break;

            case "--const-category" or "--category" or "-c":
                if (i + 1 < args.Length) options.ConstCategory = args[++i];
                break;

            case "--model-path" or "--model":
                if (i + 1 < args.Length) options.ModelPath = args[++i];
                break;

            case "--help" or "-h":
                PrintHelp();
                Environment.Exit(0);
                break;
        }
    }

    // Also check environment variables
    if (Environment.GetEnvironmentVariable("ZAKIRA_DATABASE_PATH") is { } dbPath && string.IsNullOrEmpty(options.DatabasePath))
        options.DatabasePath = dbPath;

    if (Environment.GetEnvironmentVariable("ZAKIRA_ACCESS_MODE") is { } mode)
    {
        options.AccessMode = mode.ToLowerInvariant() switch
        {
            "full" => AccessMode.Full,
            "read-only" or "readonly" => AccessMode.ReadOnly,
            "append-only" or "appendonly" => AccessMode.AppendOnly,
            "no-delete" or "nodelete" => AccessMode.NoDelete,
            _ => options.AccessMode
        };
    }

    if (Environment.GetEnvironmentVariable("ZAKIRA_CONST_CATEGORY") is { } constCat)
        options.ConstCategory = constCat;

    if (Environment.GetEnvironmentVariable("ZAKIRA_MODEL_PATH") is { } modelPath)
        options.ModelPath = modelPath;

    return options;
}

static void PrintHelp()
{
    Console.Error.WriteLine("""
        Zakira.Exchange - MCP Server for AI Agent Memory Storage & Semantic Search

        Usage:
          zakira [options]

        Options:
          --database-path, --db, -d <path>     SQLite database file path (default: ./zakira.db)
          --access-mode, --mode, -m <mode>     Access mode: full, read-only, append-only, no-delete (default: full)
          --const-category, --category, -c <name>  Lock all tools to this category (hides category parameter)
          --model-path, --model <path>         Path to custom ONNX model directory
          --help, -h                           Show this help

        Access Modes:
          full         All operations: create, read, edit, delete
          read-only    Read only: list and search
          append-only  Read + create only (no edit or delete)
          no-delete    Read + create + edit (no delete)

        Environment Variables:
          ZAKIRA_DATABASE_PATH    Same as --database-path
          ZAKIRA_ACCESS_MODE      Same as --access-mode
          ZAKIRA_CONST_CATEGORY   Same as --const-category
          ZAKIRA_MODEL_PATH       Same as --model-path

        MCP Client Configuration Example:
          {
            "servers": {
              "zakira": {
                "type": "stdio",
                "command": "zakira",
                "args": ["--database-path", "./memories.db", "--access-mode", "full"]
              }
            }
          }
        """);
}
