namespace Datadog.Trace.Tools.AotProcessor.Interfaces;

[NativeObject]
internal interface IClassFactory : IUnknown
{
    public static new readonly Guid Guid = new("00000001-0000-0000-C000-000000000046");

    HResult CreateInstance(IntPtr outer, in Guid guid, out IntPtr instance);

    HResult LockServer(bool @lock);
}
