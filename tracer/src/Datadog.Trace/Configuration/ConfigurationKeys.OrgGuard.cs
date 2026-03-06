// <copyright file="ConfigurationKeys.OrgGuard.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Configuration;

internal static partial class ConfigurationKeys
{
    public const string OrgPropagationGuardEnforce = "DD_TRACE_ORG_GUARD_ENFORCE";

    public const string OrgPropagationMarker = "DD_TRACE_ORG_GUARD_OPM";

    public const string OrgPropagationGuardTrustedOpm = "DD_TRACE_ORG_GUARD_TRUSTED_OPM";
}
