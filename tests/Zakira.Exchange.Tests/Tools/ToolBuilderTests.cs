using System.Reflection;
using System.Text.Json;
using Zakira.Exchange.Core.Models;
using Zakira.Exchange.Cli.Tools;

namespace Zakira.Exchange.Tests.Tools;

public class ToolBuilderTests
{
    // --- ParseTags Tests ---

    [Fact]
    public void ParseTags_Null_ReturnsNull()
    {
        var method = GetParseTagsMethod();
        var result = (List<string>?)method.Invoke(null, [null]);
        Assert.Null(result);
    }

    [Fact]
    public void ParseTags_Empty_ReturnsNull()
    {
        var method = GetParseTagsMethod();
        var result = (List<string>?)method.Invoke(null, [""]);
        Assert.Null(result);
    }

    [Fact]
    public void ParseTags_Whitespace_ReturnsNull()
    {
        var method = GetParseTagsMethod();
        var result = (List<string>?)method.Invoke(null, ["   "]);
        Assert.Null(result);
    }

    [Fact]
    public void ParseTags_SingleTag_ReturnsList()
    {
        var method = GetParseTagsMethod();
        var result = (List<string>?)method.Invoke(null, ["important"]);
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("important", result[0]);
    }

    [Fact]
    public void ParseTags_MultipleTags_ReturnsList()
    {
        var method = GetParseTagsMethod();
        var result = (List<string>?)method.Invoke(null, ["tag1,tag2,tag3"]);
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal("tag1", result[0]);
        Assert.Equal("tag2", result[1]);
        Assert.Equal("tag3", result[2]);
    }

    [Fact]
    public void ParseTags_WithSpaces_TrimsEntries()
    {
        var method = GetParseTagsMethod();
        var result = (List<string>?)method.Invoke(null, [" tag1 , tag2 , tag3 "]);
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal("tag1", result[0]);
        Assert.Equal("tag2", result[1]);
        Assert.Equal("tag3", result[2]);
    }

    [Fact]
    public void ParseTags_WithEmptyEntries_SkipsThem()
    {
        var method = GetParseTagsMethod();
        var result = (List<string>?)method.Invoke(null, ["tag1,,tag2,,,tag3"]);
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
    }

    // --- ParseCustom Tests ---

    [Fact]
    public void ParseCustom_Null_ReturnsNull()
    {
        var method = GetParseCustomMethod();
        var result = (Dictionary<string, string>?)method.Invoke(null, [null]);
        Assert.Null(result);
    }

    [Fact]
    public void ParseCustom_Empty_ReturnsNull()
    {
        var method = GetParseCustomMethod();
        var result = (Dictionary<string, string>?)method.Invoke(null, [""]);
        Assert.Null(result);
    }

    [Fact]
    public void ParseCustom_ValidJson_ReturnsDictionary()
    {
        var method = GetParseCustomMethod();
        var result = (Dictionary<string, string>?)method.Invoke(null, ["{\"key\":\"value\"}"]);
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("value", result["key"]);
    }

    [Fact]
    public void ParseCustom_InvalidJson_ReturnsNull()
    {
        var method = GetParseCustomMethod();
        var result = (Dictionary<string, string>?)method.Invoke(null, ["not json at all"]);
        Assert.Null(result);
    }

    [Fact]
    public void ParseCustom_MultipleEntries_ReturnsAll()
    {
        var method = GetParseCustomMethod();
        var result = (Dictionary<string, string>?)method.Invoke(null, ["{\"a\":\"1\",\"b\":\"2\",\"c\":\"3\"}"]);
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal("1", result["a"]);
        Assert.Equal("2", result["b"]);
        Assert.Equal("3", result["c"]);
    }

    // --- ParseSearchMode Tests ---

    [Fact]
    public void ParseSearchMode_Null_ReturnsAny()
    {
        var method = GetParseSearchModeMethod();
        var result = (SearchMode)method.Invoke(null, [null])!;
        Assert.Equal(SearchMode.Any, result);
    }

    [Fact]
    public void ParseSearchMode_Empty_ReturnsAny()
    {
        var method = GetParseSearchModeMethod();
        var result = (SearchMode)method.Invoke(null, [""])!;
        Assert.Equal(SearchMode.Any, result);
    }

    [Fact]
    public void ParseSearchMode_Whitespace_ReturnsAny()
    {
        var method = GetParseSearchModeMethod();
        var result = (SearchMode)method.Invoke(null, ["   "])!;
        Assert.Equal(SearchMode.Any, result);
    }

