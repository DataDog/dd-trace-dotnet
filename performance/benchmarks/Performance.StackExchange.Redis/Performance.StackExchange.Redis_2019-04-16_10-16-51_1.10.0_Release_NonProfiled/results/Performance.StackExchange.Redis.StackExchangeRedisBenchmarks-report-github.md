``` ini

BenchmarkDotNet=v0.12.0, OS=Windows 10.0.18362
Intel Core i7-8750H CPU 2.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
  [Host]     : .NET Framework 4.8 (4.8.4042.0), X64 RyuJIT
  Job-YRNBIY : .NET Framework 4.8 (4.8.4042.0), X64 RyuJIT

IterationCount=100  LaunchCount=1  WarmupCount=0  

```
|             Method |     Mean |   Error |   StdDev |      Min |      Max |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|------------------- |---------:|--------:|---------:|---------:|---------:|-------:|------:|------:|----------:|
| EvalSetLargeString | 508.3 us | 8.66 us | 24.15 us | 464.8 us | 573.1 us | 1.9531 |     - |     - |  10.55 KB |
