// <copyright file="ProductsTelemetryCollector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Threading;

namespace Datadog.Trace.Telemetry;

internal class ProductsTelemetryCollector
{
    private readonly ProductDetail?[] _allTime;
    private readonly ProductDetail?[] _productsByType;
    private int _hasChangesFlag = 0;

    public ProductsTelemetryCollector()
    {
        _productsByType = new ProductDetail?[3];
        _allTime = new ProductDetail?[3];
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

        return BuildProductsData();
    }

    /// <summary>
    /// Gets all the product data recorded so far
    /// </summary>
    public ProductsData? GetFullData()
    {
        lock (_allTime)
        {
            var appsec = GetLatestData(_allTime, _productsByType, TelemetryProductType.AppSec);
            var profiler = GetLatestData(_allTime, _productsByType, TelemetryProductType.Profiler);
            var dynamicInstrumentation = GetLatestData(_allTime, _productsByType, TelemetryProductType.DynamicInstrumentation);

            if (appsec is not null
             || profiler is not null
             || dynamicInstrumentation is not null)
            {
                return new ProductsData
                {
                    Appsec = appsec,
                    Profiler = profiler,
                    DynamicInstrumentation = dynamicInstrumentation
                };
            }

            return null;
        }

        static ProductData? GetLatestData(ProductDetail?[] allTime, ProductDetail?[] current, TelemetryProductType product)
            // Current contains data added in this cycle, but may be null if not changed
            => (current[(int)product] ?? allTime[(int)product]) is { } detail
                   ? new ProductData(detail.Enabled, detail.Error)
                   : null;
    }

    private ProductsData? BuildProductsData()
    {
        lock (_allTime)
        {
            // only include changes to products
            var appsec = GetAndUpdateProductData(_allTime, _productsByType, TelemetryProductType.AppSec);
            var profiler = GetAndUpdateProductData(_allTime, _productsByType, TelemetryProductType.Profiler);
            var dynamicInstrumentation = GetAndUpdateProductData(_allTime, _productsByType, TelemetryProductType.DynamicInstrumentation);

            if (appsec is not null
             || profiler is not null
             || dynamicInstrumentation is not null)
            {
                return new ProductsData
                {
                    Appsec = appsec,
                    Profiler = profiler,
                    DynamicInstrumentation = dynamicInstrumentation
                };
            }

            return null;
        }

        static ProductData? GetAndUpdateProductData(ProductDetail?[] allTime, ProductDetail?[] productsByType, TelemetryProductType product)
        {
            var updated = productsByType[(int)product];
            if (updated is { } latest)
            {
                allTime[(int)product] = latest;
                productsByType[(int)product] = null;
                return new ProductData(latest.Enabled, latest.Error);
            }

            // nothing to do
            return null;
        }
    }

    public bool HasChanges() => _hasChangesFlag == 1;

    private void SetHasChanges()
    {
        Interlocked.Exchange(ref _hasChangesFlag, 1);
    }

    internal readonly record struct ProductDetail
    {
        public ProductDetail(bool enabled, ErrorData? error)
        {
            Enabled = enabled;
            Error = error;
        }

        public bool Enabled { get; }

        public ErrorData? Error { get; }
    }
}
