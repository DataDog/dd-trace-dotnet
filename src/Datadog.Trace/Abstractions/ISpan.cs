using System;

namespace Datadog.Trace.Abstractions
{
    internal interface ISpan
    {
        string ResourceName { get; set; }

        string Type { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this span represents an error.
        /// </summary>
        bool Error { get; set; }

        ISpan SetTag(string key, string value);

        string GetTag(string key);

        void SetException(Exception exception);
    }
}
