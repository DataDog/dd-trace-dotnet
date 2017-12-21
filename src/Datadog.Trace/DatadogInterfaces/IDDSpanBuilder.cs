namespace Datadog.Trace
{
    public interface IDDSpanBuilder
    {
        IDDSpanBuilder AsChildOf(IDDSpan parent);

        IDDSpanBuilder AsChildOf(IDDSpanContext parent);

        IDDSpanBuilder WithTag(string key, string value);

        IDDSpanBuilder IgnoreActiveSpan();

        IDDScope StartActive();

        IDDScope StartActive(bool finishSpanOnClose);

        IDDSpan StartManual();
    }
}
