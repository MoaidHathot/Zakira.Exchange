using System.Reflection;
using Zakira.Exchange.Core.Models;
using Zakira.Exchange.Core.Search;

namespace Zakira.Exchange.Tests.Search;

/// <summary>
/// Tests for HybridSearchEngine's internal algorithms.
/// Uses reflection to test private static methods that implement RRF and dot product.
/// </summary>
public class HybridSearchEngineTests
{
    [Fact]
    public void DotProduct_IdenticalNormalizedVectors_ReturnsOne()
    {
        var method = typeof(HybridSearchEngine).GetMethod("DotProduct",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        // L2-normalized vector
        var a = new float[] { 0.6f, 0.8f };
        var result = (double)method.Invoke(null, [a, a])!;

        Assert.Equal(1.0, result, 0.001);
    }

    [Fact]
    public void DotProduct_OrthogonalVectors_ReturnsZero()
    {
        var method = typeof(HybridSearchEngine).GetMethod("DotProduct",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var a = new float[] { 1.0f, 0.0f };
        var b = new float[] { 0.0f, 1.0f };
        var result = (double)method.Invoke(null, [a, b])!;

        Assert.Equal(0.0, result, 0.001);
    }

    [Fact]
    public void DotProduct_OppositeVectors_ReturnsNegativeOne()
    {
        var method = typeof(HybridSearchEngine).GetMethod("DotProduct",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var a = new float[] { 1.0f, 0.0f };
        var b = new float[] { -1.0f, 0.0f };
        var result = (double)method.Invoke(null, [a, b])!;

        Assert.Equal(-1.0, result, 0.001);
    }

    [Fact]
    public void DotProduct_DifferentLengths_UsesMinLength()
    {
        var method = typeof(HybridSearchEngine).GetMethod("DotProduct",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var a = new float[] { 1.0f, 2.0f, 3.0f };
        var b = new float[] { 4.0f, 5.0f };
        var result = (double)method.Invoke(null, [a, b])!;

        // Only first 2 elements: 1*4 + 2*5 = 14
        Assert.Equal(14.0, result, 0.001);
    }

    [Fact]
    public void ReciprocalRankFusion_EmptyLists_ReturnsEmpty()
    {
        var method = typeof(HybridSearchEngine).GetMethod("ReciprocalRankFusion",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var keyword = new List<(string Category, string Key, double Bm25Score)>();
        var vector = new List<(string Category, string Key, double Score)>();

        var result = (List<(string Category, string Key, double Score)>)
            method.Invoke(null, [keyword, vector])!;

        Assert.Empty(result);
    }

    [Fact]
    public void ReciprocalRankFusion_ItemInBothLists_HasHigherScore()
    {
        var method = typeof(HybridSearchEngine).GetMethod("ReciprocalRankFusion",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        // Item "common" appears in both lists, "only-keyword" and "only-vector" in one each
        var keyword = new List<(string Category, string Key, double Bm25Score)>
        {
            ("cat", "common", -1.0),
            ("cat", "only-keyword", -2.0),
        };
        var vector = new List<(string Category, string Key, double Score)>
        {
            ("cat", "common", 0.9),
            ("cat", "only-vector", 0.5),
        };

        var result = (List<(string Category, string Key, double Score)>)
            method.Invoke(null, [keyword, vector])!;

        Assert.Equal(3, result.Count);

        // "common" should have the highest RRF score (appears in both lists)
        Assert.Equal("common", result[0].Key);

        // Both "only-keyword" and "only-vector" should have the same RRF score (rank 2 in their list)
        var commonScore = result[0].Score;
        var singleScore = result[1].Score;
        Assert.True(commonScore > singleScore);
    }

    [Fact]
    public void ReciprocalRankFusion_RespectRankOrder()
    {
        var method = typeof(HybridSearchEngine).GetMethod("ReciprocalRankFusion",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        // Items ranked differently in keyword and vector searches
        var keyword = new List<(string Category, string Key, double Bm25Score)>
        {
            ("cat", "item-a", -1.0), // rank 1 in keyword
            ("cat", "item-b", -2.0), // rank 2 in keyword
            ("cat", "item-c", -3.0), // rank 3 in keyword
        };
        var vector = new List<(string Category, string Key, double Score)>();

        var result = (List<(string Category, string Key, double Score)>)
            method.Invoke(null, [keyword, vector])!;

        // item-a should have highest score since it's rank 1
        Assert.Equal("item-a", result[0].Key);
        Assert.Equal("item-b", result[1].Key);
        Assert.Equal("item-c", result[2].Key);

        // Verify descending RRF scores
        Assert.True(result[0].Score > result[1].Score);
        Assert.True(result[1].Score > result[2].Score);
    }

    [Fact]
    public void ReciprocalRankFusion_RrfScoreFormula_IsCorrect()
    {
        var method = typeof(HybridSearchEngine).GetMethod("ReciprocalRankFusion",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        // Single item at rank 0 (first position) in keyword results only
        var keyword = new List<(string Category, string Key, double Bm25Score)>
        {
            ("cat", "item", -1.0),
        };
        var vector = new List<(string Category, string Key, double Score)>();

        var result = (List<(string Category, string Key, double Score)>)
            method.Invoke(null, [keyword, vector])!;

        // RRF score = 1 / (k + rank + 1) = 1 / (60 + 0 + 1) = 1/61
        var expected = 1.0 / 61.0;
        Assert.Equal(expected, result[0].Score, 0.0001);
    }
}
