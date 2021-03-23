namespace Datadog.Trace.Tools.Runner.Crank
{
    internal readonly struct NumberResultConverter : IResultConverter
    {
        public readonly string Name;

        public NumberResultConverter(string name)
        {
            Name = name;
        }

        public bool CanConvert(string name)
            => Name == name;

        public void SetToSpan(Span span, string sanitizedName, object value)
        {
            if (double.TryParse(value.ToString(), out var doubleValue))
            {
                span.SetMetric(sanitizedName, doubleValue);
            }
            else
            {
                span.SetTag(sanitizedName, value.ToString());
            }
        }
    }
}
