using System.Text.Json;
using Microsoft.Data.Sqlite;
using Zakira.Exchange.Core.Models;

namespace Zakira.Exchange.Core.Storage;

/// <summary>
/// SQLite-based storage for memory entries with FTS5 full-text search support.
/// Thread-safe: uses connection-per-operation against the built-in ADO.NET pool,
/// so multiple readers and writers can call the public API concurrently. SQLite
/// WAL mode (enabled at construction) lets concurrent readers run without
/// blocking the single in-flight writer; a per-connection busy_timeout gives
/// contending writers a bounded wait instead of an immediate SQLITE_BUSY error.
/// </summary>
public sealed class MemoryStore : IDisposable
{
    private readonly string _connectionString;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public MemoryStore(string databasePath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString();

        // One-time init on a single connection that is then returned to the pool.
        // WAL mode is a file-level setting that persists across connections.
        // synchronous=NORMAL and busy_timeout are per-connection; they are re-applied
        // by OpenConnection() so fresh pool entries also use them.
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        Execute(conn, "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=5000;");
        InitializeSchema(conn);
    }

    /// <summary>
    /// Opens a fresh pooled connection. Per-call cost is sub-millisecond on warm
    /// pools. Per-connection PRAGMAs are re-applied so fresh pool entries match.
    /// </summary>
    private SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        Execute(conn, "PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=5000;");
        return conn;
    }

    private static void Execute(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static void InitializeSchema(SqliteConnection conn)
    {
        Execute(conn, """
            CREATE TABLE IF NOT EXISTS memories (
                category         TEXT NOT NULL,
                key              TEXT NOT NULL,
                data             TEXT NOT NULL,
                author           TEXT,
                reason           TEXT,
                tags             TEXT,
                custom           TEXT,
                created_at       TEXT NOT NULL,
                last_modified_at TEXT NOT NULL,
                embedding        BLOB,
                PRIMARY KEY (category, key)
            );
            """);

        Execute(conn, """
            CREATE VIRTUAL TABLE IF NOT EXISTS memories_fts USING fts5(
                category, key, data, author, reason, tags,
                content='memories',
                content_rowid='rowid'
            );
            """);

        // Triggers to keep FTS5 in sync with the main table
        Execute(conn, """
            CREATE TRIGGER IF NOT EXISTS memories_ai AFTER INSERT ON memories BEGIN
                INSERT INTO memories_fts(rowid, category, key, data, author, reason, tags)
                VALUES (new.rowid, new.category, new.key, new.data, new.author, new.reason, new.tags);
            END;
            """);

        Execute(conn, """
            CREATE TRIGGER IF NOT EXISTS memories_ad AFTER DELETE ON memories BEGIN
                INSERT INTO memories_fts(memories_fts, rowid, category, key, data, author, reason, tags)
                VALUES ('delete', old.rowid, old.category, old.key, old.data, old.author, old.reason, old.tags);
            END;
            """);

        Execute(conn, """
            CREATE TRIGGER IF NOT EXISTS memories_au AFTER UPDATE ON memories BEGIN
                INSERT INTO memories_fts(memories_fts, rowid, category, key, data, author, reason, tags)
                VALUES ('delete', old.rowid, old.category, old.key, old.data, old.author, old.reason, old.tags);
                INSERT INTO memories_fts(rowid, category, key, data, author, reason, tags)
                VALUES (new.rowid, new.category, new.key, new.data, new.author, new.reason, new.tags);
            END;
            """);

        // Indexes for timestamp-based and category queries
        Execute(conn, "CREATE INDEX IF NOT EXISTS idx_memories_modified ON memories(last_modified_at);");
        Execute(conn, "CREATE INDEX IF NOT EXISTS idx_memories_created ON memories(created_at);");
        Execute(conn, "CREATE INDEX IF NOT EXISTS idx_memories_category ON memories(category);");
    }

    /// <summary>
    /// Creates a new memory entry. Throws if an entry with the same (category, key) already exists.
    /// </summary>
    public void Create(MemoryEntry entry, float[]? embedding)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO memories (category, key, data, author, reason, tags, custom, created_at, last_modified_at, embedding)
            VALUES (@category, @key, @data, @author, @reason, @tags, @custom, @createdAt, @lastModifiedAt, @embedding);
            """;

        BindEntryParameters(cmd, entry, embedding);

        try
        {
            cmd.ExecuteNonQuery();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19) // SQLITE_CONSTRAINT
        {
            throw new InvalidOperationException($"A memory entry with category '{entry.Category}' and key '{entry.Key}' already exists.", ex);
        }
    }

    /// <summary>
    /// Updates an existing memory entry. Returns false if not found.
    /// </summary>
    public bool Update(MemoryEntry entry, float[]? embedding)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE memories
            SET data = @data,
                author = @author,
                reason = @reason,
                tags = @tags,
                custom = @custom,
                last_modified_at = @lastModifiedAt,
                embedding = @embedding
            WHERE category = @category AND key = @key;
            """;

        BindEntryParameters(cmd, entry, embedding);
        // Don't update created_at
        return cmd.ExecuteNonQuery() > 0;
    }

    /// <summary>
    /// Deletes a memory entry. Returns false if not found.
    /// </summary>
    public bool Delete(string category, string key)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM memories WHERE category = @category AND key = @key;";
        cmd.Parameters.AddWithValue("@category", category);
        cmd.Parameters.AddWithValue("@key", key);
        return cmd.ExecuteNonQuery() > 0;
    }

    /// <summary>
    /// Gets a single memory entry by (category, key). Returns null if not found.
    /// </summary>
    public MemoryEntry? Get(string category, string key)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT category, key, data, author, reason, tags, custom, created_at, last_modified_at
            FROM memories
            WHERE category = @category AND key = @key;
            """;
        cmd.Parameters.AddWithValue("@category", category);
        cmd.Parameters.AddWithValue("@key", key);

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadEntry(reader) : null;
    }

    /// <summary>
    /// Lists memory entries with filtering.
    /// </summary>
    public List<MemoryEntry> List(ListFilter filter)
    {
        var conditions = new List<string>();
        var parameters = new List<SqliteParameter>();

        if (filter.Category is not null)
        {
            conditions.Add("category = @category");
            parameters.Add(new SqliteParameter("@category", filter.Category));
        }

        if (filter.Author is not null)
        {
            conditions.Add("author = @author");
            parameters.Add(new SqliteParameter("@author", filter.Author));
        }

        if (filter.Tags is { Count: > 0 })
        {
            // Match entries that have ANY of the specified tags
            var tagConditions = new List<string>();
            for (var i = 0; i < filter.Tags.Count; i++)
            {
                tagConditions.Add($"tags LIKE @tag{i}");
                parameters.Add(new SqliteParameter($"@tag{i}", $"%\"{filter.Tags[i]}\"%"));
            }
            conditions.Add($"({string.Join(" OR ", tagConditions)})");
        }

        if (filter.Before is not null)
        {
            conditions.Add("last_modified_at < @before");
            parameters.Add(new SqliteParameter("@before", filter.Before.Value.UtcDateTime.ToString("O")));
        }

        if (filter.After is not null)
        {
            conditions.Add("last_modified_at > @after");
            parameters.Add(new SqliteParameter("@after", filter.After.Value.UtcDateTime.ToString("O")));
        }

        var whereClause = conditions.Count > 0
            ? "WHERE " + string.Join(" AND ", conditions)
            : "";

        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT category, key, data, author, reason, tags, custom, created_at, last_modified_at
            FROM memories
            {whereClause}
            ORDER BY last_modified_at DESC
            LIMIT @top;
            """;

        cmd.Parameters.AddWithValue("@top", filter.Top);
        foreach (var p in parameters)
        {
            cmd.Parameters.Add(p);
        }

        var results = new List<MemoryEntry>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(ReadEntry(reader));
        }
        return results;
    }

    /// <summary>
    /// Performs FTS5 BM25 keyword search. Returns (category, key, bm25Score) tuples ranked by relevance.
    /// </summary>
    public List<(string Category, string Key, double Bm25Score)> KeywordSearch(string query, string? category, int overFetchMultiplier = 3)
    {
        // Escape FTS5 special characters and format as OR-connected tokens
        var sanitized = SanitizeFtsQuery(query);
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return [];
        }

        var categoryFilter = category is not null
            ? "AND m.category = @category"
            : "";

        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT m.category, m.key, bm25(memories_fts) as score
            FROM memories_fts
            JOIN memories m ON memories_fts.rowid = m.rowid
            WHERE memories_fts MATCH @query
            {categoryFilter}
            ORDER BY score
            LIMIT @limit;
            """;

        cmd.Parameters.AddWithValue("@query", sanitized);
        cmd.Parameters.AddWithValue("@limit", 50 * overFetchMultiplier);

        if (category is not null)
        {
            cmd.Parameters.AddWithValue("@category", category);
        }

        var results = new List<(string, string, double)>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add((
                reader.GetString(0),
                reader.GetString(1),
                reader.GetDouble(2)
            ));
        }
        return results;
    }

    /// <summary>
    /// Gets all embeddings, optionally filtered by category. Returns (category, key, embedding) tuples.
    /// </summary>
    public List<(string Category, string Key, float[] Embedding)> GetAllEmbeddings(string? category)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();

        if (category is not null)
        {
            cmd.CommandText = "SELECT category, key, embedding FROM memories WHERE embedding IS NOT NULL AND category = @category;";
            cmd.Parameters.AddWithValue("@category", category);
        }
        else
        {
            cmd.CommandText = "SELECT category, key, embedding FROM memories WHERE embedding IS NOT NULL;";
        }

        var results = new List<(string, string, float[])>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var embeddingBlob = (byte[])reader["embedding"];
            var embedding = new float[embeddingBlob.Length / sizeof(float)];
            Buffer.BlockCopy(embeddingBlob, 0, embedding, 0, embeddingBlob.Length);
            results.Add((reader.GetString(0), reader.GetString(1), embedding));
        }
        return results;
    }

    /// <summary>
    /// Gets the list of distinct categories.
    /// </summary>
    public List<string> GetCategories()
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT category FROM memories ORDER BY category;";
        var results = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(reader.GetString(0));
        }
        return results;
    }

    /// <summary>
    /// Gets the total count of entries, optionally filtered by category.
    /// </summary>
    public long GetCount(string? category = null)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        if (category is not null)
        {
            cmd.CommandText = "SELECT COUNT(*) FROM memories WHERE category = @category;";
            cmd.Parameters.AddWithValue("@category", category);
        }
        else
        {
            cmd.CommandText = "SELECT COUNT(*) FROM memories;";
        }
        return (long)cmd.ExecuteScalar()!;
    }

    private static void BindEntryParameters(SqliteCommand cmd, MemoryEntry entry, float[]? embedding)
    {
        cmd.Parameters.AddWithValue("@category", entry.Category);
        cmd.Parameters.AddWithValue("@key", entry.Key);
        cmd.Parameters.AddWithValue("@data", entry.Data);
        cmd.Parameters.AddWithValue("@author", (object?)entry.Metadata.Author ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@reason", (object?)entry.Metadata.Reason ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@tags", entry.Metadata.Tags.Count > 0
            ? JsonSerializer.Serialize(entry.Metadata.Tags, JsonOptions)
            : DBNull.Value);
        cmd.Parameters.AddWithValue("@custom", entry.Metadata.Custom.Count > 0
            ? JsonSerializer.Serialize(entry.Metadata.Custom, JsonOptions)
            : DBNull.Value);
        cmd.Parameters.AddWithValue("@createdAt", entry.Metadata.CreatedAt.UtcDateTime.ToString("O"));
        cmd.Parameters.AddWithValue("@lastModifiedAt", entry.Metadata.LastModifiedAt.UtcDateTime.ToString("O"));

        if (embedding is not null)
        {
            var blob = new byte[embedding.Length * sizeof(float)];
            Buffer.BlockCopy(embedding, 0, blob, 0, blob.Length);
            cmd.Parameters.AddWithValue("@embedding", blob);
        }
        else
        {
            cmd.Parameters.AddWithValue("@embedding", DBNull.Value);
        }
    }

    private static MemoryEntry ReadEntry(SqliteDataReader reader)
    {
        var tagsJson = reader.IsDBNull(5) ? null : reader.GetString(5);
        var customJson = reader.IsDBNull(6) ? null : reader.GetString(6);

        return new MemoryEntry
        {
            Category = reader.GetString(0),
            Key = reader.GetString(1),
            Data = reader.GetString(2),
            Metadata = new MemoryMetadata
            {
                Author = reader.IsDBNull(3) ? null : reader.GetString(3),
                Reason = reader.IsDBNull(4) ? null : reader.GetString(4),
                Tags = tagsJson is not null
                    ? JsonSerializer.Deserialize<List<string>>(tagsJson, JsonOptions) ?? []
                    : [],
                Custom = customJson is not null
                    ? JsonSerializer.Deserialize<Dictionary<string, string>>(customJson, JsonOptions) ?? []
                    : [],
                CreatedAt = DateTimeOffset.Parse(reader.GetString(7)),
                LastModifiedAt = DateTimeOffset.Parse(reader.GetString(8)),
            }
        };
    }

    /// <summary>
    /// Sanitizes a user query for FTS5 MATCH syntax.
    /// Splits into tokens and joins with OR for broad matching.
    /// </summary>
    private static string SanitizeFtsQuery(string query)
    {
        // Remove FTS5 special characters, keep alphanumeric and spaces
        var tokens = query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => new string(t.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray()))
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();

        if (tokens.Count == 0)
        {
            return "";
        }

        // Use OR to broaden matching
        return string.Join(" OR ", tokens);
    }

    public void Dispose()
    {
        // Release any pooled connections for this database so its files
        // can be deleted or moved by callers (e.g., test cleanup).
        using var conn = new SqliteConnection(_connectionString);
        SqliteConnection.ClearPool(conn);
    }
}
