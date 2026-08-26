using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Structure;

namespace BenchLumDb;

/// <summary>
/// Post-migration benchmarks using DbCell / RowView APIs.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 1, iterationCount: 5)]
[Config(typeof(Cfg))]
public class DbCellApiBench
{
    private const string Table = "bench";
    private const int RowCount = 2000;

    private string _path = null!;
    private DbEngine _engine = null!;
    private ITransaction _tx = null!;

    private sealed class Cfg : ManualConfig
    {
        public Cfg() => WithOptions(ConfigOptions.DisableOptimizationsValidator);
    }

    [GlobalSetup]
    public void Setup()
    {
        _path = Path.Combine(Path.GetTempPath(), "lumdb_bench_cell_" + Guid.NewGuid().ToString("N") + ".db");
        _engine = new DbEngine(_path);
        using var ts = _engine.StartTransaction();
        ts.Create(Table, [
            ("uid", DbValueType.Int, true),
            ("score", DbValueType.Long, false),
            ("pay", DbValueType.Decimal, false),
            ("when", DbValueType.DateTimeUTC, false),
            ("name", DbValueType.Str32B, false),
            ("note", DbValueType.StrVar, false),
        ]);

        var utc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < RowCount; i++)
        {
            ts.Insert(Table, [
                ("uid", i),
                ("score", (long)i * i),
                ("pay", 1.2300m + i),
                ("when", utc.AddSeconds(i)),
                ("name", "user" + i),
                ("note", "note-content-" + i),
            ]);
        }

        ts.SaveChanges();
        _tx = _engine.StartTransaction();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _tx.Dispose();
        _engine.SetDestoryOnDisposed();
        _engine.Dispose();
    }

    [Benchmark]
    public uint InsertOne_DbCell()
    {
        var id = _tx.Insert(Table, [
            ("uid", RowCount + 99999),
            ("score", 123L),
            ("pay", 9.99m),
            ("when", DateTime.UtcNow),
            ("name", "bench"),
            ("note", "insert-one"),
        ]);
        _tx.Discard();
        return id.Value;
    }

    [Benchmark]
    public string? FindByKey_DbCell()
    {
        var r = _tx.Find(Table, "uid", 1234);
        return r.IsSuccess ? r.Row.GetString(4) : null;
    }

    [Benchmark]
    public int FindById_Row()
    {
        var r = _tx.Find(Table, 500u);
        return r.IsSuccess ? r.Row.GetInt(0) : -1;
    }

    [Benchmark]
    public int GoThrough_RowView()
    {
        int count = 0;
        _tx.GoThrough(Table, (ref RowView row) =>
        {
            count++;
            _ = row.GetInt(0);
            return true;
        });
        return count;
    }
}
