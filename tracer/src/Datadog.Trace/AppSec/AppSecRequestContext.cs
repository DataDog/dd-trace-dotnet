// <copyright file="AppSecRequestContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.AppSec.Rasp;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.AppSec;

internal partial class AppSecRequestContext
{
    private const string StackKey = "_dd.stack";
    private const string ExploitStackKey = "exploit";
    private const string VulnerabilityStackKey = "vulnerability";
    private const string AppsecKey = "appsec";
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<AppSecRequestContext>();
    private readonly object _sync = new();
    private readonly RaspTelemetryHelper? _raspTelemetryHelper = Security.Instance.RaspEnabled ? new RaspTelemetryHelper() : null;
    private readonly List<object> _wafSecurityEvents = new();
    private int? _wafError = null;
    private int? _wafRaspError = null;
    private Dictionary<string, List<Dictionary<string, object>>>? _raspStackTraces;

    internal void CloseWebSpan(TraceTagCollection tags, Span span)
    {
        lock (_sync)
        {
            if (_wafSecurityEvents.Count > 0)
            {
                // Older version of the Agent doesn't support meta struct
                // Fallback to the _dd.appsec.json tag
                if (Security.Instance.IsMetaStructSupported())
                {
                    span.SetMetaStruct(AppsecKey, MetaStructHelper.ObjectToByteArray(new Dictionary<string, List<object>> { { "triggers", _wafSecurityEvents } }));
                }
                else
                {
                    var triggers = JsonConvert.SerializeObject(_wafSecurityEvents);
                    tags.SetTag(Tags.AppSecJson, "{\"triggers\":" + triggers + "}");
                }
            }

            if (_raspStackTraces?.Count > 0)
            {
                span.SetMetaStruct(StackKey, MetaStructHelper.ObjectToByteArray(_raspStackTraces));
            }

            if (_wafError != null)
            {
                tags.SetTag(Tags.WafError, _wafError.ToString());
            }

            if (_wafRaspError != null)
            {
                tags.SetTag(Tags.RaspWafError, _wafRaspError.ToString());
            }

            _raspTelemetryHelper?.GenerateRaspSpanMetricTags(span.Tags);
        }
    }

    internal void CheckWAFError(int code, bool isRasp)
    {
        int? existingValue = isRasp ? _wafRaspError : _wafError;
        if (code < 0 && (existingValue == null || existingValue < code))
        {
            if (isRasp)
            {
                _wafRaspError = code;
            }
            else
            {
                _wafError = code;
            }
        }
    }

    internal void AddRaspSpanMetrics(ulong duration, ulong durationWithBindings, bool timeout)
    {
        lock (_sync)
        {
            _raspTelemetryHelper?.AddRaspSpanMetrics(duration, durationWithBindings, timeout);
        }
    }

    internal void AddWafSecurityEvents(IReadOnlyCollection<object> events)
    {
        lock (_sync)
        {
            _wafSecurityEvents.AddRange(events);
        }
    }

    internal void AddRaspStackTrace(Dictionary<string, object> stackTrace, int maxStackTraces)
    {
        AddStackTrace(ExploitStackKey, stackTrace, maxStackTraces);
    }

    internal void AddVulnerabilityStackTrace(Dictionary<string, object> stackTrace, int maxStackTraces)
    {
        AddStackTrace(VulnerabilityStackKey, stackTrace, maxStackTraces);
    }

    internal void AddStackTrace(string stackCategory, Dictionary<string, object> stackTrace, int maxStackTraces)
    {
        lock (_sync)
        {
            _raspStackTraces ??= new();

            if (!_raspStackTraces.ContainsKey(stackCategory))
            {
                _raspStackTraces.Add(stackCategory, new());
            }
            else if (maxStackTraces > 0 && _raspStackTraces[stackCategory].Count >= maxStackTraces)
            {
                return;
            }

            _raspStackTraces[stackCategory].Add(stackTrace);
        }
    }
}

internal partial class AppSecRequestContext
{
    private bool _isAdditiveContextDisposed;

    private IContext? _context;

    /// <summary>
    /// Disposes the WAF's context stored in HttpContext.Items[]. If it doesn't exist, nothing happens, no crash
    /// </summary>
    internal void DisposeAdditiveContext()
    {
        _context?.Dispose();
        _isAdditiveContextDisposed = true;
    }

    internal IContext? GetOrCreateAdditiveContext(Security security)
    {
        if (_isAdditiveContextDisposed)
        {
            Log.Debug("Additive context was requested when already disposed");
            return null;
        }

        if (_context is not null)
        {
            return _context;
        }

        _context = security.CreateAdditiveContext();
        return _context;
    }
}
