namespace Datadog.Trace.Tools.AotProcessor.Interfaces;

[NativeObject]
internal unsafe interface ICorProfilerModuleEnum : IUnknown
{
    HResult Skip(uint celt);

    HResult Reset();

    HResult Clone(out void* ppEnum);

    HResult GetCount(out uint pcelt);

    HResult Next(uint celt, ModuleId* ids, out uint pceltFetched);
}
