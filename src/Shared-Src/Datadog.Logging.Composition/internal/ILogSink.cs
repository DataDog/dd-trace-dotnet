using System;

namespace Datadog.Logging.Composition
{
    internal interface ILogSink
    {
        void Error(StringPair componentName, string message, Exception exception, params object[] dataNamesAndValues);

        void Info(StringPair componentName, string message, params object[] dataNamesAndValues);

        void Debug(StringPair componentName, string message, params object[] dataNamesAndValues);
    }

    internal struct StringPair
    {
        public StringPair(string item1, string item2)
        {
            Item1 = item1;
            Item2 = item2;
        }

        public string Item1 { get; }

        public string Item2 { get; }

        public static StringPair Create(string item1, string item2)
        {
            return new StringPair(item1, item2);
        }
    }
}
