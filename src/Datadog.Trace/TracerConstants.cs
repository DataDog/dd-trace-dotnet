namespace Datadog.Trace
{
    internal static class TracerConstants
    {
        public const string Language = "dotnet";

        /// <summary>
        /// 2^63-1
        /// </summary>
        public const ulong MaxTraceId = 9_223_372_036_854_775_807;

        public static readonly string AssemblyVersion = typeof(Tracer).Assembly.GetName().Version.ToString();
    }
}
