namespace Zakira.Exchange.Core.Models;

/// <summary>
/// Filters for listing memory entries.
/// </summary>
public sealed class ListFilter
{
    /// <summary>
    /// Filter by category. Null means all categories.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Maximum number of results to return. Default is 50.
    /// </summary>
    public int Top { get; set; } = 50;

    /// <summary>
    /// Filter by author.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Filter by tags (any match). Comma-separated in tool input, parsed to list.
    /// </summary>
    public List<string>? Tags { get; set; }

    /// <summary>
    /// Only return entries created/modified before this timestamp.
    /// </summary>
    public DateTimeOffset? Before { get; set; }

    /// <summary>
    /// Only return entries created/modified after this timestamp.
    /// </summary>
    public DateTimeOffset? After { get; set; }
}
