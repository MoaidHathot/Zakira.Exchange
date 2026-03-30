---
layout: default
title: Configuration
nav_order: 5
---

# Configuration

Zakira can be configured via CLI flags or environment variables. All settings apply globally to every subcommand, including `mcp`.

---

## CLI Flags

| Flag | Alias | Description | Default |
|------|-------|-------------|---------|
| `--database-path` | `--db`, `-d` | SQLite database file path | `./zakira.db` |
| `--access-mode` | `--mode`, `-m` | Access restriction mode | `full` |
| `--const-category` | `--category`, `-c` | Lock to a single category | none |
| `--model-path` | `--model` | Custom ONNX model path | auto-detect |

---

## Environment Variables

Every CLI flag has a corresponding environment variable. Environment variables are useful when configuring MCP clients that support environment variable injection.

| Variable | Equivalent Flag | Example |
|----------|----------------|---------|
| `ZAKIRA_DATABASE_PATH` | `--database-path` | `./memories.db` |
| `ZAKIRA_ACCESS_MODE` | `--access-mode` | `read-only` |
| `ZAKIRA_CONST_CATEGORY` | `--const-category` | `project-notes` |
| `ZAKIRA_MODEL_PATH` | `--model-path` | `/path/to/models/` |

CLI flags take precedence over environment variables when both are set.

---

## Access Modes

Access modes restrict which operations are available. When running as an MCP server, tools for disallowed operations are not registered, making them completely invisible to agents.

| Mode | Create | Read | Edit | Delete |
|------|--------|------|------|--------|
| `full` | yes | yes | yes | yes |
| `read-only` | no | yes | no | no |
| `append-only` | yes | yes | no | no |
| `no-delete` | yes | yes | yes | no |

### Usage Examples

```bash
# Full access (default)
zakira mcp

# Read-only: agents can only search and retrieve
zakira mcp --access-mode read-only

# Append-only: agents can create but not modify or delete
zakira mcp --access-mode append-only

# No-delete: agents can create and edit but not delete
zakira mcp --access-mode no-delete
```

---

## Const-Category Mode

When `--const-category` is set, all operations are locked to that single category. The category parameter is hidden from MCP tool schemas, so agents cannot see or change it.

This is useful for:
- Restricting an agent to a specific namespace (e.g., project-specific notes)
- Simplifying the agent's tool interface by removing the category parameter
- Preventing agents from creating entries in arbitrary categories

```bash
# Lock to "project-notes" category
zakira mcp --const-category project-notes

# Combine with access mode
zakira mcp --const-category project-notes --access-mode no-delete
```

---

## Database Path

The database file is created automatically if it does not exist. SQLite WAL mode is enabled by default for concurrent access.

```bash
# Default: ./zakira.db in the current directory
zakira mcp

# Custom path
zakira mcp --db /path/to/my-memories.db
```

---

## Model Path

By default, Zakira looks for the ONNX model files (`all-MiniLM-L6-v2.onnx` and `vocab.txt`) in the assembly's directory. You can override this with a custom path.

```bash
zakira mcp --model-path /path/to/models/
```

> **Note:** The model is loaded lazily. It is only initialized when a create, edit, or search operation is performed. List, get, delete, and categories commands work without the model.