    [Fact]
    public void ParseSearchMode_Any_ReturnsAny()
    {
        var method = GetParseSearchModeMethod();
        Assert.Equal(SearchMode.Any, (SearchMode)method.Invoke(null, ["any"])!);
        Assert.Equal(SearchMode.Any, (SearchMode)method.Invoke(null, ["ANY"])!);
        Assert.Equal(SearchMode.Any, (SearchMode)method.Invoke(null, [" Any "])!);
    }

    [Fact]
    public void ParseSearchMode_All_ReturnsAll()
    {
        var method = GetParseSearchModeMethod();
        Assert.Equal(SearchMode.All, (SearchMode)method.Invoke(null, ["all"])!);
        Assert.Equal(SearchMode.All, (SearchMode)method.Invoke(null, ["ALL"])!);
        Assert.Equal(SearchMode.All, (SearchMode)method.Invoke(null, [" All "])!);
    }

    [Fact]
    public void ParseSearchMode_Phrase_ReturnsPhrase()
    {
        var method = GetParseSearchModeMethod();
        Assert.Equal(SearchMode.Phrase, (SearchMode)method.Invoke(null, ["phrase"])!);
        Assert.Equal(SearchMode.Phrase, (SearchMode)method.Invoke(null, ["PHRASE"])!);
        Assert.Equal(SearchMode.Phrase, (SearchMode)method.Invoke(null, [" Phrase "])!);
    }

    [Fact]
    public void ParseSearchMode_UnknownValue_FallsBackToAny()
    {
        var method = GetParseSearchModeMethod();
        Assert.Equal(SearchMode.Any, (SearchMode)method.Invoke(null, ["fuzzy"])!);
        Assert.Equal(SearchMode.Any, (SearchMode)method.Invoke(null, ["123"])!);
    }

    // --- BuildListFilter Tests ---

    [Fact]
    public void BuildListFilter_AllDefaults_ReturnsDefaultFilter()
    {
        var method = GetBuildListFilterMethod();
        var result = (ListFilter)method.Invoke(null, [null, null, null, null, null, null])!;

        Assert.Null(result.Category);
        Assert.Equal(50, result.Top);
        Assert.Null(result.Author);
        Assert.Null(result.Tags);
        Assert.Null(result.Before);
        Assert.Null(result.After);
    }

    [Fact]
    public void BuildListFilter_WithCategory_SetsCategory()
    {
        var method = GetBuildListFilterMethod();
        var result = (ListFilter)method.Invoke(null, ["my-cat", null, null, null, null, null])!;
        Assert.Equal("my-cat", result.Category);
    }

    [Fact]
    public void BuildListFilter_WithTop_SetsTop()
    {
        var method = GetBuildListFilterMethod();
        var result = (ListFilter)method.Invoke(null, [null, (int?)25, null, null, null, null])!;
        Assert.Equal(25, result.Top);
    }

    [Fact]
    public void BuildListFilter_WithAuthor_SetsAuthor()
    {
        var method = GetBuildListFilterMethod();
        var result = (ListFilter)method.Invoke(null, [null, null, "alice", null, null, null])!;
        Assert.Equal("alice", result.Author);
    }

    [Fact]
    public void BuildListFilter_WithTags_ParsesAndSetsTags()
    {
        var method = GetBuildListFilterMethod();
        var result = (ListFilter)method.Invoke(null, [null, null, null, "tag1,tag2", null, null])!;
        Assert.NotNull(result.Tags);
        Assert.Equal(2, result.Tags.Count);
    }

    [Fact]
    public void BuildListFilter_WithTimestamps_ParsesCorrectly()
    {
        var method = GetBuildListFilterMethod();
        var before = "2026-01-15T12:00:00Z";
        var after = "2026-01-01T00:00:00Z";

        var result = (ListFilter)method.Invoke(null, [null, null, null, null, before, after])!;

        Assert.NotNull(result.Before);
        Assert.NotNull(result.After);
        Assert.Equal(2026, result.Before!.Value.Year);
        Assert.Equal(1, result.After!.Value.Month);
    }

    // --- FormatEntry Tests ---

    [Fact]
    public void FormatEntry_BasicEntry_ContainsKeyInfo()
    {
        var method = GetFormatEntryMethod();
        var entry = new MemoryEntry
        {
            Category = "decisions",
            Key = "use-redis",
            Data = "We decided to use Redis for caching.",
            Metadata = new MemoryMetadata
            {
                Author = "architect",
                CreatedAt = DateTimeOffset.UtcNow,
                LastModifiedAt = DateTimeOffset.UtcNow,
            }
        };

        var result = (string)method.Invoke(null, [entry])!;

        Assert.Contains("[decisions] use-redis", result);
        Assert.Contains("We decided to use Redis for caching.", result);
        Assert.Contains("architect", result);
    }

