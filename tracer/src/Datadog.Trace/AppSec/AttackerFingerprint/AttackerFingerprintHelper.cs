// <copyright file="AttackerFingerprintHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Logging;

#nullable enable

namespace Datadog.Trace.AppSec.AttackerFingerprint;

internal static class AttackerFingerprintHelper
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AttackerFingerprintHelper));
    private static bool _warningLogged = false;

    public static void AddSpanTags(Span span, IResult result)
    {
        if (result?.FingerprintDerivatives is null || span.IsFinished || span.Type != SpanTypes.Web)
        {
            return;
        }

        var securityCoordinator = SecurityCoordinator.TryGet(Security.Instance, span);

        if (securityCoordinator is null)
        {
            return;
        }

        // We need a context
        if (securityCoordinator.Value.IsAdditiveContextDisposed())
        {
            return;
        }

        AddSpanTags(result.FingerprintDerivatives, span);
    }

    private static void AddSpanTags(Dictionary<string, object?>? fingerPrintDerivatives, ISpan span)
    {
        if (fingerPrintDerivatives is not null)
        {
            foreach (var derivative in fingerPrintDerivatives)
            {
                var value = derivative.Value?.ToString();
                if (!string.IsNullOrEmpty(value))
                {
                    span.SetTag(derivative.Key, value);
                }
                else
                {
                    if (!_warningLogged)
                    {
                        // This should not happen
                        Log.Warning("Fingerprint derivative {DerivativeKey} has no value", derivative.Key);
                        _warningLogged = true;
                    }
                }
            }
        }
    }
}
