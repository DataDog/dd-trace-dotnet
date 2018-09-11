namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class BuildParameters
    {
#if DEBUG
        public const string Configuration = "Debug";
#else
        public const string Configuration = "Release";
#endif

#if NETFRAMEWORK
        public const bool CoreClr = false;
#else
        public const bool CoreClr = true;
#endif

#if NET452
        public const string TargetFramework = "net452";
#elif NET461
        public const string TargetFramework = "net461";
#elif NET47
        public const string TargetFramework = "net47";
#elif NETCOREAPP2_0
        public const string TargetFramework = "netcoreapp2.0";
#endif
    }
}
