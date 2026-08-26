using BenchmarkDotNet.Running;
using BenchLumDb;

BenchmarkSwitcher.FromAssembly(typeof(ObjectApiBaselineBench).Assembly).Run(args);
