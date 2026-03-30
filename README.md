# Zakira.Exchange

An MCP server and CLI tool that enables AI agents to save, share, and search memories. Built with .NET 10, standalone with no external service dependencies.

Memories are stored in SQLite with full-text search (FTS5) and semantic vector search (ONNX embeddings), merged via Reciprocal Rank Fusion for hybrid retrieval.

## Features

- **Structured memory entries** with category, key, data, and rich metadata (author, reason, tags, custom key-value pairs, auto-managed timestamps)
- **Categories/tables** to separate different types of memories
- **Full CRUD** - create, edit, delete, get, list entries
- **Hybrid search (RAG)** - BM25 keyword search + cosine vector similarity via `all-MiniLM-L6-v2` ONNX embeddings, fused with Reciprocal Rank Fusion (k=60)
- **Access mode control** - restrict which operations agents can perform
- **Const-category mode** - lock all operations to a single category, hiding the parameter from agents
- **Single tool, two modes** - use `zakira` as a CLI tool, or `zakira mcp` to start as an MCP server

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### 1. Download the ONNX model

```powershell
# PowerShell
./scripts/download-model.ps1
```

```bash
# Bash
./scripts/download-model.sh
```

This downloads `all-MiniLM-L6-v2.onnx` (~90MB) and `vocab.txt` (~230KB) from HuggingFace into `src/Zakira.Exchange.Core/Models/`.

### 2. Build

```bash
dotnet build Zakira.Exchange.slnx
```

### 3. Use as a CLI tool

```bash
dotnet run --project src/Zakira.Exchange.Cli -- create general my-key --data "Hello, world!"
dotnet run --project src/Zakira.Exchange.Cli -- list
dotnet run --project src/Zakira.Exchange.Cli -- search "greeting"
dotnet run --project src/Zakira.Exchange.Cli -- categories
```

### 4. Start as an MCP server

```bash
dotnet run --project src/Zakira.Exchange.Cli -- mcp
dotnet run --project src/Zakira.Exchange.Cli -- mcp --access-mode read-only
dotnet run --project src/Zakira.Exchange.Cli -- mcp --const-category notes --db ./notes.db
```

## MCP Server Configuration

Add to your MCP client configuration (e.g. Claude Desktop, VS Code, Cursor):

```json
{
  "servers": {
    "zakira": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "path/to/src/Zakira.Exchange.Cli", "--", "mcp"]
    }
  }
}
```

Or if installed as a dotnet tool:

```json
{
  "servers": {
    "zakira": {
      "type": "stdio",
      "command": "zakira",
      "args": ["mcp", "--database-path", "./memories.db"]
    }
  }
}
```

### MCP Tools

The server exposes up to 6 tools depending on the access mode:

| Tool | Description | Modes |
|------|-------------|-------|
| `create_memory` | Create a new memory entry | full, append-only, no-delete |
| `edit_memory` | Edit an existing entry | full, no-delete |
| `delete_memory` | Delete an entry | full |
| `get_memory` | Get a specific entry by category+key | all |
| `list_memories` | List entries with filtering | all |
| `search_memories` | Hybrid semantic + keyword search | all |

Tools not permitted by the access mode are simply not registered, making them invisible to agents.

## CLI Commands

```
zakira create <category> <key> --data <text> [--author <name>] [--reason <text>] [--tags <csv>] [--custom <json>]
zakira edit <category> <key> [--data <text>] [--author <name>] [--reason <text>] [--tags <csv>] [--custom <json>]
zakira delete <category> <key>
zakira get <category> <key>
zakira list [--category <name>] [--top <n>] [--author <name>] [--tags <csv>] [--before <iso8601>] [--after <iso8601>]
zakira search <query> [--category <name>] [--top <n>] [--author <name>] [--tags <csv>]
zakira categories
zakira mcp                              # Start as MCP server
```

## Concurrent Access

Multiple processes can access the same database simultaneously. SQLite WAL mode is enabled by default, so:

- An MCP server can be running while you query the same database via CLI
- Multiple MCP server instances can share the same database
- Readers are never blocked by writers

```bash
# Terminal 1: MCP server running, agent is using it
zakira mcp --db ./memories.db

# Terminal 2: Query the same database via CLI
zakira list --db ./memories.db
zakira search "something" --db ./memories.db
```

## Options

### CLI flags

| Flag | Alias | Description | Default |
|------|-------|-------------|---------|
| `--database-path` | `--db`, `-d` | SQLite database file path | `./zakira.db` |
| `--access-mode` | `--mode`, `-m` | Access restriction mode | `full` |
| `--const-category` | `--category`, `-c` | Lock to a single category | none |
| `--model-path` | `--model` | Custom ONNX model path | auto-detect |

All flags are global and apply to every subcommand, including `mcp`.

### Environment variables

| Variable | Equivalent flag |
|----------|----------------|
| `ZAKIRA_DATABASE_PATH` | `--database-path` |
| `ZAKIRA_ACCESS_MODE` | `--access-mode` |
| `ZAKIRA_CONST_CATEGORY` | `--const-category` |
| `ZAKIRA_MODEL_PATH` | `--model-path` |

### Access modes

| Mode | Create | Read | Edit | Delete |
|------|--------|------|------|--------|
| `full` | yes | yes | yes | yes |
| `read-only` | no | yes | no | no |
| `append-only` | yes | yes | no | no |
| `no-delete` | yes | yes | yes | no |

## Architecture

```
Zakira.Exchange.Core        Business logic, storage, search, embeddings
Zakira.Exchange.Cli         Single entry point: CLI commands + MCP server (via 'mcp' subcommand)
```

### How search works

1. **Keyword search**: SQLite FTS5 with BM25 scoring against the entry's key, data, tags, and reason
2. **Vector search**: ONNX `all-MiniLM-L6-v2` generates 384-dim embeddings; cosine similarity via dot product (L2-normalized vectors)
3. **Fusion**: Reciprocal Rank Fusion (k=60) merges both ranked lists into a single result set
4. **Post-filtering**: Author and tag filters are applied after fusion

Each entry is embedded as a single unit (key + data + tags + reason concatenated). No chunking is needed since memories are typically short structured entries.

## License

[Unlicense](LICENSE) - public domain.
