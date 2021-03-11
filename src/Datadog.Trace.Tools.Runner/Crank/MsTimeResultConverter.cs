namespace Datadog.Trace.Tools.Runner.Crank
{
    internal readonly struct MsTimeResultConverter : IResultConverter
    {
        public readonly string Name;

        public MsTimeResultConverter(string name)
        {
            Name = name;
        }

        public bool CanConvert(string name)
            => Name == name;

        public void SetToSpan(Span span, string sanitizedName, object value)
        {
            if (double.TryParse(value.ToString(), out var doubleValue))
            {
                span.SetMetric(sanitizedName + "_ns", doubleValue * 1_000_000);
            }
            else
            {
                span.SetTag(sanitizedName, value.ToString());
            }
        }
    }
}
