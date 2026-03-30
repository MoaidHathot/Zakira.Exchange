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

SQLite FTS5 performs full-text search with BM25 scoring against the entry's concatenated fields:
- Key
- Data
- Tags
- Reason

BM25 is a probabilistic ranking function that scores documents based on term frequency, inverse document frequency, and document length normalization.

### Step 2: Vector Search (Cosine Similarity)

The query is embedded using the `all-MiniLM-L6-v2` ONNX model:

1. **Tokenization:** The query is tokenized using a custom WordPiece tokenizer with BERT-style `[CLS]` and `[SEP]` tokens
2. **Inference:** The ONNX model produces token-level embeddings
3. **Mean pooling:** Token embeddings are averaged (with attention mask) into a single 384-dimensional sentence embedding
4. **L2 normalization:** The embedding is normalized so cosine similarity equals dot product
5. **Comparison:** The query embedding is compared against all stored entry embeddings using dot product (brute-force search)

Each memory entry is embedded as a single concatenated string: `key + data + tags + reason`. No chunking is needed since memories are typically short structured entries.

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
| `Embedding` | float[] | 384-dim vector embedding |

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

Each category gets its own table. The storage layer creates:

1. **Main table** (`memories_{category}`) -- stores entries with their data, metadata (JSON), and embedding (BLOB)
2. **FTS5 virtual table** (`memories_{category}_fts`) -- full-text search index over key, data, tags, and reason
3. **Triggers** -- keep the FTS index synchronized with the main table on insert, update, and delete

WAL (Write-Ahead Logging) mode is enabled on the database connection for concurrent read/write access.

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
