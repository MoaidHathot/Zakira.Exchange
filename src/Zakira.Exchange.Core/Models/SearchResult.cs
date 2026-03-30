namespace Zakira.Exchange.Core.Models;

/// <summary>
/// A search result containing a memory entry and its relevance score.
/// </summary>
public sealed class SearchResult
{
    /// <summary>
    /// The matched memory entry.
    /// </summary>
    public required MemoryEntry Entry { get; set; }

    /// <summary>
    /// Relevance score (higher is more relevant). Computed via RRF merging of BM25 and vector scores.
    /// </summary>
    public double Score { get; set; }
}
