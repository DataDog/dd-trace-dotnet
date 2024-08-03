namespace Datadog.Trace.Tools.AotProcessor.Interfaces;

[NativeObject]
internal unsafe interface ICorProfilerInfo5 : ICorProfilerInfo4
{
    public static new readonly Guid Guid = new("07602928-CE38-4B83-81E7-74ADAF781214");

    /*
     * The code profiler calls GetEventMask2 to obtain the current event
     * categories for which it is to receive event notifications from the CLR
     *
     * *pdwEventsLow is a bitwise combination of values from COR_PRF_MONITOR
     * *pdwEventsHigh is a bitwise combination of values from COR_PRF_HIGH_MONITOR
     */
    HResult GetEventMask2(
        out CorPrfMonitor pdwEventsLow,
        out CorPrfHighMonitor pdwEventsHigh);

    /*
     * The code profiler calls SetEventMask2 to set the event categories for
     * which it is set to receive notification from the CLR.
     *
     * dwEventsLow is a bitwise combination of values from COR_PRF_MONITOR
     * dwEventsHigh is a bitwise combination of values from COR_PRF_HIGH_MONITOR
     */
    HResult SetEventMask2(
        CorPrfMonitor dwEventsLow,
        CorPrfHighMonitor dwEventsHigh);
}
