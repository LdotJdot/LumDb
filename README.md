# LumDb 2.2.2

A single-file, thread-safe embedded database for .NET 10. 100% C#, AOT-friendly, no native dependencies.

On-disk format **1.3.9** (`DbHeader.VERSION = 1_003_009`). Packed as `major * 1_000_000 + minor * 1_000 + patch`. Bump that constant only when the layout or engine contract changes — there is no runtime “append version” API. `DbEngine.Version` / `VersionString` expose the file format (`"1.3.9"`). The NuGet package version (2.2.0) is independent.

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
[LumEntity]
public partial class Student
{
    [Id] public uint Id { get; set; }          // engine auto row id, not a column
    [Key] [Str32B] public string Name { get; set; } = "";
    public int Age { get; set; }
    public bool Active { get; set; }
}
```

Unsupported nested members on `[LumEntity]` are skipped with warning `LUMDB002`. `[Ignore]` is silent. DateTime columns are UTC (`DateTimeKind.Utc`).

Tuple / `DbCell` API (no entity type):

```csharp
ts.Create("kv", [("k", DbValueType.Int, true), ("v", DbValueType.StrVar, false)]);
ts.Insert("kv", [("k", 1), ("v", "hello")]);
var row = ts.Find("kv", "k", 1);
Console.WriteLine(row.Row.GetString(1));
```

## Query (LINQ)

`Where` / `Join` / `Skip` / `Take` only build a plan (no I/O). `ToList` / `Count` / `Delete` / `Exists` run the whole plan under one transaction lock. The thread that called `Query()` must consume that instance; other threads call `Query()` themselves.

Scan uses `RowView` (no entity `new` until the result window). `Count` and `WhereExists` do not materialize inner rows.

```csharp
using (var ts = eng.StartTransaction())
{
    var page = ts.Query<Student>("students")
        .Where(s => s.Active && s.Name.Trim().ToUpper().StartsWith("LJ"))
        .OrderByDescending(s => s.Age)
        .Skip(0)
        .Take(20)
        .ToList();

    var n = ts.Query<Student>("students").Where(s => s.Age >= 18).Count();

    var pairs = ts.Query<Student>("students")
        .Join<Order>("orders", (s, o) => s.Id == o.StudentId && o.Status == 1)
        .Where((s, o) => o.Amount >= 10)
        .ToList(); // List<(Student Outer, Order Inner)>

    var withOrders = ts.Query<Student>("students")
        .WhereExists<Order>("orders", (s, o) => s.Id == o.StudentId)
        .ToList();
}
```

Hand-written `IDbEntity`: `Query_Entity<T>` / `JoinEntity`. Three-table: `.Join<A>(...).Join<B>(...)` → `List<(T1 A, T2 B, T3 C)>`.

### Expressions

Translated to a `RowView` tree (AOT: no `Expression.Compile` of the whole lambda):

- Comparisons `== != < <= > >=`, logic `&& || !`, arithmetic `+ - * / %`
- String (Ordinal / Invariant, **one-argument** overloads): `StartsWith` / `EndsWith` / `Contains` / `Equals`, `ToUpper` / `ToLower` (and `*Invariant`), `Trim` / `TrimStart` / `TrimEnd`, `Substring`, `Replace`, `IndexOf` / `LastIndexOf`, `Length`, `IsNullOrEmpty` / `IsNullOrWhiteSpace`, `ToString()`
- `Math`: `Abs` `Sign` `Min` `Max` `Clamp` `Floor` `Ceiling` `Truncate` `Round` `Sqrt` `Pow` `Sin` `Cos` `Tan` `Log` `Log10` `Exp`
- Nesting is allowed: `s.Name.Trim().ToUpper().Contains("LJ")`, `Math.Abs(Add(s.Age, 0))`

Your own methods and captured `Func` (any `TResult`, not `void` / `Action`) are invoked per matching row:

```csharp
static int Add(int a, int b) => a + b;
static string Fold(string s) => s.Trim().ToUpperInvariant();

ts.Query<Student>("students").Where(s => Add(s.Age, 1) > 18);
ts.Query<Student>("students").Where(s => Fold(s.Name).Contains("LJ"));

Func<string, bool> rx = n => System.Text.RegularExpressions.Regex.IsMatch(n, "^lj");
ts.Query<Student>("students").Where(s => rx(s.Name));
```

C# rejects `Action` in `Where` (predicate must be `bool`). Local functions inside the method cannot appear in expression trees (CS8110). `System.*` instance APIs that are not in the list (`Split`, `Contains(..., StringComparison)`, `ToUpper(culture)`, ternary `?:`) compile, then throw `LumException` at `ToList` / `Count`.

Zero-alloc predicate: `Where((ref RowView row) => row.GetInt(1) > 90)`. Entity callback (allocates every scanned row): `WhereCallback(s => ...)`.

### Performance notes

- **Count / Take / Join / WhereExists** vs loading every entity (`Find` all, or skipping by `new T()`): scan stays on `RowView`; only the result window is boxed. That is the large win (see `BenchLumDb` GoThrough vs object scan, entity page vs `Find` all).
- Whitelist string/Math nesting is the same `RowOp` path as a simple comparison.
- Captured `Func` and user `MethodInfo.Invoke` run managed IL per row — correct, not faster than `RowOp`.
- Equi-join is hash; inequality join is nested-loop over inner ids (`O(N×M)` CPU, still no full-table entity alloc). `OrderBy` on a join materializes **all matching ids** before `Skip`/`Take`.

## Features

- Relational tables with optional multi-key secondary indexes
- Types: bool, byte, int/uint, long/ulong, float/double, decimal, DateTime UTC, fixed/var string, fixed/var bytes
- Fluent `Query` / `Join` / `WhereExists` (one lock per terminal)
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
