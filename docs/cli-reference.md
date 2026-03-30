---
layout: default
title: CLI Reference
nav_order: 3
---

# CLI Reference

Zakira provides a full-featured command-line interface for managing memory entries. All commands support global options for database path, access mode, const-category, and model path.

---

## Global Options

These options apply to all commands, including `mcp`:

| Flag | Alias | Description | Default |
|------|-------|-------------|---------|
| `--database-path` | `--db`, `-d` | SQLite database file path | `./zakira.db` |
| `--access-mode` | `--mode`, `-m` | Access restriction mode | `full` |
| `--const-category` | `--category`, `-c` | Lock to a single category | none |
| `--model-path` | `--model` | Custom ONNX model path | auto-detect |

---

## Commands

### `create`

Create a new memory entry.

```
zakira create <category> <key> --data <text> [options]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `category` | argument | yes | Category/table for the memory |
| `key` | argument | yes | Unique key within the category |
| `--data` | option | yes | The memory content (text) |
| `--author` | option | no | Who/what created this |
| `--reason` | option | no | Why this was created |
| `--tags` | option | no | Comma-separated tags |
| `--custom` | option | no | JSON object of custom key-value metadata |

**Example:**
```bash
zakira create decisions auth-strategy \
  --data "Use JWT with short-lived access tokens and refresh token rotation" \
  --author "architect" \
  --reason "Security review decision" \
  --tags "security,jwt,authentication" \
  --custom '{"reviewed_by":"team-lead","sprint":"14"}'
```

---

### `edit`

Edit an existing memory entry. Only provided fields are updated; omitted fields keep their current values.

```
zakira edit <category> <key> [options]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `category` | argument | yes | Category of the entry to edit |
| `key` | argument | yes | Key of the entry to edit |
| `--data` | option | no | Updated content |
| `--author` | option | no | Updated author |
| `--reason` | option | no | Updated reason |
| `--tags` | option | no | Updated comma-separated tags |
| `--custom` | option | no | Updated JSON object of custom metadata |

**Example:**
```bash
zakira edit decisions auth-strategy \
  --data "Use JWT with short-lived access tokens (15min) and refresh token rotation (7 days)" \
  --tags "security,jwt,authentication,updated"
```

---

### `delete`

Delete a memory entry permanently.

```
zakira delete <category> <key>
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `category` | argument | yes | Category of the entry to delete |
| `key` | argument | yes | Key of the entry to delete |

**Example:**
```bash
zakira delete decisions auth-strategy
```

---

### `get`

Retrieve a specific memory entry by category and key.

```
zakira get <category> <key>
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `category` | argument | yes | Category of the entry |
| `key` | argument | yes | Key of the entry |

**Example:**
```bash
zakira get decisions auth-strategy
```

---

### `list`

List memory entries with optional filtering.

```
zakira list [options]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--category` / `--cat` | option | no | Filter by category |
| `--top` / `-n` | option | no | Max results (default: 50) |
| `--author` | option | no | Filter by author |
| `--tags` | option | no | Filter by tags (comma-separated, matches any) |
| `--before` | option | no | Only entries before this ISO 8601 timestamp |
| `--after` | option | no | Only entries after this ISO 8601 timestamp |

**Examples:**
```bash
# List all entries
zakira list

# List entries in a specific category
zakira list --category decisions

# List entries by a specific author, limited to 10 results
zakira list --author architect --top 10

# List entries with specific tags
zakira list --tags "security,authentication"

# List entries modified within a time range
zakira list --after 2026-01-01T00:00:00Z --before 2026-06-01T00:00:00Z
```

---

### `search`

Search memories using hybrid semantic + keyword search. This uses natural language understanding -- you do not need exact keyword matches.

```
zakira search <query> [options]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `query` | argument | yes | Search query (natural language) |
| `--category` / `--cat` | option | no | Filter by category |
| `--top` / `-n` | option | no | Max results (default: 10) |
| `--author` | option | no | Filter by author |
| `--tags` | option | no | Filter by tags (comma-separated, matches any) |

**Examples:**
```bash
# Semantic search across all memories
zakira search "caching strategy"

# Search within a specific category
zakira search "authentication approach" --category decisions

# Search with tag filtering
zakira search "performance optimization" --tags "backend"
```

> **Tip:** The hybrid search engine combines BM25 keyword matching with semantic vector similarity. A query like `"caching strategy"` will find entries about `"Redis TTL configuration"` even without shared keywords.

---

### `categories`

List all categories and their entry counts.

```
zakira categories
```

**Example output:**
```
Categories (3):
  decisions (12 entries)
  architecture (8 entries)
  preferences (5 entries)
```

---

### `mcp`

Start Zakira as an MCP server using stdio transport.

```
zakira mcp [global options]
```

See [MCP Server](mcp-server) for detailed configuration.

**Examples:**
```bash
# Start with defaults
zakira mcp

# Start in read-only mode
zakira mcp --access-mode read-only

# Start locked to a single category
zakira mcp --const-category notes --db ./notes.db
```
