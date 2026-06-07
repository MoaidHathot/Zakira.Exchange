namespace Zakira.Exchange.Core.Models;

/// <summary>
/// How a search query's tokens are combined when running the FTS5 portion of the
/// hybrid search.
/// </summary>
public enum SearchMode
{
    /// <summary>
    /// Match entries containing ANY of the query tokens. Default; broadest recall.
    /// </summary>
    Any,

    /// <summary>
    /// Match entries containing ALL of the query tokens (in any order).
    /// Use when you want stricter precision.
    /// </summary>
    All,

    /// <summary>
    /// Match entries containing the query as a single contiguous phrase
    /// (tokens in the exact order given). Strictest mode.
    /// </summary>
    Phrase,
}
