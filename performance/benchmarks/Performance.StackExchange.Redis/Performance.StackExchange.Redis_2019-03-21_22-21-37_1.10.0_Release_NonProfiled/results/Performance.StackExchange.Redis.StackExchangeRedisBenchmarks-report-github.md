``` ini

BenchmarkDotNet=v0.12.0, OS=Windows 10.0.18362
Intel Core i7-8750H CPU 2.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
  [Host]     : .NET Framework 4.8 (4.8.4042.0), X64 RyuJIT  [AttachedDebugger]
  Job-XZSNKY : .NET Framework 4.8 (4.8.4042.0), X64 RyuJIT

Jit=RyuJit  Platform=X64  Runtime=.NET 4.8  
IterationCount=1000  LaunchCount=1  WarmupCount=0  

```
|             Method |     Mean |    Error |   StdDev |   Median |      Min |        Max |
|------------------- |---------:|---------:|---------:|---------:|---------:|-----------:|
| EvalSetLargeString | 813.2 us | 11.12 us | 105.1 us | 789.9 us | 622.4 us | 1,110.7 us |
