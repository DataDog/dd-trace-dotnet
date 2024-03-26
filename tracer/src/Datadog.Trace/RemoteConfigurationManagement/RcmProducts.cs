// <copyright file="RcmProducts.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
namespace Datadog.Trace.RemoteConfigurationManagement;

internal static class RcmProducts
{
    public const string LiveDebugging = "LIVE_DEBUGGING";
    public const string LiveDebuggingSymbolDb = "LIVE_DEBUGGING_SYMBOL_DB";
    public const string Asm = "ASM";
    public const string AsmFeatures = "ASM_FEATURES";
    public const string AsmDd = "ASM_DD";
    public const string AsmData = "ASM_DATA";

    public const string TracerFlareInitiated = "AGENT_CONFIG";
    public const string TracerFlareRequested = "AGENT_TASK";
}
