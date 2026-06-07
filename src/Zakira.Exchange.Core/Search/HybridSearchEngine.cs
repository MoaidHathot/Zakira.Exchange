using Zakira.Exchange.Core.Models;
using Zakira.Exchange.Core.Storage;

namespace Zakira.Exchange.Core.Search;

/// <summary>
/// Hybrid search engine that combines FTS5 BM25 keyword search with
/// cosine vector similarity, merged via Reciprocal Rank Fusion (RRF).
/// </summary>
public sealed class HybridSearchEngine
{
    private readonly MemoryStore _store;
    private readonly EmbeddingService _embeddingService;

    /// <summary>
    /// RRF smoothing constant. k=60 from the original RRF paper.
    /// Higher values reduce the advantage of top-ranked items.
    /// </summary>
    private const int RrfK = 60;

    public HybridSearchEngine(MemoryStore store, EmbeddingService embeddingService)
    {
        _store = store;
        _embeddingService = embeddingService;
    }

    /// <summary>
    /// Performs hybrid search: BM25 keyword + vector cosine similarity merged via RRF.
    /// Returns top K results ordered by combined relevance.
    /// </summary>
    public List<SearchResult> Search(SearchFilter filter)
    {
        var queryEmbedding = _embeddingService.Embed(filter.Query);

        // 1. Keyword search via FTS5 BM25
        var keywordResults = _store.KeywordSearch(filter.Query, filter.Category, filter.Mode);

        // 2. Vector search via brute-force cosine similarity (dot product on normalized vectors)
        var vectorResults = VectorSearch(queryEmbedding, filter.Category);

        // 3. Merge via Reciprocal Rank Fusion
        var merged = ReciprocalRankFusion(keywordResults, vectorResults);

        // 4. Apply additional filters (author, tags)
        var filtered = ApplyFilters(merged, filter);

        // 5. Fetch full entries for top K results
        var results = new List<SearchResult>();
        foreach (var (category, key, score) in filtered.Take(filter.Top))
        {
            var entry = _store.Get(category, key);
            if (entry is not null)
            {
                results.Add(new SearchResult { Entry = entry, Score = score });
            }
        }

        return results;
    }

    /// <summary>
    /// Computes cosine similarity (dot product on L2-normalized vectors) against all stored embeddings.
    /// Returns results ranked by similarity (descending).
    /// </summary>
    private List<(string Category, string Key, double Score)> VectorSearch(float[] queryEmbedding, string? category)
    {
        var allEmbeddings = _store.GetAllEmbeddings(category);
        var scored = new List<(string Category, string Key, double Score)>(allEmbeddings.Count);

        foreach (var (cat, key, embedding) in allEmbeddings)
        {
            var similarity = DotProduct(queryEmbedding, embedding);
            scored.Add((cat, key, similarity));
        }

        // Sort descending by similarity
        scored.Sort((a, b) => b.Score.CompareTo(a.Score));
        return scored;
    }

    /// <summary>
    /// Merges two ranked lists using Reciprocal Rank Fusion.
    /// score(item) = 1/(k + rank_keyword) + 1/(k + rank_vector)
    /// </summary>
    private static List<(string Category, string Key, double Score)> ReciprocalRankFusion(
        List<(string Category, string Key, double Bm25Score)> keywordResults,
        List<(string Category, string Key, double Score)> vectorResults)
    {
        var scores = new Dictionary<(string Category, string Key), double>();

        // Add keyword search contributions
        for (var rank = 0; rank < keywordResults.Count; rank++)
        {
            var item = keywordResults[rank];
            var compositeKey = (item.Category, item.Key);
            var rrfScore = 1.0 / (RrfK + rank + 1);
            scores[compositeKey] = scores.GetValueOrDefault(compositeKey, 0) + rrfScore;
        }

        // Add vector search contributions
        for (var rank = 0; rank < vectorResults.Count; rank++)
        {
            var item = vectorResults[rank];
            var compositeKey = (item.Category, item.Key);
            var rrfScore = 1.0 / (RrfK + rank + 1);
            scores[compositeKey] = scores.GetValueOrDefault(compositeKey, 0) + rrfScore;
        }

        // Sort by combined RRF score descending
        var merged = scores
            .Select(kvp => (kvp.Key.Category, kvp.Key.Key, kvp.Value))
            .OrderByDescending(x => x.Value)
            .ToList();

        return merged;
    }

    /// <summary>
    /// Applies author and tag filters to the merged results.
    /// These are applied post-RRF because they are exact-match filters.
    /// </summary>
    private List<(string Category, string Key, double Score)> ApplyFilters(
        List<(string Category, string Key, double Score)> results,
        SearchFilter filter)
    {
        if (filter.Author is null && (filter.Tags is null || filter.Tags.Count == 0))
        {
            return results;
        }

        var filtered = new List<(string Category, string Key, double Score)>();
        foreach (var result in results)
        {
            var entry = _store.Get(result.Category, result.Key);
            if (entry is null)
            {
                continue;
            }

            if (filter.Author is not null && !string.Equals(entry.Metadata.Author, filter.Author, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (filter.Tags is { Count: > 0 })
            {
                var hasMatch = filter.Tags.Any(ft =>
                    entry.Metadata.Tags.Any(et => string.Equals(et, ft, StringComparison.OrdinalIgnoreCase)));

                if (!hasMatch)
                {
                    continue;
                }
            }

            filtered.Add(result);
        }

        return filtered;
    }

    /// <summary>
    /// Dot product of two vectors. Since both are L2-normalized, this equals cosine similarity.
    /// </summary>
    private static double DotProduct(float[] a, float[] b)
    {
        var sum = 0.0;
        var length = Math.Min(a.Length, b.Length);
        for (var i = 0; i < length; i++)
        {
            sum += a[i] * (double)b[i];
        }
        return sum;
    }
}
