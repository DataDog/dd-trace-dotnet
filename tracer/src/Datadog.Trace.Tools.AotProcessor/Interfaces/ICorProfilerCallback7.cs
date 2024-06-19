namespace Datadog.Trace.Tools.AotProcessor.Interfaces;

[NativeObject]
internal unsafe interface ICorProfilerCallback7 : ICorProfilerCallback6
{
    public static new readonly Guid Guid = Guid.Parse("F76A2DBA-1D52-4539-866C-2AA518F9EFC3");

    // This event is triggered whenever the symbol stream associated with an
    // in-memory module is updated. Even when symbols are provided up-front in
    // a call to the managed API Assembly.Load(byte[], byte[], ...) the runtime
    // may not actually associate the symbolic data with the module until after
    // the ModuleLoadFinished callback has occurred. This event provides a later
    // opportunity to collect symbols for such modules.
    //
    // This event is controlled by the COR_PRF_HIGH_IN_MEMORY_SYMBOLS_UPDATED
    // event mask flag.
    //
    // Note: This event is not currently raised for symbols implicitly created or
    // modified via Reflection.Emit APIs.
    HResult ModuleInMemorySymbolsUpdated(ModuleId moduleId);
}
