#pragma warning disable SA1201 // Elements should appear in the correct order

namespace Datadog.Trace.Tools.Runner.Crank
{
    internal interface IResultConverter
    {
        bool CanConvert(string name);

        void SetToSpan(Span span, string sanitizedName, object value);
    }
}
