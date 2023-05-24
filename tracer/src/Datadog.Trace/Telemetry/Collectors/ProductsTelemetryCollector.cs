// <copyright file="ProductsTelemetryCollector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Threading;

namespace Datadog.Trace.Telemetry;

internal class ProductsTelemetryCollector
{
    private ProductDetail?[] _productsByType;
    private int _hasChangesFlag = 0;

    public ProductsTelemetryCollector()
    {
        _productsByType = new ProductDetail?[3];
    }

    public void ProductChanged(TelemetryProductType product, bool enabled, ErrorData? error)
    {
        _productsByType[(int)product] = new ProductDetail(enabled, error);
        SetHasChanges();
    }

    /// <summary>
    /// Get the latest data to send to the intake.
    /// </summary>
    /// <returns>Null if there are no changes, or the collector is not yet initialized</returns>
    public ProductsData? GetData()
    {
        var hasChanges = Interlocked.CompareExchange(ref _hasChangesFlag, 0, 1) == 1;
        if (!hasChanges)
        {
            return null;
        }

        var results = Interlocked.Exchange(ref _productsByType, new ProductDetail?[3]);

        var appsec = results[(int)TelemetryProductType.AppSec] is { } a ? new ProductData(a.Enabled, a.Error) : null;
        var profiler = results[(int)TelemetryProductType.Profiler] is { } p ? new ProductData(p.Enabled, p.Error) : null;
        var dynamicInstrumentation = results[(int)TelemetryProductType.DynamicInstrumentation] is { } d ? new ProductData(d.Enabled, d.Error) : null;

        if (appsec is not null
         || profiler is not null
         || dynamicInstrumentation is not null)
        {
            return new ProductsData
            {
                Appsec = appsec,
                Profiler = profiler,
                DynamicInstrumentation = dynamicInstrumentation,
            };
        }

        return null;
    }

    public bool HasChanges() => _hasChangesFlag == 1;

    private void SetHasChanges()
    {
        Interlocked.Exchange(ref _hasChangesFlag, 1);
    }

    internal struct ProductDetail
    {
        public ProductDetail(bool enabled, ErrorData? error)
        {
            Enabled = enabled;
            Error = error;
        }

        public bool Enabled { get; }

        public ErrorData? Error { get; set; }
    }
}
