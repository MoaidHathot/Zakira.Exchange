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

    /// <summary>
    /// How the query's tokens are combined for the FTS5 (keyword) portion of the
    /// hybrid search. Defaults to <see cref="SearchMode.Any"/> for broad recall;
    /// callers can pick <see cref="SearchMode.All"/> for stricter AND-of-tokens
    /// or <see cref="SearchMode.Phrase"/> for an exact phrase match. The vector
    /// (semantic) portion is unaffected and always runs.
    /// </summary>
    public SearchMode Mode { get; set; } = SearchMode.Any;
}
