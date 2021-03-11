namespace Datadog.Trace.Tools.Runner.Crank
{
    internal class BombardierRawConverter : IResultConverter
    {
        public BombardierRawConverter(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public bool CanConvert(string name)
            => Name == name;

        public void SetToSpan(Span span, string sanitizedName, object value)
        {
            span.SetTag(sanitizedName, value.ToString());
        }
    }
}
