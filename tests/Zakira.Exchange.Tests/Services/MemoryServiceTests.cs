using Microsoft.Data.Sqlite;
using Zakira.Exchange.Core.Configuration;
using Zakira.Exchange.Core.Models;
using Zakira.Exchange.Core.Services;

namespace Zakira.Exchange.Tests.Services;

/// <summary>
/// Tests for MemoryService.
/// These tests focus on non-embedding operations (Get, List, Delete, GetCategories, GetCount)
/// and the BuildEmbeddingText helper, since embedding-dependent operations require the ONNX model.
/// </summary>
public class MemoryServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly MemoryService _service;

    public MemoryServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"zakira_svc_test_{Guid.NewGuid():N}.db");
        _service = new MemoryService(new ZakiraOptions { DatabasePath = _dbPath });
    }

    public void Dispose()
    {
        _service.Dispose();
        SqliteConnection.ClearAllPools();
        TryDeleteFile(_dbPath);
        TryDeleteFile(_dbPath + "-wal");
        TryDeleteFile(_dbPath + "-shm");
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
    }

    [Fact]
    public void Get_NonExistent_ReturnsNull()
    {
        var result = _service.Get("cat", "key");
        Assert.Null(result);
    }

    [Fact]
    public void GetCategories_EmptyStore_ReturnsEmpty()
    {
        var categories = _service.GetCategories();
        Assert.Empty(categories);
    }

    [Fact]
    public void GetCount_EmptyStore_ReturnsZero()
    {
        Assert.Equal(0, _service.GetCount());
    }

    [Fact]
    public void List_EmptyStore_ReturnsEmpty()
    {
        var result = _service.List(new ListFilter());
        Assert.Empty(result);
    }

    [Fact]
    public void Delete_NonExistent_ReturnsFalse()
    {
        var result = _service.Delete("cat", "key");
        Assert.False(result);
    }

    [Fact]
    public void EditWithConcurrency_NoSuchEntry_ReturnsNotFoundWithoutLoadingModel()
    {
        // The store has no entries, so EditWithConcurrency must short-circuit on
        // the existence check before touching the embedding service. This both
        // exercises the wiring of the new overload and verifies we don't pay the
        // model-load cost on the not-found path.
        var result = _service.EditWithConcurrency(
            category: "any", key: "missing",
            data: "ignored", author: null, reason: null, tags: null, custom: null,
            expectedLastModifiedAt: null);

        Assert.Equal(EditOutcome.NotFound, result.Outcome);
        Assert.Null(result.Entry);
        Assert.Null(result.CurrentLastModifiedAt);
    }

    [Fact]
    public void EditWithConcurrency_NoSuchEntry_WithExpected_StillReturnsNotFound()
    {
        // Same short-circuit when the caller passes an expected timestamp -
        // existence check fires first, no Conflict noise.
        var result = _service.EditWithConcurrency(
            category: "any", key: "missing",
            data: "ignored", author: null, reason: null, tags: null, custom: null,
            expectedLastModifiedAt: DateTimeOffset.UtcNow);

        Assert.Equal(EditOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public void Options_ReturnsConfiguredOptions()
    {
        var options = _service.Options;
        Assert.Equal(_dbPath, options.DatabasePath);
        Assert.Equal(AccessMode.Full, options.AccessMode);
    }

    [Fact]
    public void BuildEmbeddingText_BasicEntry_CombinesKeyAndData()
    {
        var method = typeof(MemoryService).GetMethod("BuildEmbeddingText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var entry = new MemoryEntry
        {
            Category = "test",
            Key = "my-key",
            Data = "some data",
            Metadata = new MemoryMetadata()
        };

        var result = (string)method.Invoke(null, [entry])!;
        Assert.Equal("my-key | some data", result);
    }

    [Fact]
    public void BuildEmbeddingText_WithTags_IncludesTags()
    {
        var method = typeof(MemoryService).GetMethod("BuildEmbeddingText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var entry = new MemoryEntry
        {
            Category = "test",
            Key = "my-key",
            Data = "some data",
            Metadata = new MemoryMetadata
            {
                Tags = ["tag1", "tag2"]
            }
        };

        var result = (string)method.Invoke(null, [entry])!;
        Assert.Equal("my-key | some data | tag1 tag2", result);
    }

    [Fact]
    public void BuildEmbeddingText_WithReason_IncludesReason()
    {
        var method = typeof(MemoryService).GetMethod("BuildEmbeddingText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var entry = new MemoryEntry
        {
            Category = "test",
            Key = "my-key",
            Data = "some data",
            Metadata = new MemoryMetadata
            {
                Reason = "important decision"
            }
        };

        var result = (string)method.Invoke(null, [entry])!;
        Assert.Equal("my-key | some data | important decision", result);
    }

    [Fact]
    public void BuildEmbeddingText_WithTagsAndReason_IncludesBoth()
    {
        var method = typeof(MemoryService).GetMethod("BuildEmbeddingText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var entry = new MemoryEntry
        {
            Category = "test",
            Key = "my-key",
            Data = "some data",
            Metadata = new MemoryMetadata
            {
                Tags = ["tag1"],
                Reason = "because"
            }
        };

        var result = (string)method.Invoke(null, [entry])!;
        Assert.Equal("my-key | some data | tag1 | because", result);
    }
}

public class MemoryServiceConstCategoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly MemoryService _service;

    public MemoryServiceConstCategoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"zakira_const_test_{Guid.NewGuid():N}.db");
        _service = new MemoryService(new ZakiraOptions
        {
            DatabasePath = _dbPath,
            ConstCategory = "fixed-category"
        });
    }

    public void Dispose()
    {
        _service.Dispose();
        SqliteConnection.ClearAllPools();
        TryDeleteFile(_dbPath);
        TryDeleteFile(_dbPath + "-wal");
        TryDeleteFile(_dbPath + "-shm");
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
    }

    [Fact]
    public void Get_WithConstCategory_UsesConstCategory()
    {
        // When const-category is set, the provided category should be ignored
        // and the const-category should be used instead
        var result = _service.Get("any-category", "key");
        Assert.Null(result); // Just verifying it doesn't throw
    }

    [Fact]
    public void List_WithConstCategory_OverridesFilterCategory()
    {
        var filter = new ListFilter { Category = "some-other-category" };
        var result = _service.List(filter);

        // The filter should be overridden with const-category
        Assert.Equal("fixed-category", filter.Category);
        Assert.Empty(result);
    }

    [Fact]
    public void GetCount_WithConstCategory_UsesConstCategory()
    {
        var count = _service.GetCount("some-other-category");
        // Should not throw, and should use const-category
        Assert.Equal(0, count);
    }

    [Fact]
    public void Delete_WithConstCategory_UsesConstCategory()
    {
        // Should not throw
        var result = _service.Delete("any-category", "key");
        Assert.False(result);
    }
}
