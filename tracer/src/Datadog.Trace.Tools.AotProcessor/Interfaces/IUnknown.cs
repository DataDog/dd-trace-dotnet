namespace Datadog.Trace.Tools.AotProcessor.Interfaces;

[NativeObject]
internal interface IUnknown
{
    public static readonly Guid Guid = new("00000000-0000-0000-C000-000000000046");

    HResult QueryInterface(in Guid guid, out IntPtr ptr);
    int AddRef();
    int Release();
}
