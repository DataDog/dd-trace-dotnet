namespace Datadog.Trace.Tools.AotProcessor.Interfaces;

[NativeObject]
internal unsafe interface ICorProfilerCallback10 : ICorProfilerCallback9
{
    public static new readonly Guid Guid = Guid.Parse("CEC5B60E-C69C-495F-87F6-84D28EE16FFB");

    // This event is triggered whenever an EventPipe event is configured to be delivered.
    //
    // Documentation Note: All pointers are only valid during the callback

    HResult EventPipeEventDelivered(
        IntPtr provider,
        int eventId,
        int eventVersion,
        uint cbMetadataBlob,
        byte* metadataBlob,
        uint cbEventData,
        byte* eventData,
        in Guid pActivityId,
        in Guid pRelatedActivityId,
        ThreadId eventThread,
        uint numStackFrames,
        nint* stackFrames);

    HResult EventPipeProviderCreated(IntPtr provider);
}
