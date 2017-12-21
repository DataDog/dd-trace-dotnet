using System;

namespace Datadog.Trace
{
    public interface IDDSpan : IDisposable
    {
        IDDSpanContext Context { get; }

        string OperationName { get; set; }

        string ResourceName { get; set; }

        string ServiceName { get; }

        void SetTag(string name, string value);

        void Finish();
    }
}
