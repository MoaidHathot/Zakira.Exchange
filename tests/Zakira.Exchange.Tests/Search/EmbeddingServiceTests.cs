using System.Reflection;
using Zakira.Exchange.Core.Search;

namespace Zakira.Exchange.Tests.Search;

/// <summary>
/// Tests for EmbeddingService static helper methods (MeanPool, L2Normalize).
/// These are tested via reflection since they are private static methods.
/// Full integration tests require the ONNX model file which may not be available in CI.
/// </summary>
public class EmbeddingServiceTests
{
    [Fact]
    public void L2Normalize_NormalizesVector()
    {
        // Use reflection to call private static method
        var method = typeof(EmbeddingService).GetMethod("L2Normalize",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var vector = new float[] { 3.0f, 4.0f };
        method.Invoke(null, [vector]);

        // Magnitude of [3,4] = 5, so normalized = [0.6, 0.8]
        Assert.Equal(0.6f, vector[0], 0.0001f);
        Assert.Equal(0.8f, vector[1], 0.0001f);

        // Verify unit magnitude
        var magnitude = MathF.Sqrt(vector[0] * vector[0] + vector[1] * vector[1]);
        Assert.Equal(1.0f, magnitude, 0.0001f);
    }

    [Fact]
    public void L2Normalize_ZeroVector_StaysZero()
    {
        var method = typeof(EmbeddingService).GetMethod("L2Normalize",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var vector = new float[] { 0.0f, 0.0f, 0.0f };
        method.Invoke(null, [vector]);

        Assert.All(vector, v => Assert.Equal(0.0f, v));
    }

    [Fact]
    public void L2Normalize_AlreadyNormalized_StaysNormalized()
    {
        var method = typeof(EmbeddingService).GetMethod("L2Normalize",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var vector = new float[] { 1.0f, 0.0f, 0.0f };
        method.Invoke(null, [vector]);

        Assert.Equal(1.0f, vector[0], 0.0001f);
        Assert.Equal(0.0f, vector[1], 0.0001f);
        Assert.Equal(0.0f, vector[2], 0.0001f);
    }

    [Fact]
    public void EmbeddingDimension_Is384()
    {
        Assert.Equal(384, EmbeddingService.EmbeddingDimension);
    }
}
