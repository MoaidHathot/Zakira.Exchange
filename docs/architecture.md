---
layout: default
title: Architecture
nav_order: 6
---

# Architecture

## Project Structure

Zakira consists of two .NET projects:

```
Zakira.Exchange.slnx
  |
  +-- src/Zakira.Exchange.Core/      Core library (business logic, storage, search)
  |     |
  |     +-- Configuration/
  |     |     +-- AccessMode.cs       Access mode enum and permission helpers
  |     |     +-- ZakiraOptions.cs    Configuration options (db path, mode, category, model)
  |     |
  |     +-- Models/
  |     |     +-- MemoryEntry.cs      Memory entry data model
  |     |     +-- MemoryMetadata.cs   Metadata (author, reason, tags, custom, timestamps)
  |     |     +-- ListFilter.cs       List filtering parameters
  |     |     +-- SearchFilter.cs     Search filtering parameters
  |     |     +-- SearchResult.cs     Search result with relevance score
  |     |
  |     +-- Search/
  |     |     +-- EmbeddingService.cs       ONNX model inference (384-dim embeddings)
  |     |     +-- HybridSearchEngine.cs     BM25 + vector search merged via RRF
  |     |     +-- WordPieceTokenizer.cs     Minimal WordPiece tokenizer for BERT
  |     |
  |     +-- Services/
  |     |     +-- MemoryService.cs    Main orchestration service (CRUD + search)
  |     |
  |     +-- Storage/
  |           +-- MemoryStore.cs      SQLite storage with FTS5 and WAL mode
  |
  +-- src/Zakira.Exchange.Cli/       CLI entry point and MCP server
        |
        +-- Program.cs              CLI command definitions and entry point
        +-- Tools/
              +-- ToolBuilder.cs     Dynamic MCP tool builder based on access mode
```

---

## Technology Stack

| Component | Technology | Purpose |
|-----------|-----------|---------|
| Runtime | .NET 10 | Application framework |
| Language | C# (latest) | Programming language |
| Storage | SQLite via `Microsoft.Data.Sqlite` | Persistent data storage |
| Full-text search | SQLite FTS5 | BM25 keyword search |
| Concurrency | SQLite WAL mode | Multi-process concurrent access |
| Embeddings | ONNX Runtime (`Microsoft.ML.OnnxRuntime`) | Neural network inference |
| Model | `all-MiniLM-L6-v2` (HuggingFace) | Sentence transformer (384-dim) |
| Tokenizer | Custom WordPiece implementation | BERT-style text tokenization |
| CLI | `System.CommandLine` | Command-line argument parsing |
| MCP | `ModelContextProtocol` | MCP server protocol (stdio) |
| Hosting | `Microsoft.Extensions.Hosting` | Host builder for MCP server |

---

## How Search Works

The search engine uses a hybrid approach combining two ranking methods and merging them with Reciprocal Rank Fusion.

### Step 1: Keyword Search (BM25)

SQLite FTS5 performs full-text search with BM25 scoring against the entry's indexed fields:
- Category
- Key
- Data
- Author
- Reason
- Tags

BM25 is a probabilistic ranking function that scores documents based on term frequency, inverse document frequency, and document length normalization.

How the query's tokens combine into an FTS5 expression depends on the requested **search mode**:

| Mode | FTS5 expression | When to use |
|------|-----------------|-------------|
| `any` (default) | `"tok1" OR "tok2" OR ...` | Broadest recall; matches entries with any token |
| `all` | `"tok1" AND "tok2" AND ...` | Stricter precision; every token must appear |
| `phrase` | `"tok1 tok2 ..."` (single phrase string) | Strictest; tokens must appear in the exact order, contiguously |

Tokens are sanitized (alphanumeric and underscore only) and each token is wrapped in FTS5 string-literal syntax (double quotes), so user queries containing FTS5 metacharacters never cause syntax errors. Search mode is passed through `SearchFilter.Mode` and exposed by both the MCP `search_memories` tool and the `--mode` CLI flag. The vector (semantic) portion of the hybrid search is unaffected by mode and always runs.

### Step 2: Vector Search (Cosine Similarity)

The query is embedded using the `all-MiniLM-L6-v2` ONNX model:

1. **Tokenization:** The query is tokenized using a custom WordPiece tokenizer with BERT-style `[CLS]` and `[SEP]` tokens
2. **Inference:** The ONNX model produces token-level embeddings
3. **Mean pooling:** Token embeddings are averaged (with attention mask) into a single 384-dimensional sentence embedding
4. **L2 normalization:** The embedding is normalized so cosine similarity equals dot product
5. **Comparison:** The query embedding is compared against all stored entry embeddings using dot product (brute-force search)

Each memory entry is embedded as a single concatenated string: `key | data | tags | reason` (joined with ` | `). No chunking is needed since memories are typically short structured entries.

### Step 3: Reciprocal Rank Fusion (RRF)

