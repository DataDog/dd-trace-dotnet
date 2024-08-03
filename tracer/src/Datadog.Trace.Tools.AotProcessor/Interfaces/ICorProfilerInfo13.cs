namespace Datadog.Trace.Tools.AotProcessor.Interfaces;

[NativeObject]
internal unsafe interface ICorProfilerInfo13 : ICorProfilerInfo12
{
    public static new readonly Guid Guid = new("6E6C7EE2-0701-4EC2-9D29-2E8733B66934");

    HResult CreateHandle(
        ObjectId @object,
        COR_PRF_HANDLE_TYPE type,
        out ObjectHandleId pHandle);

    HResult DestroyHandle(
        ObjectHandleId handle);

    HResult GetObjectIDFromHandle(
        ObjectHandleId handle,
        out ObjectId pObject);
}
