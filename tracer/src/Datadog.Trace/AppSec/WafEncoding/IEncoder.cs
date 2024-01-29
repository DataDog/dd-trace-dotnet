// <copyright file="IEncoder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Datadog.Trace.AppSec.Waf;

namespace Datadog.Trace.AppSec.WafEncoding;

internal interface IEncoder
{
    public IEncodeResult Encode<TInstance>(TInstance? o, int remainingDepth = WafConstants.MaxContainerDepth, string? key = null, bool applySafetyLimits = true);
}