Both ranked lists are merged using RRF with k=60:

```
RRF_score(entry) = 1/(k + rank_bm25) + 1/(k + rank_vector)
```

Where:
- `k = 60` (a standard constant that controls the impact of high vs. low ranks)
- `rank_bm25` is the entry's position in the BM25-ranked results
- `rank_vector` is the entry's position in the vector-ranked results

RRF is robust because it only considers rank positions, not raw scores, making it immune to score distribution differences between the two methods.

### Step 4: Post-Filtering

After fusion, author and tag filters are applied to the merged result set. This ensures filtering does not interfere with the ranking process.

---

## Data Model

### MemoryEntry

| Field | Type | Description |
|-------|------|-------------|
| `Category` | string | Category/table namespace |
| `Key` | string | Unique key within the category |
| `Data` | string | The memory content (text) |
| `Metadata` | MemoryMetadata | Rich metadata object |

> **Note:** Embeddings (384-dim float arrays) are stored in the SQLite database alongside each entry but are not exposed as a property on the `MemoryEntry` model. They are managed internally by the storage and search layers.

### MemoryMetadata

| Field | Type | Description |
|-------|------|-------------|
| `Author` | string? | Who/what created or last modified this |
| `Reason` | string? | Why this was created or modified |
| `Tags` | List\<string\> | Tags for categorization |
| `Custom` | Dictionary\<string, string\> | Arbitrary key-value metadata |
| `CreatedAt` | DateTimeOffset | Auto-set on creation |
| `LastModifiedAt` | DateTimeOffset | Auto-updated on every modification |

---

## SQLite Schema

All entries are stored in a single `memories` table with category as a column. The storage layer creates:

1. **Main table** (`memories`) -- stores entries with their data, metadata (JSON), and embedding (BLOB), with a composite primary key of (category, key)
2. **FTS5 virtual table** (`memories_fts`) -- full-text search index over category, key, data, author, reason, and tags
3. **Triggers** -- keep the FTS index synchronized with the main table on insert, update, and delete
4. **Indexes** -- on last_modified_at, created_at, and category for efficient filtering

WAL (Write-Ahead Logging) mode is enabled on the database connection for concurrent read/write access.

---

## Concurrency and Thread Safety

`MemoryStore` is thread-safe. Internally it holds only a connection **string**, not a live connection; every public method opens its own connection from the `Microsoft.Data.Sqlite` ADO.NET pool, executes its statements, and returns the connection to the pool on dispose. This means:

- Multiple threads may call any combination of `Create`, `Update`, `Delete`, `Get`, `List`, `KeywordSearch`, etc. concurrently without external synchronization.
- WAL mode lets concurrent **readers** run in parallel with the single in-flight **writer** without blocking each other.
- A per-connection `busy_timeout=5000` gives contending writers a bounded wait (up to 5 seconds) instead of an immediate `SQLITE_BUSY` error.

`MemoryService` is also safe to register as a singleton in an MCP / DI host: it delegates all state to `MemoryStore`, and the lazily-initialised embedding service is guarded by a lock.

---

## Optimistic Concurrency on Edit

Two agents (or two instances of the same agent) may try to edit the same `(category, key)` at the same time. By default both edits succeed and the second silently overwrites the first (last-write-wins). When that's not acceptable, callers can opt into **optimistic concurrency**:

- `MemoryStore.Update(entry, embedding, expectedLastModifiedAt?)` returns an `UpdateOutcome` of `Updated`, `NotFound`, or `Conflict`. Pass the row's `last_modified_at` value as it was when you read it; the UPDATE's WHERE clause includes `AND last_modified_at = @expected`, so a racing writer that has since changed the row will cause this call to fail with `Conflict` rather than overwrite their change.
- `MemoryService.EditWithConcurrency(...)` is the same idea at the service layer, returning an `EditResult` with the conflict's current `LastModifiedAt` so the caller can re-fetch, merge, and retry.
- The MCP `edit_memory` tool exposes `expectedLastModifiedAt`. On conflict it returns a human-readable message including the current timestamp.
- The CLI `edit` command exposes `--expected-modified` and exits with **code 2** on conflict (distinct from code 1 for not-found), so scripts can detect and retry.

Backward compatibility: the original `bool Update(entry, embedding)` and `MemoryEntry? Edit(...)` overloads remain and still mean "last-write-wins". They simply delegate to the new overloads with `expectedLastModifiedAt: null`.

---

## Lazy Loading

The ONNX model (~90 MB) is loaded lazily on first use. Operations that do not require embeddings work without the model:

| Operation | Requires Model |
|-----------|---------------|
| `create` | yes (generates embedding) |
| `edit` | yes (regenerates embedding) |
| `search` | yes (embeds query) |
| `get` | no |
| `list` | no |
| `delete` | no |
| `categories` | no |

This means you can list, get, and delete entries even if the model files are not present.
