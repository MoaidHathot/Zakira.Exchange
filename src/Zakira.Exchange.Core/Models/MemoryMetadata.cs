using System.Text.Json.Serialization;

namespace Zakira.Exchange.Core.Models;

/// <summary>
/// Metadata associated with a memory entry.
/// </summary>
public sealed class MemoryMetadata
{
    /// <summary>
    /// Who or what created/owns this entry (e.g. agent name, user name).
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Why this entry was created or last modified.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Tags for categorization and filtering.
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// Extensible key-value pairs for custom metadata.
    /// </summary>
    public Dictionary<string, string> Custom { get; set; } = [];

    /// <summary>
    /// When this entry was first created (UTC). Auto-managed.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// When this entry was last modified (UTC). Auto-managed.
    /// </summary>
    public DateTimeOffset LastModifiedAt { get; set; }
}
