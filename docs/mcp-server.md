---
layout: default
title: MCP Server
nav_order: 4
---

# MCP Server

Zakira can run as a [Model Context Protocol (MCP)](https://modelcontextprotocol.io/) server, enabling AI agents to save, retrieve, and search memories during conversations.

---

## Starting the Server

```bash
# Using dotnet run
dotnet run --project src/Zakira.Exchange.Cli -- mcp

# If installed as a global tool
zakira mcp
```

The server uses **stdio transport** -- it reads JSON-RPC messages from stdin and writes responses to stdout. Diagnostic logs are written to stderr.

---

## Client Configuration

### Claude Desktop

Add to your Claude Desktop configuration file:

**Using dotnet run:**
```json
{
  "mcpServers": {
    "zakira": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "path/to/src/Zakira.Exchange.Cli", "--", "mcp"]
    }
  }
}
```

**Using the global tool:**
```json
{
  "mcpServers": {
    "zakira": {
      "type": "stdio",
      "command": "zakira",
      "args": ["mcp", "--database-path", "./memories.db"]
    }
  }
}
```

### VS Code

Add to your VS Code MCP settings (`.vscode/mcp.json` or user settings):

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

### Cursor

Add to your Cursor MCP configuration:

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

### With Access Mode and Const-Category

You can restrict what agents can do and lock them to a specific category:

```json
{
  "servers": {
    "zakira": {
      "type": "stdio",
      "command": "zakira",
      "args": ["mcp", "--access-mode", "no-delete", "--const-category", "project-notes"]
    }
  }
}
```

### Using Environment Variables

Environment variables can be used instead of or alongside CLI flags:

```json
{
  "servers": {
    "zakira": {
      "type": "stdio",
      "command": "zakira",
      "args": ["mcp"],
      "env": {
        "ZAKIRA_DATABASE_PATH": "./memories.db",
        "ZAKIRA_ACCESS_MODE": "no-delete",
        "ZAKIRA_CONST_CATEGORY": "project-notes"
      }
    }
  }
}
```

---

## MCP Tools

The server exposes up to 6 tools depending on the configured access mode. Tools not permitted by the access mode are simply not registered, making them invisible to agents.

### `create_memory`

Creates a new memory entry with a unique (category, key) pair.

| Parameter | Required | Description |
|-----------|----------|-------------|
| `category` | yes* | Category to store the memory in |
| `key` | yes | Unique key within the category |
| `data` | yes | The memory content (text) |
| `author` | no | Who/what is creating this memory |
| `reason` | no | Why this memory is being created |
| `tags` | no | Comma-separated tags |
| `custom` | no | JSON object of custom key-value metadata |

*Hidden when const-category is set.

**Available in modes:** full, append-only, no-delete

---

### `edit_memory`

Edits an existing memory entry. Only provided fields are updated.

| Parameter | Required | Description |
|-----------|----------|-------------|
| `category` | yes* | Category of the entry to edit |
| `key` | yes | Key of the entry to edit |
| `data` | no | Updated content |
| `author` | no | Updated author |
| `reason` | no | Updated reason |
| `tags` | no | Updated tags |
| `custom` | no | Updated custom metadata |

*Hidden when const-category is set.

**Available in modes:** full, no-delete

---

### `delete_memory`

Permanently deletes a memory entry.

| Parameter | Required | Description |
|-----------|----------|-------------|
| `category` | yes* | Category of the entry to delete |
| `key` | yes | Key of the entry to delete |

*Hidden when const-category is set.

**Available in modes:** full

---

### `get_memory`

Retrieves a specific memory entry by its category and key.

| Parameter | Required | Description |
|-----------|----------|-------------|
| `category` | yes* | Category of the entry |
| `key` | yes | Key of the entry to retrieve |

*Hidden when const-category is set.

**Available in modes:** all

---

### `list_memories`

Lists memory entries with optional filtering, ordered by last modified date (newest first).

| Parameter | Required | Description |
|-----------|----------|-------------|
| `category` | no* | Filter by category |
| `top` | no | Maximum number of results (default: 50) |
| `author` | no | Filter by author |
| `tags` | no | Filter by tags (comma-separated, matches any) |
| `before` | no | Only entries before this ISO 8601 timestamp |
| `after` | no | Only entries after this ISO 8601 timestamp |

*Hidden when const-category is set.

**Available in modes:** all

---

### `search_memories`

Searches for memories using hybrid semantic + keyword search. Uses natural language understanding -- exact keyword matches are not required.

| Parameter | Required | Description |
|-----------|----------|-------------|
| `query` | yes | Natural language search query |
| `category` | no* | Filter by category |
| `top` | no | Maximum number of results (default: 10) |
| `author` | no | Filter by author |
| `tags` | no | Filter by tags (comma-separated, matches any) |

*Hidden when const-category is set.

**Available in modes:** all

---

## Tool Availability by Access Mode

| Tool | full | read-only | append-only | no-delete |
|------|------|-----------|-------------|-----------|
| `create_memory` | yes | -- | yes | yes |
| `edit_memory` | yes | -- | -- | yes |
| `delete_memory` | yes | -- | -- | -- |
| `get_memory` | yes | yes | yes | yes |
| `list_memories` | yes | yes | yes | yes |
| `search_memories` | yes | yes | yes | yes |

---

## Concurrent Access

Multiple processes can access the same database simultaneously. SQLite WAL mode is enabled by default:

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
