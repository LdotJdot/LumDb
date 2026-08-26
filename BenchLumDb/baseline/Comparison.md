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
