public static class Constants
{
    /// <summary>
    /// The name of the native loader file (without the extension dll/so/dylib)
    /// The name of this file is fixed for backwards compatibility reasons
    /// </summary>
    public const string NativeLoaderFilename = "Datadog.Trace.ClrProfiler.Native";

    public const string NativeTracerFilename = "Datadog.Tracer.Native";
    public const string NativeProfilerFilename = "Datadog.Profiler.Native";
    public const string NativeDebuggerFilename = "Datadog.Debugger.Native";

    public const string LoaderConfFilename = "loader.conf";
}
