using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using LumDbEngine;
using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Manager.Specific;
using LumDbEngine.Element.Structure;

namespace BenchLumDb;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 1, iterationCount: 5)]
[Config(typeof(Cfg))]
    public partial class LargeVarFilterBench
{
    private const string Table = "orders";
    private const int RowCount = 500;

    private string _path = null!;
    private DbEngine _engine = null!;
    private ITransaction _tx = null!;

    [LumEntity]
    public partial class OrderDoc
    {
        [Id] public uint Id { get; set; }
        [Key] public int OrderNo { get; set; }
        public int Score { get; set; }
        public string Body { get; set; } = "";
    }

    private sealed class Cfg : ManualConfig
    {
        public Cfg() => WithOptions(ConfigOptions.DisableOptimizationsValidator);
    }

    [GlobalSetup]
    public void Setup()
    {
        _path = Path.Combine(Path.GetTempPath(), "lumdb_bench_var_" + Guid.NewGuid().ToString("N") + ".db");
        _engine = new DbEngine(_path);
        var big = new string('Z', 32_000);
        using var ts = _engine.StartTransaction();
        ts.Create<OrderDoc>(Table);
        for (int i = 0; i < RowCount; i++)
        {
            ts.Insert(Table, new OrderDoc { OrderNo = i, Score = i % 11, Body = big });
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
    public long FilterScore_NoBodyTouch()
    {
        DataVarManager.GetDataVarCallCount = 0;
        long sum = 0;
        _tx.GoThrough(Table, (ref RowView r) =>
        {
            if (OrderDoc.GetScore(ref r) == 3)
                sum += OrderDoc.GetOrderNo(ref r);
            return true;
        });
        if (DataVarManager.GetDataVarCallCount != 0)
            throw new InvalidOperationException("Var touched during Score filter");
        return sum;
    }

    [Benchmark]
    public int FindEntity_WhereScore()
    {
        DataVarManager.GetDataVarCallCount = 0;
        var hits = _tx.Find<OrderDoc>(Table, (ref RowView r) => OrderDoc.GetScore(ref r) == 3);
        var predCalls = DataVarManager.GetDataVarCallCount;
        // Materialization touches Body for each hit — pred phase should be 0 before materialize,
        // but Find materializes immediately; count equals hit count.
        return hits.Values.Count();
    }
}
