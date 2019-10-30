namespace Datadog.Trace.Sampling
{
    internal interface ISamplingRule
    {
        string Name { get; }

        int Priority { get; }

        bool IsMatch(Span span);

        float GetSamplingRate();
    }
}
