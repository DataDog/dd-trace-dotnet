namespace Datadog.Trace.ClrProfiler.Interfaces
{
    internal interface ISpanDecorationBuilder
    {
        ISpanDecorationBuilder With(ISpanDecorator decoration);

        ISpanDecorator Build();
    }
}
