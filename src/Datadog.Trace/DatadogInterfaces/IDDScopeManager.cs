namespace Datadog.Trace
{
    public interface IDDScopeManager
    {
        IDDScope Active { get; }

        IDDScope Activate(IDDSpan span);

        IDDScope Activate(IDDSpan span, bool finishSpanOnClose);
    }
}
