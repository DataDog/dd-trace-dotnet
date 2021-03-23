namespace Datadog.Trace.Tools.Runner.Crank
{
    internal readonly struct UsTimeResultConverter : IResultConverter
    {
        public readonly string Name;

        public UsTimeResultConverter(string name)
        {
            Name = name;
        }

        public bool CanConvert(string name)
            => Name == name;

        public void SetToSpan(Span span, string sanitizedName, object value)
        {
            if (double.TryParse(value.ToString(), out var doubleValue))
            {
                span.SetMetric(sanitizedName + "_μs", doubleValue * 1_000);
            }
            else
            {
                span.SetTag(sanitizedName, value.ToString());
            }
        }
    }
}
