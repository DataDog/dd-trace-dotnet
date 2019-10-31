namespace Datadog.Trace.Sampling
{
    internal interface IRateLimiter
    {
        bool Allowed(ulong traceId);

        float GetEffectiveRate();
    }
}
