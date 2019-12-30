namespace Datadog.Trace.Sampling
{
    internal interface IRateLimiter
    {
        bool Allowed(Span span);

        float GetEffectiveRate();
    }
}
