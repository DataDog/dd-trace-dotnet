using Datadog.Trace.Interfaces;

namespace Datadog.Trace.Services
{
    internal static class SimpleDependencyFactory
    {
        internal static IRandomProvider RandomProvider() => AsyncLocalGuidSeedRandomProvider.Instance;

        internal static IIdProvider IdProvider() => RandomIdProvider.Instance;
    }
}
