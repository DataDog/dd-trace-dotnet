using System;

#pragma warning disable SA1131 // Use readable conditions
#pragma warning disable SA1203 // Constants must appear before fields
#pragma warning disable SA1201 // Elements must appear in the correct order
namespace Datadog.Trace.ClrProfiler
{
    internal class ActivityCollectorConfiguration
    {
        public static class Defaults
        {
            public const string ActivitySourceName = "Datadog.Trace";
            public static readonly string ActivitySourceVersion = typeof(Tracer).Assembly.GetName().Version.ToString();
            public const bool AggregateActivitiesIntoTraces = true;
            public static readonly TimeSpan CompletedItemsBufferSendInterval = TimeSpan.FromSeconds(1);
            public const int CompletedItemsBufferMaxSize = 512;
            public static readonly Func<ActivityCollectorConfiguration, IActivityExporter> TraceExporterFactory = DatadogAgentMessagePackActivityExporter.Factory;
        }

        private string _activitySourceName = Defaults.ActivitySourceName;
        private string _activitySourceVersion = Defaults.ActivitySourceVersion;
        private bool _aggregateActivitiesIntoTraces = Defaults.AggregateActivitiesIntoTraces;
        private TimeSpan _completedItemsBufferSendInterval = Defaults.CompletedItemsBufferSendInterval;
        private int _completedItemsBufferMaxSize = Defaults.CompletedItemsBufferMaxSize;
        private Func<ActivityCollectorConfiguration, IActivityExporter> _traceExporterFactory = Defaults.TraceExporterFactory;

        public string ActivitySourceName
        {
            get
            {
                return _activitySourceName;
            }

            set
            {
                ValidateNotReadOnly();
                Validate.NotNull(_activitySourceName, nameof(ActivitySourceName));
                _activitySourceName = value;
            }
        }

        public string ActivitySourceVersion
        {
            get
            {
                return _activitySourceVersion;
            }

            set
            {
                ValidateNotReadOnly();
                Validate.NotNull(_activitySourceVersion, nameof(ActivitySourceVersion));
                _activitySourceVersion = value;
            }
        }

        public bool AggregateActivitiesIntoTraces
        {
            get
            {
                return _aggregateActivitiesIntoTraces;
            }

            set
            {
                ValidateNotReadOnly();
                _aggregateActivitiesIntoTraces = value;
            }
        }

        public TimeSpan CompletedItemsBufferSendInterval
        {
            get
            {
                return _completedItemsBufferSendInterval;
            }

            set
            {
                ValidateNotReadOnly();

                if (value < TimeSpan.FromMilliseconds(1) || TimeSpan.FromHours(1) < value)
                {
                    throw new ArgumentOutOfRangeException(nameof(CompletedItemsBufferSendInterval));
                }

                _completedItemsBufferSendInterval = value;
            }
        }

        public int CompletedItemsBufferMaxSize
        {
            get
            {
                return _completedItemsBufferMaxSize;
            }

            set
            {
                ValidateNotReadOnly();

                if (value < 1 || 1000000 < value)
                {
                    throw new ArgumentOutOfRangeException(nameof(CompletedItemsBufferMaxSize));
                }

                _completedItemsBufferMaxSize = value;
            }
        }

        public Func<ActivityCollectorConfiguration, IActivityExporter> TraceExporterFactory
        {
            get
            {
                return _traceExporterFactory;
            }

            set
            {
                ValidateNotReadOnly();
                Validate.NotNull(value, nameof(TraceExporterFactory));
                _traceExporterFactory = value;
            }
        }

        public bool IsReadOnly { get; private set; }

        public void SetReadOnly()
        {
            this.IsReadOnly = true;
        }

        private void ValidateNotReadOnly()
        {
            if (this.IsReadOnly)
            {
                throw new InvalidOperationException($"This {nameof(ActivityCollectorConfiguration)} is read-only and cannot be modified.");
            }
        }
    }
}
