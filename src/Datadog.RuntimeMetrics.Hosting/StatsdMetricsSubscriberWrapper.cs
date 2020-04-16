using System;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using StatsdClient;

namespace Datadog.RuntimeMetrics.Hosting
{
    public class StatsdMetricsSubscriberWrapper : IObserver<IEnumerable<MetricValue>>, IDisposable
    {
        private readonly StatsdMetricsSubscriber _subscriber;

        public StatsdMetricsSubscriberWrapper(IStatsdUDP statsdUdp, IOptions<StatsdMetricsOptions> statsdOptions)
        {
            _subscriber = new StatsdMetricsSubscriber(statsdUdp, statsdOptions.Value);
        }

        public void OnCompleted()
        {
            _subscriber.OnCompleted();
        }

        public void OnError(Exception error)
        {
            _subscriber.OnError(error);
        }

        public void OnNext(IEnumerable<MetricValue> value)
        {
            _subscriber.OnNext(value);
        }

        public void Dispose()
        {
            _subscriber?.Dispose();
        }
    }
}
