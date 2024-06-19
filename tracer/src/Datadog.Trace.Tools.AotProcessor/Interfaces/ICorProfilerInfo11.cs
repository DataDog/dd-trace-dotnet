namespace Datadog.Trace.Tools.AotProcessor.Interfaces;

[NativeObject]
internal unsafe interface ICorProfilerInfo11 : ICorProfilerInfo10
{
    public static new readonly Guid Guid = new("06398876-8987-4154-B621-40A00D6E4D04");

    /*
     * Get environment variable for the running managed code.
     */
    HResult GetEnvironmentVariable(
        char* szName,
        uint cchValue,
        out uint pcchValue,
        char* szValue);

    /*
     * Set environment variable for the running managed code.
     *
     * The code profiler calls this function to modify environment variables of the
     * current managed process. For example, it can be used in the profiler's Initialize()
     * or InitializeForAttach() callbacks.
     *
     * szName is the name of the environment variable, should not be NULL.
     *
     * szValue is the contents of the environment variable, or NULL if the variable should be deleted.
     */
    HResult SetEnvironmentVariable(
        char* szName,
        char* szValue);
}
