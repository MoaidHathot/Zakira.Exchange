using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;
using Zakira.Exchange.Core.Models;
using Zakira.Exchange.Core.Storage;

namespace Zakira.Exchange.Tests.Storage;

/// <summary>
/// Concurrency tests for MemoryStore. These would fail (with intermittent
/// SqliteException, data races, or invalid-state exceptions) if the store
/// held a single shared connection across threads. Each test uses its own
/// temp database so parallel test runs don't collide.
/// </summary>
public class MemoryStoreConcurrencyTests : IDisposable
{
    private readonly string _dbPath;
    private readonly MemoryStore _store;

    public MemoryStoreConcurrencyTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"zakira_concurrency_{Guid.NewGuid():N}.db");
        _store = new MemoryStore(_dbPath);
    }

    public void Dispose()
    {
        _store.Dispose();
        SqliteConnection.ClearAllPools();
        TryDeleteFile(_dbPath);
        TryDeleteFile(_dbPath + "-wal");
        TryDeleteFile(_dbPath + "-shm");
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
    }

    private static MemoryEntry CreateEntry(string category = "test", string key = "key1", string data = "test data")
    {
        var now = DateTimeOffset.UtcNow;
        return new MemoryEntry
        {
            Category = category,
            Key = key,
            Data = data,
            Metadata = new MemoryMetadata { CreatedAt = now, LastModifiedAt = now }
        };
    }

    [Fact]
    public async Task Create_ConcurrentUniqueKeys_AllSucceed()
    {
        const int taskCount = 100;
        var exceptions = new ConcurrentBag<Exception>();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, taskCount),
            new ParallelOptions { MaxDegreeOfParallelism = 16 },
            async (i, _) =>
            {
                try
                {
                    _store.Create(CreateEntry(key: $"key-{i:D4}", data: $"data {i}"), null);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
                await Task.Yield();
            });

        Assert.Empty(exceptions);
        Assert.Equal(taskCount, _store.GetCount());
    }

    [Fact]
    public async Task MixedOperations_NoExceptions()
    {
        // Seed entries so reads/updates have something to hit
        for (var i = 0; i < 20; i++)
        {
            _store.Create(CreateEntry(key: $"seed-{i:D2}", data: $"seed data {i}"), null);
        }

        const int taskCount = 100;
        var exceptions = new ConcurrentBag<Exception>();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, taskCount),
            new ParallelOptions { MaxDegreeOfParallelism = 16 },
            async (i, _) =>
            {
                try
                {
                    // Deterministic per-iteration RNG so tests are reproducible
                    var rng = new Random(i);
                    var op = i % 5;
                    switch (op)
                    {
                        case 0:
                            _store.Create(CreateEntry(key: $"new-{i:D4}", data: $"data {i}"), null);
                            break;
                        case 1:
                            _store.Get("test", $"seed-{rng.Next(20):D2}");
                            break;
                        case 2:
                            var entry = CreateEntry(key: $"seed-{rng.Next(20):D2}", data: $"updated by {i}");
                            _store.Update(entry, null);
                            break;
                        case 3:
                            _store.List(new ListFilter { Top = 10 });
                            break;
                        case 4:
                            _store.GetCount();
                            _store.GetCategories();
                            break;
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
                await Task.Yield();
            });

        Assert.Empty(exceptions);
    }

    [Fact]
    public async Task ConcurrentReadersAndWriter_NoExceptions()
    {
        // Seed
        for (var i = 0; i < 10; i++)
        {
            _store.Create(CreateEntry(key: $"seed-{i:D2}", data: $"data {i}"), null);
        }

        var exceptions = new ConcurrentBag<Exception>();
        var stop = false;

        // Writer task: continuously updates seed-00
        var writer = Task.Run(() =>
        {
            try
            {
                for (var i = 0; i < 200 && !stop; i++)
                {
                    var entry = CreateEntry(key: "seed-00", data: $"writer iteration {i}");
                    _store.Update(entry, null);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        // Concurrent readers
        var readers = Enumerable.Range(0, 20).Select(_ => Task.Run(() =>
        {
            try
            {
                for (var i = 0; i < 50; i++)
                {
                    _store.Get("test", "seed-00");
                    _store.List(new ListFilter { Top = 10 });
                    _store.GetCount();
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToArray();

        await Task.WhenAll(readers);
        stop = true;
        await writer;

        Assert.Empty(exceptions);
    }

    [Fact]
    public async Task Update_ConcurrentSameKey_LastWriteWins_NoExceptions()
    {
        _store.Create(CreateEntry(key: "contested", data: "initial"), null);

        const int taskCount = 50;
        var exceptions = new ConcurrentBag<Exception>();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, taskCount),
            new ParallelOptions { MaxDegreeOfParallelism = 16 },
            async (i, _) =>
            {
                try
                {
                    var entry = CreateEntry(key: "contested", data: $"writer {i}");
                    _store.Update(entry, null);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
                await Task.Yield();
            });

        Assert.Empty(exceptions);

        // Exactly one row should still exist
        Assert.Equal(1, _store.GetCount());

        // Final value must be one of the writers' values (last-write-wins)
        var final = _store.Get("test", "contested");
        Assert.NotNull(final);
        Assert.Matches(@"^writer \d+$", final.Data);
    }
}
