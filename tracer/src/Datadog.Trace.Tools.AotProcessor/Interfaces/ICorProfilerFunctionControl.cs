using System.Security.Cryptography;

namespace Datadog.Trace.Tools.AotProcessor.Interfaces;

[NativeObject]
internal unsafe interface ICorProfilerFunctionControl : IUnknown
{
    public static new readonly Guid Guid = new("F0963021-E1EA-4732-8581-E01B0BD3C0C6");

    HResult SetCodegenFlags(int flags);

    HResult SetILFunctionBody(uint cbNewILMethodHeader, IntPtr pbNewILMethodHeader);

    HResult SetILInstrumentedCodeMap(uint cILMapEntries, CorIlMap* rgILMapEntries);
}
