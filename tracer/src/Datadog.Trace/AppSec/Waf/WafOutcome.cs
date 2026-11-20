// <copyright file="WafOutcome.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.AppSec.Waf;

/// <summary>
/// Why a WAF call produced no context or no result. Carrying the cause is what lets a caller tell a
/// real failure from a WAF that is simply gone: both come back as a null, and the difference is not
/// observable afterwards because a concurrent disposal can happen in between.
/// </summary>
internal enum WafOutcome
{
    /// <summary>The call succeeded: a context was handed out, or a run produced a result.</summary>
    Success = 0,

    /// <summary>The request this call belongs to has already ended, so nothing was evaluated.</summary>
    RequestEnded = 1,

    /// <summary>The WAF instance is gone: disposed, replaced by an update, or never initialized.</summary>
    WafUnavailable = 2,

    /// <summary>The addresses or the (sub)context could not be handed to the WAF.</summary>
    BindingFailed = 3,
}
