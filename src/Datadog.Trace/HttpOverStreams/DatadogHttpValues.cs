using System;

namespace Datadog.Trace.HttpOverStreams
{
    internal static class DatadogHttpValues
    {
        public const char CarriageReturn = '\r';
        public static readonly string NewLine = Environment.NewLine;
        public static readonly int CrLfLength = NewLine.Length;
    }
}
