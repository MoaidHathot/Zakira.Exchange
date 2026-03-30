namespace Zakira.Exchange.Core.Models;

/// <summary>
/// Filters for searching memory entries (semantic + keyword hybrid search).
/// </summary>
public sealed class SearchFilter
{
    /// <summary>
    /// The search query text (required).
    /// </summary>
    public required string Query { get; set; }

    /// <summary>
    /// Filter by category. Null means all categories.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Maximum number of results to return. Default is 10.
    /// </summary>
    public int Top { get; set; } = 10;

    /// <summary>
    /// Filter by author.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Filter by tags (any match).
    /// </summary>
    public List<string>? Tags { get; set; }
}
