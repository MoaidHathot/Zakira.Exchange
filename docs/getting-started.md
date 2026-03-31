---
layout: default
title: Getting Started
nav_order: 2
---

# Getting Started

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

No other external services or dependencies are required. Zakira uses SQLite for storage and an embedded ONNX model for semantic search.

---

## 1. Download the ONNX Model

The semantic search feature requires the `all-MiniLM-L6-v2` ONNX model. Download it using one of the provided scripts:

**PowerShell:**
```powershell
./scripts/download-model.ps1
```

**Bash:**
```bash
./scripts/download-model.sh
```

This downloads:
- `all-MiniLM-L6-v2.onnx` (~90 MB) -- the sentence transformer model
- `vocab.txt` (~230 KB) -- the WordPiece vocabulary file

Both files are placed in `src/Zakira.Exchange.Core/Models/`.

> **Note:** The model files are included in the build output automatically. They are gitignored since they are large binary files downloaded from HuggingFace.

---

## 2. Build

```bash
dotnet build Zakira.Exchange.slnx
```

---

## 3. Use as a CLI Tool

Create, list, and search memories directly from the command line:

```bash
# Create a memory entry
dotnet run --project src/Zakira.Exchange.Cli -- create general my-key --data "Hello, world!"

# List all entries
dotnet run --project src/Zakira.Exchange.Cli -- list

# Search using natural language
dotnet run --project src/Zakira.Exchange.Cli -- search "greeting"

# List all categories
dotnet run --project src/Zakira.Exchange.Cli -- categories
```

See the [CLI Reference](cli-reference) for the complete command documentation.

---

## 4. Start as an MCP Server

Launch Zakira as an MCP server for AI agent integration:

```bash
# Start with default settings
dotnet run --project src/Zakira.Exchange.Cli -- mcp

# Start in read-only mode
dotnet run --project src/Zakira.Exchange.Cli -- mcp --access-mode read-only

# Start locked to a single category with a custom database
dotnet run --project src/Zakira.Exchange.Cli -- mcp --category notes --db ./notes.db
```

See [MCP Server](mcp-server) for detailed setup instructions for Claude Desktop, VS Code, and Cursor.

---

## Install as a Global Tool (Optional)

You can also pack and install Zakira as a .NET global tool:

```bash
dotnet pack src/Zakira.Exchange.Cli/Zakira.Exchange.Cli.csproj
dotnet tool install --global --add-source ./src/Zakira.Exchange.Cli/bin/Release Zakira.Exchange
```

Then use `zakira` directly:

```bash
zakira create general my-key --data "Hello, world!"
zakira search "greeting"
zakira mcp
```

---

## Next Steps

- [CLI Reference](cli-reference) -- Learn all available commands and options
- [MCP Server](mcp-server) -- Configure Zakira for your AI workflow
- [Configuration](configuration) -- Customize access modes, database paths, and more
