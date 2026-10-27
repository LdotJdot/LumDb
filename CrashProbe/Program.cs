using System.Diagnostics;
using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Structure;

// 崩溃探针：持续小事务写入并逐条打印已提交序号；父进程在随机时刻强杀。
// 用法: CrashProbe <dbPath> <durationMs>

const string Table = "crash";

if (args.Length < 2)
{
    Console.Error.WriteLine("usage: CrashProbe <dbPath> <durationMs>");
    return 2;
}

var dbPath = args[0];
var durationMs = int.Parse(args[1]);

static string Payload(int k) => new string((char)('a' + k % 26), 80) + ":" + k;

try
{
    using var eng = new DbEngine(dbPath);
    using (var ts = eng.StartTransaction())
    {
        ts.Create(Table, [("k", DbValueType.Int, true), ("pad", DbValueType.StrVar, false)]);
        ts.SaveChanges();
    }

    int k = 0;
    var sw = Stopwatch.StartNew();
    while (sw.ElapsedMilliseconds < durationMs)
    {
        using (var ts = eng.StartTransaction())
        {
            ts.Insert(Table, [("k", k), ("pad", Payload(k))]);
            ts.SaveChanges();
        }
        Console.WriteLine("C " + k);
        Console.Out.Flush();
        k++;
    }

    Console.WriteLine("DONE " + k);
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine("ERR " + ex.GetType().Name + ": " + ex.Message);
    return 3;
}