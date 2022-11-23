// <copyright file="IastRequestContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Iast;

internal class IastRequestContext
{
    private VulnerabilityBatch? _vulnerabilityBatch;
    private object _vulnerabilityLock = new();
    private TaintedObjects _taintedObjects = new();

    internal void AddIastVulnerabilitiesToSpan(Span span)
    {
        span.Tags.SetTag(Tags.IastEnabled, "1");

        if (_vulnerabilityBatch != null)
        {
            span.Tags.SetTag(Tags.IastJson, _vulnerabilityBatch.ToString());
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
            _vulnerabilityBatch ??= new();
            _vulnerabilityBatch.Add(vulnerability);
        }
    }

    private void AddRequestParameter(string name, string value)
    {
        _taintedObjects.TaintInputString(value, new Source(SourceType.RequestParameterValue.Item1, name, value));
        _taintedObjects.TaintInputString(name, new Source(SourceType.RequestParameterName.Item1, name, null));
    }

    private void AddRoutedParameter(string name, string value)
    {
        _taintedObjects.TaintInputString(value, new Source(SourceType.RoutedParameterValue.Item1, name, value));
    }

    private void AddQueryStringRaw(string queryString)
    {
        _taintedObjects.TaintInputString(queryString, new Source(SourceType.RequestQueryString.Item1, queryString, null));
    }

    private void AddQueryPath(string path)
    {
        _taintedObjects.TaintInputString(path, new Source(SourceType.RequestPath.Item1, path, null));
    }

    private void AddRouteData(IDictionary<string, object> routeData)
    {
        foreach (var item in routeData)
        {
            if (item.Value is string valueAsString)
            {
                AddRoutedParameter(item.Key, valueAsString);
            }
        }
    }

    internal TaintedObject? GetTainted(object objectToFind)
    {
        return _taintedObjects.Get(objectToFind);
    }

#if NETFRAMEWORK
    internal void AddRequestData(System.Web.HttpRequest request)
    {
        if (request.QueryString != null)
        {
            foreach (var key in request.QueryString.AllKeys)
            {
                AddRequestParameter(key, request.QueryString[key]);
            }

            AddQueryStringRaw(request.QueryString.ToString());
        }

        AddQueryPath(request.Path);
    }

    internal void AddRequestData(System.Web.HttpRequest request, IDictionary<string, object> routeData)
    {
        AddRouteData(routeData);
        AddRequestData(request);
    }
#else
    internal void AddRequestData(Microsoft.AspNetCore.Http.HttpRequest request, IDictionary<string, object> routeParameters)
    {
        AddRouteData(routeParameters);

        if (request.Query != null)
        {
            foreach (var item in request.Query)
            {
                AddRequestParameter(item.Key, item.Value);
            }
        }

        AddQueryPath(request.Path);
        AddQueryStringRaw(request.QueryString.Value);
    }
#endif
}
