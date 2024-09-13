// <copyright file="IastRequestContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Threading;
using System.Web;
#if !NETFRAMEWORK
using Microsoft.AspNetCore.Http;
#endif
using Datadog.Trace.AppSec;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SDK;
using Datadog.Trace.Iast.Telemetry;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;
using static Datadog.Trace.Telemetry.Metrics.MetricTags;

namespace Datadog.Trace.Iast;

internal class IastRequestContext
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(IastRequestContext));
    private VulnerabilityBatch? _vulnerabilityBatch;
    private object _vulnerabilityLock = new();
    private TaintedObjects _taintedObjects = new();
    private bool _routedParametersAdded = false;
    private bool _querySourcesAdded = false;
    private ExecutedTelemetryHelper? _executedTelemetryHelper = ExecutedTelemetryHelper.Enabled() ? new ExecutedTelemetryHelper() : null;
    private int _lastVulnerabilityStackId = 0;

    internal static void AddIastDisabledFlagToSpan(Span span)
    {
        span.Tags.SetTag(Tags.IastEnabled, "0");
    }

    internal void AddIastVulnerabilitiesToSpan(Span span)
    {
        try
        {
            span.Tags.SetTag(Tags.IastEnabled, "1");

            if (_vulnerabilityBatch != null)
            {
                span.Tags.SetTag(Tags.IastJson, _vulnerabilityBatch.ToString());

                if (_vulnerabilityBatch.IsTruncated())
                {
                    span.Tags.SetTag(Tags.IastJsonTagSizeExceeded, "1");
                }
            }

            if (_executedTelemetryHelper != null)
            {
                _executedTelemetryHelper.GenerateMetricTags(span.Tags, _taintedObjects.GetEstimatedSize());
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in IAST AddIastVulnerabilitiesToSpan");
        }
    }

    internal bool AddVulnerabilitiesAllowed()
    {
        return ((_vulnerabilityBatch?.Vulnerabilities.Count ?? 0) < Iast.Instance.Settings.VulnerabilitiesPerRequest);
    }

    internal void AddVulnerability(Vulnerability vulnerability)
    {
        lock (_vulnerabilityLock)
        {
            _vulnerabilityBatch ??= IastModule.GetVulnerabilityBatch();
            _vulnerabilityBatch.Add(vulnerability);
        }
    }

    internal void AddRequestBody(object? body, object? bodyExtracted)
    {
        try
        {
            _executedTelemetryHelper?.AddExecutedSource(IastInstrumentedSources.RequestBody);

            if (bodyExtracted is null)
            {
                if (body is null)
                {
                    return;
                }

                bodyExtracted = ObjectExtractor.Extract(body);
            }

            AddExtractedBody(bodyExtracted, null);
        }
        catch
        {
            Log.Warning("Error reading request Body.");
        }
    }

    private void AddExtractedBody(object? bodyExtracted, string? key)
    {
        if (bodyExtracted != null)
        {
            // We get either string, List<object> or Dictionary<string, object>
            if (bodyExtracted is string bodyExtractedStr)
            {
                _taintedObjects.TaintInputString(bodyExtractedStr, new Source(SourceType.RequestBody, key, bodyExtractedStr));
            }
            else
            {
                if (bodyExtracted is List<object> bodyExtractedList)
                {
                    foreach (var element in bodyExtractedList)
                    {
                        AddExtractedBody(element, key);
                    }
                }
                else
                {
                    if (bodyExtracted is Dictionary<string, object> bodyExtractedDic)
                    {
                        foreach (var keyValue in bodyExtractedDic)
                        {
                            AddExtractedBody(keyValue.Value, keyValue.Key);
                            _taintedObjects.TaintInputString(keyValue.Key, new Source(SourceType.RequestBody, key, keyValue.Key));
                        }
                    }
                }
            }
        }
    }

    private void AddRequestParameter(string name, string value)
    {
        _taintedObjects.TaintInputString(value, new Source(SourceType.RequestParameterValue, name, value));
        _taintedObjects.TaintInputString(name, new Source(SourceType.RequestParameterName, name, null));
    }

    private void AddRoutedParameter(string name, string value)
    {
        _taintedObjects.TaintInputString(value, new Source(SourceType.RoutedParameterValue, name, value));
    }

    private void AddQueryStringRaw(string queryString)
    {
        _taintedObjects.TaintInputString(queryString, new Source(SourceType.RequestQuery, null, queryString));
    }

    private void AddQueryUrl(string url)
    {
        _taintedObjects.TaintInputString(url, new Source(SourceType.RequestUri, null, url));
    }

    private void AddQueryPath(string path)
    {
        _taintedObjects.TaintInputString(path, new Source(SourceType.RequestPath, null, path));
    }

    private void AddRouteData(IDictionary<string, object> routeData)
    {
        if (!_routedParametersAdded)
        {
            _executedTelemetryHelper?.AddExecutedSource(IastInstrumentedSources.RoutedParameterValue);
            if (routeData != null)
            {
                foreach (var item in routeData)
                {
                    if (item.Value is string valueAsString)
                    {
                        AddRoutedParameter(item.Key, valueAsString);
                    }
                }
            }

            _routedParametersAdded = true;
        }
    }

    internal TaintedObjects GetTaintedObjects()
    {
        return _taintedObjects;
    }

    internal void AddDbValue(string? column, string value)
    {
        _taintedObjects.TaintInputString(value, new Source(SourceType.SqlRowValue, column, value));
    }

    internal TaintedObject? GetTainted(object objectToFind)
    {
        return _taintedObjects.Get(objectToFind);
    }

#if NETFRAMEWORK
    // It might happen that we call more than once this method depending on the asp version. Anyway, these calls would be sequential.
    internal void AddRequestData(System.Web.HttpRequest request)
    {
        if (!_querySourcesAdded)
        {
            if (_executedTelemetryHelper is { } helper)
            {
                helper.AddExecutedSource(IastInstrumentedSources.RequestParameterName);
                helper.AddExecutedSource(IastInstrumentedSources.RequestParameterValue);
                helper.AddExecutedSource(IastInstrumentedSources.RequestHeaderName);
                helper.AddExecutedSource(IastInstrumentedSources.RequestHeaderValue);
                helper.AddExecutedSource(IastInstrumentedSources.CookieName);
                helper.AddExecutedSource(IastInstrumentedSources.CookieValue);
                helper.AddExecutedSource(IastInstrumentedSources.RequestPath);
                helper.AddExecutedSource(IastInstrumentedSources.RequestQuery);
                helper.AddExecutedSource(IastInstrumentedSources.RequestUri);
            }

            var queryString = RequestDataHelper.GetQueryString(request);

            if (queryString != null)
            {
                foreach (var key in queryString.AllKeys)
                {
                    AddRequestParameter(key, queryString[key]);
                }

                AddQueryStringRaw(queryString.ToString());
            }

            AddRequestHeaders(RequestDataHelper.GetHeaders(request));

            var path = RequestDataHelper.GetPath(request);
            if (path is not null)
            {
                AddQueryPath(path);
            }

            var url = RequestDataHelper.GetUrl(request)?.ToString();
            if (url is not null)
            {
                AddQueryUrl(url);
            }

            AddRequestCookies(RequestDataHelper.GetCookies(request));
            _querySourcesAdded = true;
        }
    }

    private void AddRequestCookies(HttpCookieCollection? cookies)
    {
        if (cookies?.AllKeys is not null)
        {
            foreach (string key in cookies.AllKeys)
            {
                // cookies[key].Value is covered in the aspect

                for (int i = 0; i < cookies[key].Values.Count; i++)
                {
                    if (cookies[key].Values[i] is string valueInCollectionString)
                    {
                        AddCookieData(key, valueInCollectionString);
                    }
                }
            }
        }
    }

    private void AddRequestHeaders(System.Collections.Specialized.NameValueCollection? headers)
    {
        if (headers is not null)
        {
            foreach (var header in headers.AllKeys)
            {
                AddHeaderData(header, headers[header]);
            }
        }
    }

    // It might happen that we call more than once this method depending on the asp version. Anyway, these calls would be sequential.
    internal void AddRequestData(System.Web.HttpRequest request, IDictionary<string, object> routeData)
    {
        AddRouteData(routeData);
        AddRequestData(request);
    }

#else
    // It might happen that we call more than once this method depending on the asp version. Anyway, these calls would be sequential.
    internal void AddRequestData(Microsoft.AspNetCore.Http.HttpRequest request, IDictionary<string, object> routeParameters)
    {
        AddRouteData(routeParameters);

        if (!_querySourcesAdded)
        {
            if (_executedTelemetryHelper is { } helper)
            {
                helper.AddExecutedSource(IastInstrumentedSources.RequestParameterName);
                helper.AddExecutedSource(IastInstrumentedSources.RequestParameterValue);
                helper.AddExecutedSource(IastInstrumentedSources.RequestHeaderName);
                helper.AddExecutedSource(IastInstrumentedSources.RequestHeaderValue);
                helper.AddExecutedSource(IastInstrumentedSources.CookieName);
                helper.AddExecutedSource(IastInstrumentedSources.CookieValue);
                helper.AddExecutedSource(IastInstrumentedSources.RequestPath);
                helper.AddExecutedSource(IastInstrumentedSources.RequestQuery);
            }

            if (request.Query != null)
            {
                foreach (var item in request.Query)
                {
                    AddRequestParameter(item.Key, item.Value);
                }
            }

            AddQueryPath(request.Path);
            AddQueryStringRaw(RequestDataHelper.GetQueryString(request).Value);
            AddRequestHeaders(RequestDataHelper.GetHeaders(request));
            AddRequestCookies(RequestDataHelper.GetCookies(request));
            _querySourcesAdded = true;
        }
    }

    private void AddRequestCookies(IRequestCookieCollection? cookies)
    {
        if (cookies is not null)
        {
            foreach (var cookie in cookies)
            {
                AddCookieData(cookie.Key, cookie.Value);
            }
        }
    }

    private void AddRequestHeaders(Microsoft.AspNetCore.Http.IHeaderDictionary? headers)
    {
        if (headers is not null)
        {
            foreach (var header in headers)
            {
                if (header.Value.Count > 1)
                {
                    foreach (var singleValue in header.Value)
                    {
                        AddHeaderData(header.Key, singleValue);
                    }
                }
                else
                {
                    AddHeaderData(header.Key, header.Value);
                }
            }
        }
    }

#endif

    private void AddCookieData(string name, string value)
    {
        _taintedObjects.TaintInputString(value, new Source(SourceType.CookieValue, name, value));
        _taintedObjects.TaintInputString(name, new Source(SourceType.CookieName, name, name));
    }

    private void AddHeaderData(string name, string value)
    {
        _taintedObjects.TaintInputString(value, new Source(SourceType.RequestHeaderValue, name, value));
        _taintedObjects.TaintInputString(name, new Source(SourceType.RequestHeaderName, name, name));
    }

    internal void OnExecutedSinkTelemetry(IastInstrumentedSinks sink)
    {
        _executedTelemetryHelper?.AddExecutedSink(sink);
    }

    internal void OnExecutedSourceTelemetry(IastInstrumentedSources source)
    {
        _executedTelemetryHelper?.AddExecutedSource(source);
    }

    internal void OnExecutedPropagationTelemetry()
    {
        _executedTelemetryHelper?.AddExecutedPropagation();
    }

    internal string GetNextVulnerabilityStackId()
    {
        return Interlocked.Increment(ref _lastVulnerabilityStackId).ToString(CultureInfo.InvariantCulture);
    }
}
