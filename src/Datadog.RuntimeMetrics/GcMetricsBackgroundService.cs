using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.RuntimeMetrics
{
    public class GcMetricsBackgroundService : BackgroundService, IObservable<IEnumerable<MetricValue>>
    {
        private readonly ObserverCollection<IEnumerable<MetricValue>> _observers = new ObserverCollection<IEnumerable<MetricValue>>();
        private readonly Process _process = Process.GetCurrentProcess();
        private readonly GcEventListener _gcEventListener = new GcEventListener();

        private TimeSpan _oldCpuTime;
        private DateTime _lastMonitorTime = DateTime.UtcNow;
        private int _lastGcCountGen0;
        private int _lastGcCountGen1;
        private int _lastGcCountGen2;

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _gcEventListener.EnableEvents();
            return base.StartAsync(cancellationToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _gcEventListener.DisableEvents();
            return base.StopAsync(cancellationToken);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Factory.StartNew(() =>
                                         {
                                             var values = new MetricValue[8];
                                             var period = TimeSpan.FromSeconds(1);

                                             // set initial values
                                             _process.Refresh();
                                             _oldCpuTime = _process.TotalProcessorTime;
                                             _lastMonitorTime = DateTime.UtcNow;

                                             while (!stoppingToken.IsCancellationRequested)
                                             {
                                                 GcMetrics metrics = GetMetrics();

                                                 values[0] = new MetricValue(Metric.GcHeapSize, metrics.TotalAllocatedBytes);
                                                 values[1] = new MetricValue(Metric.WorkingSet, metrics.WorkingSetBytes);
                                                 values[2] = new MetricValue(Metric.PrivateBytes, metrics.PrivateMemoryBytes);
                                                 values[3] = new MetricValue(Metric.GcCountGen0, metrics.GcCountGen0);
                                                 values[4] = new MetricValue(Metric.GcCountGen1, metrics.GcCountGen1);
                                                 values[5] = new MetricValue(Metric.GcCountGen2, metrics.GcCountGen2);
                                                 values[6] = new MetricValue(Metric.CpuPercent, metrics.CpuPercent);
                                                 values[7] = new MetricValue(Metric.CpuTimeMs, metrics.CpuTimeMs);

                                                 _observers.OnNext(values);

                                                 if (!stoppingToken.IsCancellationRequested)
                                                 {
                                                     Thread.Sleep(period);
                                                 }
                                             }
                                         },
                                         stoppingToken,
                                         TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
                                         TaskScheduler.Default);
        }

        public GcMetrics GetMetrics()
        {
            DateTime now = DateTime.UtcNow;
            _process.Refresh();
            TimeSpan newCpuTime = _process.TotalProcessorTime;

            TimeSpan elapsedTime = now - _lastMonitorTime;
            TimeSpan elapsedCpuTime = newCpuTime - _oldCpuTime;
            double cpu = 100.0 * elapsedCpuTime.Ticks / elapsedTime.Ticks /* / Environment.ProcessorCount */;

            _lastMonitorTime = now;
            _oldCpuTime = newCpuTime;

            int gcCountGen0 = GC.CollectionCount(0);
            int gcCountGen1 = GC.CollectionCount(1);
            int gcCountGen2 = GC.CollectionCount(2);

            var metrics = new GcMetrics
                          {
                              TotalAllocatedBytes = GC.GetTotalMemory(false),
                              WorkingSetBytes = _process.WorkingSet64,
                              PrivateMemoryBytes = _process.PrivateMemorySize64,
                              GcCountGen0 = gcCountGen0 - _lastGcCountGen0,
                              GcCountGen1 = gcCountGen1 - _lastGcCountGen1,
                              GcCountGen2 = gcCountGen2 - _lastGcCountGen2,
                              CpuTimeMs = elapsedCpuTime.TotalMilliseconds,
                              CpuPercent = cpu
                          };

            _lastGcCountGen0 = gcCountGen0;
            _lastGcCountGen1 = gcCountGen1;
            _lastGcCountGen2 = gcCountGen2;

            return metrics;
        }

        public IDisposable Subscribe(IObserver<IEnumerable<MetricValue>> observer)
        {
            IDisposable listenerSubscription = _gcEventListener.Subscribe(observer);
            IDisposable serviceSubscription = _observers.Subscribe(observer);
            return new DisposableCollection(listenerSubscription, serviceSubscription);
        }

        public override void Dispose()
        {
            base.Dispose();
            _process?.Dispose();
        }
    }
}
