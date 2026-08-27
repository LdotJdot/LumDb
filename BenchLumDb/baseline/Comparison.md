# Benchmark comparison (same machine / config)

## Baseline (pre-change, object API) — 2026-08-25

| Method                   | Mean         | Allocated  |
|------------------------- |-------------:|-----------:|
| InsertOne_ObjectApi      |   657.822 us |  115.87 KB |
| FindByKey_ObjectApi      |     1.247 us |    1.04 KB |
| FindById_ObjectApi       |     1.088 us |    1.05 KB |
| FindConditions_ObjectApi |    98.271 us |  120.48 KB |
| GoThrough_ObjectApi      | 1,119.910 us | 1414.19 KB |

## After Span-backed RowView + DbRowBuffer Find — DbCellApiBench (re-run 2026-08-25)

| Method                       | Mean         | Allocated | vs object[] scan |
|----------------------------- |-------------:|----------:|------------------|
| InsertOne_DbCell             |   669 us*    |  ~116 KB | (Discard noise) |
| FindByKey_DbCell             |    1.12 us  |  ~1.1 KB | similar / slightly better |
| FindById_Row                 |    0.92 us  |  ~0.9 KB | similar / slightly better |
| **GoThrough_RowView**        | **14.9 us** | **208 B** | **~80× faster, ~6500× less alloc** |
| GoThrough_ObjectArray_Compat |  1,188 us   | ~1.34 MB | baseline-like |

\*Insert variance high due to Discard; not the focus of this pass.

### Takeaway

Span `RowView` that reads columns directly from the row buffer (no per-row `DbCell[]` / `object[]`) is where scan performance finally moves. Boxing removal alone was not enough; **eliminating per-row heap materialization** was. Find path with `DbRowBuffer` stays allocation-competitive with the old object Find.

Re-run:
`dotnet run --project BenchLumDb -c Release -- --filter "*DbCellApiBench*"`

## Entity search / pagination — EntitySearchBench (2026-08-26)

Same 2000-row table as GoThrough (uid / score / pay / when / name / note). Page size = 20.

This is the first recorded entity-search baseline (no earlier pagination numbers to compare against).

| Method | Mean | Allocated | What it does |
| --- | ---: | ---: | --- |
| GoThrough_RowView | 13.89 µs | 296 B | scan, no entity |
| LumEntity_FindAll | 1,787 µs | 1962 KB | `Find<T>(true)` materialize all |
| LumEntity_PageFwd_First20 | 13.91 µs | 20 KB | skip 0 / take 20 |
| LumEntity_PageFwd_Mid20 | 23.18 µs | 20 KB | skip 1000 / take 20 |
| LumEntity_PageFwd_Last20 | 29.41 µs | 20 KB | skip 1980 / take 20 |
| IDbEntity_LinqAll | 1,081 µs | 1758 KB | `Find_Entity(rows => rows)` |
| IDbEntity_LinqFwd_First20 | 10.68 µs | 18 KB | `Take(20)` forward |
| IDbEntity_LinqFwd_Mid20 | 530 µs | 885 KB | `Skip(1000).Take(20)` forward |
| IDbEntity_LinqFwd_Last20 | 1,047 µs | 1743 KB | `Skip(1980).Take(20)` forward |
| IDbEntity_LinqBwd_First20 | 11.54 µs | 18 KB | `Take(20)` backward (newest) |
| IDbEntity_LinqBwd_Mid20 | 504 µs | 893 KB | `Skip(1000).Take(20)` backward |
| IDbEntity_LinqBwd_Last20 | 1,017 µs | 1743 KB | `Skip(1980).Take(20)` backward |

LumEntity `Find(predicate, skip, limit)` has **no backward API**. Skip is on `RowView` (does not `TryReadFrom` skipped rows), so mid/last page stay ~20 KB. IDbEntity LINQ `Skip` still materializes skipped entities, so deep pages grow toward a full-table scan.

Re-run:
`dotnet run --project BenchLumDb -c Release -- --filter "*EntitySearchBench*"`
