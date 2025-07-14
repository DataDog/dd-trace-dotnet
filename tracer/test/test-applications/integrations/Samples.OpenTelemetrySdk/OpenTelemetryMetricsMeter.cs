using System.Diagnostics.Metrics;

namespace Samples.OpenTelemetrySdk;

public static class OpenTelemetryMetricsMeter
{
    internal const string MeterName = "OpenTelemetryMetricsMeter";

    private static readonly Meter _meter = new Meter(MeterName);
    public static Counter<long> LongCounter => _meter.CreateCounter<long>("example.counter");
    public static Histogram<double> DoubleHistogram => _meter.CreateHistogram<double>("example.histogram");
    public static UpDownCounter<long> LongUpDownCounter = _meter.CreateUpDownCounter<long>("example.upDownCounter");
#if NET9_0_OR_GREATER
    public static Gauge<double> DoubleGauge = _meter.CreateGauge<double>("example.gauge");
#endif
    public static ObservableCounter<long> AsyncLongCounter = _meter.CreateObservableCounter<long>("example.async.counter", () => 22L);
    public static ObservableUpDownCounter<long> AsyncLongUpDownCounter = _meter.CreateObservableUpDownCounter<long>("example.async.upDownCounter", () => 66L);
    public static ObservableGauge<double> AsyncGauge = _meter.CreateObservableGauge<double>("example.async.gauge", () => 88L);
}
