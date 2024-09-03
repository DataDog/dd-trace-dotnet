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
    private static readonly Dictionary<string, object> _fingerprintRequest = new() { { AddressesConstants.WafContextProcessor, new Dictionary<string, object> { { "fingerprint", true } } } };

    public static void AddSpanTags(Span span)
    {
        var securityCoordinator = new SecurityCoordinator(Security.Instance, span);

        // We need a context
        if (!securityCoordinator.HasContext() || securityCoordinator.IsAdditiveContextDisposed())
        {
            return;
        }

        var result = securityCoordinator.RunWaf(_fingerprintRequest);
        AddSpanTags(result?.FingerprintDerivatives, span);
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
                    // This should not happen
                    Log.Warning("Fingerprint derivative {DerivativeKey} has no value", derivative.Key);
                }
            }
        }
    }
}
