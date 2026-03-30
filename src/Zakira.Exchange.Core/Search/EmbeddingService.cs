using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Zakira.Exchange.Core.Search;

/// <summary>
/// Generates 384-dimensional text embeddings using the all-MiniLM-L6-v2 ONNX model.
/// All embeddings are L2-normalized so cosine similarity = dot product.
/// </summary>
public sealed class EmbeddingService : IDisposable
{
    private readonly InferenceSession _session;
    private readonly WordPieceTokenizer _tokenizer;

    /// <summary>
    /// Dimension of the output embeddings (384 for all-MiniLM-L6-v2).
    /// </summary>
    public const int EmbeddingDimension = 384;

    public EmbeddingService(string modelPath, string vocabPath)
    {
        var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            InterOpNumThreads = 1,
            IntraOpNumThreads = Environment.ProcessorCount > 1 ? 2 : 1,
        };

        _session = new InferenceSession(modelPath, options);

        var vocab = WordPieceTokenizer.LoadVocabulary(vocabPath);
        _tokenizer = new WordPieceTokenizer(vocab);
    }

    public EmbeddingService(string modelPath, Stream vocabStream)
    {
        var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            InterOpNumThreads = 1,
            IntraOpNumThreads = Environment.ProcessorCount > 1 ? 2 : 1,
        };

        _session = new InferenceSession(modelPath, options);

        var vocab = WordPieceTokenizer.LoadVocabulary(vocabStream);
        _tokenizer = new WordPieceTokenizer(vocab);
    }

    /// <summary>
    /// Generates an L2-normalized embedding for the given text.
    /// </summary>
    public float[] Embed(string text)
    {
        var (inputIds, attentionMask, tokenTypeIds) = _tokenizer.Tokenize(text);
        var seqLength = inputIds.Length;

        var inputIdsTensor = new DenseTensor<long>(inputIds, [1, seqLength]);
        var attentionMaskTensor = new DenseTensor<long>(attentionMask, [1, seqLength]);
        var tokenTypeIdsTensor = new DenseTensor<long>(tokenTypeIds, [1, seqLength]);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor),
            NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor),
        };

        using var results = _session.Run(inputs);

        // The model outputs token-level embeddings; we need to mean-pool them
        var outputTensor = results.First().AsTensor<float>();
        var embedding = MeanPool(outputTensor, attentionMask, seqLength);

        // L2 normalize so cosine similarity = dot product
        L2Normalize(embedding);

        return embedding;
    }

    /// <summary>
    /// Mean-pools token embeddings into a single sentence embedding,
    /// only averaging over non-padding tokens (attention_mask = 1).
    /// </summary>
    private static float[] MeanPool(Tensor<float> tokenEmbeddings, long[] attentionMask, int seqLength)
    {
        var embedding = new float[EmbeddingDimension];
        float tokenCount = 0;

        for (var t = 0; t < seqLength; t++)
        {
            if (attentionMask[t] == 0)
            {
                continue;
            }

            tokenCount++;
            for (var d = 0; d < EmbeddingDimension; d++)
            {
                embedding[d] += tokenEmbeddings[0, t, d];
            }
        }

        if (tokenCount > 0)
        {
            for (var d = 0; d < EmbeddingDimension; d++)
            {
                embedding[d] /= tokenCount;
            }
        }

        return embedding;
    }

    /// <summary>
    /// L2-normalizes the vector in-place so that its magnitude is 1.0.
    /// After normalization, cosine similarity = dot product.
    /// </summary>
    private static void L2Normalize(float[] vector)
    {
        var sumOfSquares = 0f;
        for (var i = 0; i < vector.Length; i++)
        {
            sumOfSquares += vector[i] * vector[i];
        }

        var magnitude = MathF.Sqrt(sumOfSquares);
        if (magnitude > 0)
        {
            for (var i = 0; i < vector.Length; i++)
            {
                vector[i] /= magnitude;
            }
        }
    }

    public void Dispose()
    {
        _session.Dispose();
    }
}
