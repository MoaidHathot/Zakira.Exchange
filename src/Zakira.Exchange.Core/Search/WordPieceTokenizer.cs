namespace Zakira.Exchange.Core.Search;

/// <summary>
/// A minimal WordPiece tokenizer compatible with the all-MiniLM-L6-v2 model.
/// Uses a vocabulary loaded from an embedded resource or file.
/// </summary>
public sealed class WordPieceTokenizer
{
    private readonly Dictionary<string, int> _vocab;
    private readonly int _unknownTokenId;
    private readonly int _clsTokenId;
    private readonly int _sepTokenId;
    private readonly int _maxTokenLength;

    private const string UnknownToken = "[UNK]";
    private const string ClsToken = "[CLS]";
    private const string SepToken = "[SEP]";
    private const string ContinuationPrefix = "##";

    public WordPieceTokenizer(Dictionary<string, int> vocab, int maxTokenLength = 512)
    {
        _vocab = vocab;
        _maxTokenLength = maxTokenLength;
        _unknownTokenId = _vocab.GetValueOrDefault(UnknownToken, 0);
        _clsTokenId = _vocab.GetValueOrDefault(ClsToken, 101);
        _sepTokenId = _vocab.GetValueOrDefault(SepToken, 102);
    }

    /// <summary>
    /// Tokenizes input text into (inputIds, attentionMask, tokenTypeIds) arrays suitable for BERT-style models.
    /// </summary>
    public (long[] InputIds, long[] AttentionMask, long[] TokenTypeIds) Tokenize(string text)
    {
        var tokens = new List<int> { _clsTokenId };

        var words = BasicTokenize(text);
        foreach (var word in words)
        {
            var subTokens = WordPieceTokenize(word);
            if (tokens.Count + subTokens.Count >= _maxTokenLength - 1)
            {
                // Leave room for [SEP]
                var remaining = _maxTokenLength - 1 - tokens.Count;
                tokens.AddRange(subTokens.Take(remaining));
                break;
            }
            tokens.AddRange(subTokens);
        }

        tokens.Add(_sepTokenId);

        var inputIds = new long[tokens.Count];
        var attentionMask = new long[tokens.Count];
        var tokenTypeIds = new long[tokens.Count];

        for (var i = 0; i < tokens.Count; i++)
        {
            inputIds[i] = tokens[i];
            attentionMask[i] = 1;
            tokenTypeIds[i] = 0;
        }

        return (inputIds, attentionMask, tokenTypeIds);
    }

    private List<int> WordPieceTokenize(string word)
    {
        var result = new List<int>();
        var start = 0;

        while (start < word.Length)
        {
            var end = word.Length;
            var found = false;

            while (start < end)
            {
                var substr = word[start..end];
                if (start > 0)
                {
                    substr = ContinuationPrefix + substr;
                }

                if (_vocab.TryGetValue(substr, out var id))
                {
                    result.Add(id);
                    found = true;
                    start = end;
                    break;
                }

                end--;
            }

            if (!found)
            {
                result.Add(_unknownTokenId);
                start++;
            }
        }

        return result;
    }

    /// <summary>
    /// Basic pre-tokenization: lowercase, split on whitespace and punctuation.
    /// </summary>
    private static List<string> BasicTokenize(string text)
    {
        text = text.ToLowerInvariant();
        var words = new List<string>();
        var current = new System.Text.StringBuilder();

        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                if (current.Length > 0)
                {
                    words.Add(current.ToString());
                    current.Clear();
                }
            }
            else if (char.IsPunctuation(c) || char.IsSymbol(c))
            {
                if (current.Length > 0)
                {
                    words.Add(current.ToString());
                    current.Clear();
                }
                words.Add(c.ToString());
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            words.Add(current.ToString());
        }

        return words;
    }

    /// <summary>
    /// Loads a vocabulary from a text file where each line is a token, indexed by line number.
    /// </summary>
    public static Dictionary<string, int> LoadVocabulary(string vocabPath)
    {
        var vocab = new Dictionary<string, int>();
        var lines = File.ReadAllLines(vocabPath);
        for (var i = 0; i < lines.Length; i++)
        {
            var token = lines[i].Trim();
            if (!string.IsNullOrEmpty(token))
            {
                vocab[token] = i;
            }
        }
        return vocab;
    }

    /// <summary>
    /// Loads a vocabulary from a stream (e.g. embedded resource).
    /// </summary>
    public static Dictionary<string, int> LoadVocabulary(Stream stream)
    {
        var vocab = new Dictionary<string, int>();
        using var reader = new StreamReader(stream);
        var index = 0;
        while (reader.ReadLine() is { } line)
        {
            var token = line.Trim();
            if (!string.IsNullOrEmpty(token))
            {
                vocab[token] = index;
            }
            index++;
        }
        return vocab;
    }
}
