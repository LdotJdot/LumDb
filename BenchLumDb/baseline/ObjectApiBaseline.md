# Object API Baseline (pre DbCell migration)

Captured: 2026-08-25  
Machine: 11th Gen Intel Core i7-11800H, .NET 8.0.28  
Config: IterationCount=5, WarmupCount=1, MemoryDiagnoser  
Dataset: 2000 rows (Int key + Long + Decimal + DateTimeUTC + Str32B + StrVar)

| Method                   | Mean         | Allocated  |
|------------------------- |-------------:|-----------:|
| InsertOne_ObjectApi      |   657.822 us |  115.87 KB |
| FindByKey_ObjectApi      |     1.247 us |    1.04 KB |
| FindById_ObjectApi       |     1.088 us |    1.05 KB |
| FindConditions_ObjectApi |    98.271 us |  120.48 KB |
| GoThrough_ObjectApi      | 1,119.910 us | 1414.19 KB |

Re-run: `dotnet run --project BenchLumDb -c Release -- --filter "*ObjectApiBaselineBench*"`
