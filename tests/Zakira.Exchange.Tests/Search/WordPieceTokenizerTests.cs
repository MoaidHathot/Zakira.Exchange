using Zakira.Exchange.Core.Search;

namespace Zakira.Exchange.Tests.Search;

public class WordPieceTokenizerTests
{
    private static Dictionary<string, int> CreateTestVocab()
    {
        // Minimal BERT-compatible vocabulary for testing
        return new Dictionary<string, int>
        {
            ["[PAD]"] = 0,
            ["[UNK]"] = 100,
            ["[CLS]"] = 101,
            ["[SEP]"] = 102,
            ["hello"] = 7592,
            ["world"] = 2088,
            ["the"] = 1996,
            ["a"] = 1037,
            ["test"] = 3231,
            ["##ing"] = 2075,
            ["##ed"] = 2098,
            ["##s"] = 2015,
            ["run"] = 2448,
            ["."] = 1012,
            [","] = 1010,
            ["!"] = 999,
        };
    }

    [Fact]
    public void Tokenize_SimpleText_WrapsWithClsAndSep()
    {
        var vocab = CreateTestVocab();
        var tokenizer = new WordPieceTokenizer(vocab);

        var (inputIds, attentionMask, tokenTypeIds) = tokenizer.Tokenize("hello world");

        // First token should be [CLS] = 101
        Assert.Equal(101, inputIds[0]);
        // Last token should be [SEP] = 102
        Assert.Equal(102, inputIds[^1]);
    }

    [Fact]
    public void Tokenize_SimpleText_ReturnsCorrectTokenIds()
    {
        var vocab = CreateTestVocab();
        var tokenizer = new WordPieceTokenizer(vocab);

        var (inputIds, _, _) = tokenizer.Tokenize("hello world");

        // [CLS] hello world [SEP]
        Assert.Equal(4, inputIds.Length);
        Assert.Equal(101, inputIds[0]);  // [CLS]
        Assert.Equal(7592, inputIds[1]); // hello
        Assert.Equal(2088, inputIds[2]); // world
        Assert.Equal(102, inputIds[3]);  // [SEP]
    }

    [Fact]
    public void Tokenize_AttentionMask_AllOnes()
    {
        var vocab = CreateTestVocab();
        var tokenizer = new WordPieceTokenizer(vocab);

        var (_, attentionMask, _) = tokenizer.Tokenize("hello");

        Assert.All(attentionMask, mask => Assert.Equal(1, mask));
    }

    [Fact]
    public void Tokenize_TokenTypeIds_AllZeros()
    {
        var vocab = CreateTestVocab();
        var tokenizer = new WordPieceTokenizer(vocab);

        var (_, _, tokenTypeIds) = tokenizer.Tokenize("hello");

        Assert.All(tokenTypeIds, id => Assert.Equal(0, id));
    }

    [Fact]
    public void Tokenize_UnknownWord_ReturnsUnknownTokenId()
    {
        var vocab = CreateTestVocab();
        var tokenizer = new WordPieceTokenizer(vocab);

        var (inputIds, _, _) = tokenizer.Tokenize("xyz");

        // [CLS] [UNK] [SEP] - each character unknown since "xyz" not in vocab
        Assert.Equal(101, inputIds[0]);  // [CLS]
        Assert.Equal(102, inputIds[^1]); // [SEP]
        // Middle tokens should be [UNK] = 100
        Assert.Contains(100L, inputIds);
    }

    [Fact]
    public void Tokenize_SubwordTokenization_SplitsWithContinuationPrefix()
    {
        var vocab = CreateTestVocab();
        var tokenizer = new WordPieceTokenizer(vocab);

        // "testing" should be split into "test" + "##ing"
        var (inputIds, _, _) = tokenizer.Tokenize("testing");

        // [CLS] test ##ing [SEP]
        Assert.Equal(4, inputIds.Length);
        Assert.Equal(101, inputIds[0]);  // [CLS]
        Assert.Equal(3231, inputIds[1]); // test
        Assert.Equal(2075, inputIds[2]); // ##ing
        Assert.Equal(102, inputIds[3]);  // [SEP]
    }

