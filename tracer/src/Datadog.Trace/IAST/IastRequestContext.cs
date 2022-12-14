// <copyright file="IastRequestContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Xml.Linq;

namespace Datadog.Trace.Iast;

internal class IastRequestContext
{
    private VulnerabilityBatch? _vulnerabilityBatch;
    private object _vulnerabilityLock = new();
    private TaintedObjects _taintedObjects = new();
    private bool _routedParametersAdded = false;
    private bool _queryPathParametersAdded = false;

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
        _taintedObjects.TaintInputString(value, new Source(SourceType.GetByte(SourceTypeName.RequestParameterValue), name, value));
        _taintedObjects.TaintInputString(name, new Source(SourceType.GetByte(SourceTypeName.RequestParameterName), name, null));
    }

    private void AddRoutedParameter(string name, string value)
    {
        _taintedObjects.TaintInputString(value, new Source(SourceType.GetByte(SourceTypeName.RoutedParameterValue), name, value));
    }

    private void AddQueryStringRaw(string queryString)
    {
        _taintedObjects.TaintInputString(queryString, new Source(SourceType.GetByte(SourceTypeName.RequestQueryString), null, queryString));
    }

    private void AddQueryPath(string path)
    {
        _taintedObjects.TaintInputString(path, new Source(SourceType.GetByte(SourceTypeName.RequestPath), null, path));
    }

    private void AddRouteData(IDictionary<string, object> routeData)
    {
        if (!_routedParametersAdded)
        {
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

    internal TaintedObject? GetTainted(object objectToFind)
    {
        return _taintedObjects.Get(objectToFind);
    }

#if NETFRAMEWORK
    // It might happen that we call more than once this method depending on the asp version. Anyway, these calls would be sequential.
    internal void AddRequestData(System.Web.HttpRequest request)
    {
        if (!_queryPathParametersAdded)
        {
            if (request.QueryString != null)
            {
                foreach (var key in request.QueryString.AllKeys)
                {
                    AddRequestParameter(key, request.QueryString[key]);
                }

                AddQueryStringRaw(request.QueryString.ToString());
            }

            AddRequestHeaders(request.Headers);
            AddQueryPath(request.Path);
            _queryPathParametersAdded = true;
        }
    }

    private void AddRequestHeaders(NameValueCollection headers)
    {
        foreach (var header in headers.AllKeys)
        {
            AddHeaderData(header, headers[header]);
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

        if (!_queryPathParametersAdded)
        {
            if (request.Query != null)
            {
                foreach (var item in request.Query)
                {
                    AddRequestParameter(item.Key, item.Value);
                }
            }

            AddQueryPath(request.Path);
            AddQueryStringRaw(request.QueryString.Value);
            AddRequestHeaders(request.Headers);
            _queryPathParametersAdded = true;
        }
    }

    private void AddRequestHeaders(Microsoft.AspNetCore.Http.IHeaderDictionary headers)
    {
        foreach (var header in headers)
        {
            AddHeaderData(header.Key, header.Value);
        }
    }

    private void AddHeaderData(string key, Microsoft.Extensions.Primitives.StringValues values)
    {
        foreach (var singleValue in values)
        {
            AddHeaderData(key, singleValue);
        }
    }

#endif

    private void AddHeaderData(string name, string value)
    {
        _taintedObjects.TaintInputString(value, new Source(SourceType.GetByte(SourceTypeName.RequestHeader), name, value));
        _taintedObjects.TaintInputString(name, new Source(SourceType.GetByte(SourceTypeName.RequestHeaderName), name, null));
    }
}