    [Fact]
    public void FormatEntry_WithTags_ShowsTags()
    {
        var method = GetFormatEntryMethod();
        var entry = new MemoryEntry
        {
            Category = "test",
            Key = "key",
            Data = "data",
            Metadata = new MemoryMetadata
            {
                Tags = ["important", "architecture"],
                CreatedAt = DateTimeOffset.UtcNow,
                LastModifiedAt = DateTimeOffset.UtcNow,
            }
        };

        var result = (string)method.Invoke(null, [entry])!;
        Assert.Contains("important", result);
        Assert.Contains("architecture", result);
    }

    // --- FormatEntryList Tests ---

    [Fact]
    public void FormatEntryList_EmptyList_ReturnsNotFoundMessage()
    {
        var method = GetFormatEntryListMethod();
        var result = (string)method.Invoke(null, [new List<MemoryEntry>(), 0L])!;
        Assert.Contains("No memory entries found", result);
    }

    [Fact]
    public void FormatEntryList_WithEntries_ShowsCount()
    {
        var method = GetFormatEntryListMethod();
        var entries = new List<MemoryEntry>
        {
            new()
            {
                Category = "cat",
                Key = "k1",
                Data = "data1",
                Metadata = new MemoryMetadata
                {
                    CreatedAt = DateTimeOffset.UtcNow,
                    LastModifiedAt = DateTimeOffset.UtcNow,
                }
            }
        };

        var result = (string)method.Invoke(null, [entries, 5L])!;
        Assert.Contains("Found 1 entries", result);
        Assert.Contains("total in store: 5", result);
    }

    // --- FormatSearchResults Tests ---

    [Fact]
    public void FormatSearchResults_EmptyResults_ReturnsNotFoundMessage()
    {
        var method = GetFormatSearchResultsMethod();
        var result = (string)method.Invoke(null, [new List<SearchResult>()])!;
        Assert.Contains("No memory entries found", result);
    }

    [Fact]
    public void FormatSearchResults_WithResults_ShowsScores()
    {
        var method = GetFormatSearchResultsMethod();
        var results = new List<SearchResult>
        {
            new()
            {
                Entry = new MemoryEntry
                {
                    Category = "cat",
                    Key = "key1",
                    Data = "relevant data",
                    Metadata = new MemoryMetadata
                    {
                        Author = "agent",
                        CreatedAt = DateTimeOffset.UtcNow,
                        LastModifiedAt = DateTimeOffset.UtcNow,
                    }
                },
                Score = 0.0328
            }
        };

        var result = (string)method.Invoke(null, [results])!;
        Assert.Contains("Found 1 results", result);
        Assert.Contains("0.0328", result);
        Assert.Contains("[cat] key1", result);
        Assert.Contains("relevant data", result);
    }

    // --- Helper methods to get private/static methods via reflection ---

    private static MethodInfo GetParseTagsMethod()
    {
        return typeof(ToolBuilder).GetMethod("ParseTags",
            BindingFlags.NonPublic | BindingFlags.Static)!;
    }

    private static MethodInfo GetParseCustomMethod()
    {
        return typeof(ToolBuilder).GetMethod("ParseCustom",
            BindingFlags.NonPublic | BindingFlags.Static)!;
    }

    private static MethodInfo GetParseSearchModeMethod()
    {
        return typeof(ToolBuilder).GetMethod("ParseSearchMode",
            BindingFlags.NonPublic | BindingFlags.Static)!;
    }

    private static MethodInfo GetBuildListFilterMethod()
    {
        return typeof(ToolBuilder).GetMethod("BuildListFilter",
            BindingFlags.NonPublic | BindingFlags.Static)!;
    }

    private static MethodInfo GetFormatEntryMethod()
    {
        return typeof(ToolBuilder).GetMethod("FormatEntry",
            BindingFlags.NonPublic | BindingFlags.Static)!;
    }

    private static MethodInfo GetFormatEntryListMethod()
    {
        return typeof(ToolBuilder).GetMethod("FormatEntryList",
            BindingFlags.NonPublic | BindingFlags.Static)!;
    }

    private static MethodInfo GetFormatSearchResultsMethod()
    {
        return typeof(ToolBuilder).GetMethod("FormatSearchResults",
            BindingFlags.NonPublic | BindingFlags.Static)!;
    }
}