    [Fact]
    public void Tokenize_Lowercases_Input()
    {
        var vocab = CreateTestVocab();
        var tokenizer = new WordPieceTokenizer(vocab);

        var (inputIds, _, _) = tokenizer.Tokenize("HELLO WORLD");

        // Should lowercase "HELLO" to "hello" and find it in vocab
        Assert.Equal(101, inputIds[0]);  // [CLS]
        Assert.Equal(7592, inputIds[1]); // hello
        Assert.Equal(2088, inputIds[2]); // world
        Assert.Equal(102, inputIds[3]);  // [SEP]
    }

    [Fact]
    public void Tokenize_Punctuation_SplitsSeparately()
    {
        var vocab = CreateTestVocab();
        var tokenizer = new WordPieceTokenizer(vocab);

        var (inputIds, _, _) = tokenizer.Tokenize("hello, world!");

        // [CLS] hello , world ! [SEP]
        Assert.Equal(6, inputIds.Length);
        Assert.Equal(7592, inputIds[1]); // hello
        Assert.Equal(1010, inputIds[2]); // ,
        Assert.Equal(2088, inputIds[3]); // world
        Assert.Equal(999, inputIds[4]);  // !
    }

    [Fact]
    public void Tokenize_EmptyString_ReturnsClsAndSepOnly()
    {
        var vocab = CreateTestVocab();
        var tokenizer = new WordPieceTokenizer(vocab);

        var (inputIds, _, _) = tokenizer.Tokenize("");

        Assert.Equal(2, inputIds.Length);
        Assert.Equal(101, inputIds[0]); // [CLS]
        Assert.Equal(102, inputIds[1]); // [SEP]
    }

    [Fact]
    public void Tokenize_MaxLength_TruncatesTokens()
    {
        var vocab = CreateTestVocab();
        var tokenizer = new WordPieceTokenizer(vocab, maxTokenLength: 4);

        // "hello world test" would produce 5 tokens ([CLS] hello world test [SEP])
        // with maxTokenLength=4, we should get at most 4 tokens: [CLS] hello world [SEP]
        var (inputIds, _, _) = tokenizer.Tokenize("hello world test");

        Assert.True(inputIds.Length <= 4);
        Assert.Equal(101, inputIds[0]);  // [CLS]
        Assert.Equal(102, inputIds[^1]); // [SEP]
    }

    [Fact]
    public void LoadVocabulary_FromStream_LoadsCorrectly()
    {
        var vocabText = "[PAD]\n[UNK]\nhello\nworld\n";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(vocabText));

        var vocab = WordPieceTokenizer.LoadVocabulary(stream);

        Assert.Equal(4, vocab.Count);
        Assert.Equal(0, vocab["[PAD]"]);
        Assert.Equal(1, vocab["[UNK]"]);
        Assert.Equal(2, vocab["hello"]);
        Assert.Equal(3, vocab["world"]);
    }

    [Fact]
    public void LoadVocabulary_FromStream_SkipsEmptyLines()
    {
        var vocabText = "[PAD]\n\nhello\n\nworld\n";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(vocabText));

        var vocab = WordPieceTokenizer.LoadVocabulary(stream);

        // Empty lines skipped, but index increments
        Assert.Equal(3, vocab.Count);
        Assert.Equal(0, vocab["[PAD]"]);
        Assert.Equal(2, vocab["hello"]);
        Assert.Equal(4, vocab["world"]);
    }

    [Fact]
    public void LoadVocabulary_FromFile_LoadsCorrectly()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "[PAD]\n[UNK]\nhello\nworld\n");

            var vocab = WordPieceTokenizer.LoadVocabulary(tempFile);

            Assert.Equal(4, vocab.Count);
            Assert.Equal(0, vocab["[PAD]"]);
            Assert.Equal(1, vocab["[UNK]"]);
            Assert.Equal(2, vocab["hello"]);
            Assert.Equal(3, vocab["world"]);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
