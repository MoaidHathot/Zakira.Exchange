namespace Zakira.Exchange.Core.Models;

/// <summary>
/// Represents a single memory entry stored in Zakira.Exchange.
/// The primary key is the composite of (Category, Key).
/// </summary>
public sealed class MemoryEntry
{
    /// <summary>
    /// Category (table/namespace) that groups related memories together.
    /// </summary>
    public required string Category { get; set; }

    /// <summary>
    /// Unique identifier within the category.
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// The memory content (text data).
    /// </summary>
    public required string Data { get; set; }

    /// <summary>
    /// Structured metadata about this memory entry.
    /// </summary>
    public MemoryMetadata Metadata { get; set; } = new();
}
