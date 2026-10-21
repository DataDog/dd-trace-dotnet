using System;
using System.Reflection;
using ReadyToRunInliner;

if (!Samples.SampleHelpers.IsProfilerAttached())
{
    Console.WriteLine("Error: Profiler is required and is not loaded.");
    return 1;
}

// This module loaded before the tracer started, so its Target is instrumented after the module loaded.
Console.WriteLine($"InstrumentedAfterModuleLoad.Caller: {InstrumentedAfterModuleLoad.Caller(1)}");

// Nothing referenced the library before, so its Target is instrumented when its module loads here.
var type = Assembly.Load("ReadyToRunInliner.Lib").GetType("ReadyToRunInliner.Lib.InstrumentedAtModuleLoad");
Console.WriteLine($"InstrumentedAtModuleLoad.Caller: {type.GetMethod("Caller").Invoke(null, new object[] { 1 })}");

Console.WriteLine("App completed successfully");
return 0;
