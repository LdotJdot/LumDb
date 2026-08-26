# LumDb 2.1.0

A single-file, thread-safe embedded database for .NET 10. 100% C#, AOT-friendly, no native dependencies.

On-disk format **1.3.9** (`DbHeader.VERSION = 1_003_009`). Packed as `major * 1_000_000 + minor * 1_000 + patch`. Bump that constant when the layout or engine contract changes — there is no runtime “append version” API. `DbEngine.Version` / `VersionString` expose the current value (`"1.3.9"`).

## Engines

```csharp
using var mem = new DbEngine();              // in-memory; same instance shares one committed image
using var disk = new DbEngine(@"d:\app.db"); // file + WAL

using (var ts = mem.StartTransaction())
{
    ts.Create<Student>("students");
    ts.Insert("students", new Student { Name = "lj", Age = 20 });
} // Dispose commits. The next StartTransaction on this engine sees the data.

mem.SaveTo(@"d:\app.db");                    // dump committed image (not uncommitted dirty pages)
```

| | Memory `new DbEngine()` | File `new DbEngine(path)` |
|---|---|---|
| Persistence | Process lifetime | Disk + `.log` WAL |
| Cross-transaction | Same instance shares the committed image | Same, via the file |
| `Discard` | Drops the current tx only | Same |
| Crash recovery | N/A | Replay `.log` |
| `SaveTo(path)` | Full committed dump | File copy of committed image |

`SaveChanges` / `Dispose` commit. `Discard` rolls back the current transaction and reloads from the committed image. A following `Dispose` does not overwrite the image with an empty cache.

## LumEntity (source generator)

Mark a **partial** class. Build generates `ILumEntity<T>` (works without PublishAOT — generation is compile-time).

```csharp
using LumDbEngine;
using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Structure;

[LumEntity]
public partial class Student
{
    [Id] public uint Id { get; set; }          // engine auto row id, not a column
    [Key] [Str32B] public string Name { get; set; } = "";
    public int Age { get; set; }
}

const string Table = "students";

using var eng = new DbEngine("demo.db");
using (var ts = eng.StartTransaction())
{
    ts.Create<Student>(Table);
    ts.Insert(Table, new Student { Name = "lj99", Age = 99 });
    ts.Insert(Table, new Student { Name = "lj0", Age = 0 });
}

using (var ts = eng.StartTransaction())
{
    var byKey = ts.FindEntity<Student>(Table, "Name", "lj99");
    Console.WriteLine($"{byKey.Value.Id}, {byKey.Value.Name}, {byKey.Value.Age}");

    ts.GoThrough(Table, (ref RowView row) =>
    {
        if (row.GetInt(1) > 90)
            Console.WriteLine(row.GetString(0));
        return true; // continue
    });
}
```

Tuple / `DbCell` API (no entity type):

```csharp
ts.Create("kv", [("k", DbValueType.Int, true), ("v", DbValueType.StrVar, false)]);
ts.Insert("kv", [("k", 1), ("v", "hello")]);
var row = ts.Find("kv", "k", 1);
Console.WriteLine(row.Row.GetString(1));
ts.Update("kv", 1u, "v", "world");   // by engine row id
ts.Delete("kv", "k", 1);
```

Unsupported nested members on `[LumEntity]` are skipped with warning `LUMDB002`. `[Ignore]` is silent. DateTime columns are UTC (`DateTimeKind.Utc`).

## Features

- Relational tables with optional multi-key secondary indexes
- Types: bool, byte, int/uint, long/ulong, float/double, decimal, DateTime UTC, fixed/var string, fixed/var bytes
- Read-committed: a readonly transaction sees the last commit, not another writer’s dirty pages
- Thread-safe: one writer (upgradeable) + concurrent readers
- Source-generated entities; `GoThrough` / `RowView` scan without allocating entities
- AOT publish supported (`PublishAot`); daily `dotnet build` still runs the generator
- Tools: **LumDbExplorer** (WinForms) to create/copy databases, create tables, CRUD rows, inspect schema and stats

## Getting started

1. Reference `LumDbEngine` (net10). For `[LumEntity]`, the analyzer is packed with the NuGet.
2. `new DbEngine()` or `new DbEngine(path)`.
3. `using var ts = engine.StartTransaction();` — dispose to commit, or `Discard()`.

## License

MIT. See [LICENSE.txt](LICENSE.txt).
