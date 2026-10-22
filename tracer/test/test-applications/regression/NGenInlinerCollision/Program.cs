using System;
using System.Linq;
using NGenInlinerCollision;

if (!Samples.SampleHelpers.IsProfilerAttached())
{
    Console.WriteLine("Error: Profiler is required and is not loaded.");
    return 1;
}

// System.Core is NGen'd, so calling it also raises the CLR callbacks that look up NGen inliners.
var result = Enumerable.Range(0, 3).Select(Targets.M0001).Sum();
Console.WriteLine($"Result: {result}");

Console.WriteLine("App completed successfully");
return 0;
