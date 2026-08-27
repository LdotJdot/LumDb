using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using LumDbEngine;
using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Structure;
using LumDbEngine.Extension.DbEntity;

namespace BenchLumDb;

/// <summary>
/// Entity search / pagination vs RowView GoThrough. Same 2000-row table as DbCellApiBench.
/// LumEntity Find uses RowView skip/limit (no backward API).
/// IDbEntity Find_Entity uses LINQ on a pull iterator (forward / backward).
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 1, iterationCount: 5)]
[Config(typeof(Cfg))]
public partial class EntitySearchBench
{
    private const string Table = "bench";
    private const int RowCount = 2000;
    private const int PageSize = 20;
    private const int MidSkip = 1000;
    private const int LastSkip = 1980;

    private string _path = null!;
    private DbEngine _engine = null!;
    private ITransaction _tx = null!;

    [LumEntity]
    public partial class SearchRow
    {
        [Id] public uint Id { get; set; }
        [Key] public int Uid { get; set; }
        public long Score { get; set; }
        public decimal Pay { get; set; }
        public DateTime When { get; set; }
        [Str32B] public string Name { get; set; } = "";
        [StrVar] public string Note { get; set; } = "";
    }

    public sealed class HandRow : IDbEntity
    {
        public int Uid;
        public long Score;
        public decimal Pay;
        public DateTime When;
        public string Name = "";
        public string Note = "";

        public void WriteTo(ref RowWriter writer)
        {
            writer.WriteInt(Uid);
            writer.WriteLong(Score);
            writer.WriteDecimal(Pay);
            writer.WriteDateTimeUtc(When);
            writer.WriteString(Name);
            writer.WriteString(Note);
        }

        public bool TryReadFrom(IDbRow row)
        {
            Uid = row.GetInt(0);
            Score = row.GetLong(1);
            Pay = row.GetDecimal(2);
            When = row.GetDateTimeUtc(3);
            Name = row.GetString(4);
            Note = row.GetString(5);
            return true;
        }
    }

    private sealed class Cfg : ManualConfig
    {
        public Cfg() => WithOptions(ConfigOptions.DisableOptimizationsValidator);
    }

    [GlobalSetup]
    public void Setup()
    {
        _path = Path.Combine(Path.GetTempPath(), "lumdb_bench_ent_" + Guid.NewGuid().ToString("N") + ".db");
        _engine = new DbEngine(_path);
        using var ts = _engine.StartTransaction();
        ts.Create<SearchRow>(Table);

        var utc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < RowCount; i++)
        {
            ts.Insert(Table, new SearchRow
            {
                Uid = i,
                Score = (long)i * i,
                Pay = 1.2300m + i,
                When = utc.AddSeconds(i),
                Name = "user" + i,
                Note = "note-content-" + i,
            });
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

    [Benchmark]
    public int LumEntity_FindAll()
    {
        var r = _tx.Find<SearchRow>(Table, (ref RowView _) => true);
        return r.Values.Count;
    }

    [Benchmark]
    public int LumEntity_PageFwd_First20()
        => _tx.Find<SearchRow>(Table, (ref RowView _) => true, skip: 0, limit: PageSize).Values.Count;

    [Benchmark]
    public int LumEntity_PageFwd_Mid20()
        => _tx.Find<SearchRow>(Table, (ref RowView _) => true, skip: MidSkip, limit: PageSize).Values.Count;

    [Benchmark]
    public int LumEntity_PageFwd_Last20()
        => _tx.Find<SearchRow>(Table, (ref RowView _) => true, skip: LastSkip, limit: PageSize).Values.Count;

    [Benchmark]
    public int IDbEntity_LinqAll()
        => _tx.Query_Entity<HandRow>(Table).ToValues().Values.Count;

    [Benchmark]
    public int IDbEntity_LinqFwd_First20()
        => _tx.Query_Entity<HandRow>(Table).Take(PageSize).ToValues().Values.Count;

    [Benchmark]
    public int IDbEntity_LinqFwd_Mid20()
        => _tx.Query_Entity<HandRow>(Table).Skip(MidSkip).Take(PageSize).ToValues().Values.Count;

    [Benchmark]
    public int IDbEntity_LinqFwd_Last20()
        => _tx.Query_Entity<HandRow>(Table).Skip(LastSkip).Take(PageSize).ToValues().Values.Count;

    [Benchmark]
    public int IDbEntity_LinqBwd_First20()
        => _tx.Query_Entity<HandRow>(Table).Reverse().Take(PageSize).ToValues().Values.Count;

    [Benchmark]
    public int IDbEntity_LinqBwd_Mid20()
        => _tx.Query_Entity<HandRow>(Table).Reverse().Skip(MidSkip).Take(PageSize).ToValues().Values.Count;

    [Benchmark]
    public int IDbEntity_LinqBwd_Last20()
        => _tx.Query_Entity<HandRow>(Table).Reverse().Skip(LastSkip).Take(PageSize).ToValues().Values.Count;
}
