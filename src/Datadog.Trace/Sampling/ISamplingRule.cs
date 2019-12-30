namespace Datadog.Trace.Sampling
{
    internal interface ISamplingRule
    {
        string RuleName { get; }

        int Priority { get; }

        bool IsMatch(Span span);

        float GetSamplingRate(Span span);
    }
}
