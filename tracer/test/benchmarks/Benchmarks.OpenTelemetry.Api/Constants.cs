#if INSTRUMENTEDAPI
namespace Benchmarks.OpenTelemetry.InstrumentedApi;
#else
namespace Benchmarks.OpenTelemetry.Api;
#endif

public class Constants
{
    public const string TracerCategory = "tracer";
}
