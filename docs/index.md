---
layout: default
title: Home
nav_order: 1
---

# Zakira.Exchange

**AI Agent Memory Storage & Semantic Search**

An MCP server and CLI tool that enables AI agents to save, share, and search memories. Built with .NET 10, standalone with no external service dependencies.

Memories are stored in SQLite with full-text search (FTS5) and semantic vector search (ONNX embeddings), merged via Reciprocal Rank Fusion for hybrid retrieval.

---

## Features

- **Structured memory entries** with category, key, data, and rich metadata (author, reason, tags, custom key-value pairs, auto-managed timestamps)
- **Categories/tables** to separate different types of memories
- **Full CRUD** -- create, edit, delete, get, list entries
- **Hybrid search (RAG)** -- BM25 keyword search + cosine vector similarity via `all-MiniLM-L6-v2` ONNX embeddings, fused with Reciprocal Rank Fusion (k=60)
- **Access mode control** -- restrict which operations agents can perform (full, read-only, append-only, no-delete)
- **Const-category mode** -- lock all operations to a single category, hiding the parameter from agents
- **Single tool, two modes** -- use `zakira` as a CLI tool, or `zakira mcp` to start as an MCP server (stdio transport)
- **Concurrent access** -- multiple processes can access the same database simultaneously via SQLite WAL mode
- **Lazy ONNX model loading** -- the model is only loaded when create, edit, or search operations are invoked
- **Environment variable configuration** -- all CLI flags have corresponding environment variables

---

## How It Works

```
 AI Agent
    |
    v
 MCP Server (stdio)          CLI Tool
    |                           |
    v                           v
 MemoryService (CRUD + Search)
    |
    +-- SQLite (FTS5 + WAL mode)
    |
    +-- ONNX Runtime (all-MiniLM-L6-v2 embeddings)
    |
    +-- Hybrid Search Engine (BM25 + Vector, merged via RRF)
```

1. Memories are stored as structured entries with rich metadata
2. On create/edit, entries are embedded using `all-MiniLM-L6-v2` (384-dim vectors)
3. On search, the query is embedded and compared against stored vectors (cosine similarity) while simultaneously performing BM25 keyword search
4. Results from both search methods are merged using Reciprocal Rank Fusion (k=60) for best-of-both-worlds retrieval

---

## Quick Links

- [Getting Started](getting-started) -- Prerequisites, download, build, and first use
- [CLI Reference](cli-reference) -- Complete command-line interface documentation
- [MCP Server](mcp-server) -- Setting up Zakira as an MCP server for AI agents
- [Configuration](configuration) -- CLI flags, environment variables, and access modes
- [Architecture](architecture) -- Internals, search algorithm, and project structure

---

## License

[Unlicense](https://github.com/MoaidHathot/Zakira.Exchange/blob/main/LICENSE) -- public domain.
