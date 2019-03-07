using System;

namespace Datadog.Trace.Interfaces
{
    internal interface ISpan
    {
        string ResourceName { get; set; }

        string Type { get; set; }

        ISpan SetTag(string key, string value);

        string GetTag(string key);

        void SetException(Exception exception);
    }
}
