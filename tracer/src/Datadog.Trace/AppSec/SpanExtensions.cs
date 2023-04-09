// <copyright file="SpanExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable  enable
namespace Datadog.Trace.AppSec;

internal static class SpanExtensions
{
    internal static bool IsAppsecEvent(this Span span) => span.GetTag(Tags.AppSecEvent) == "true";
}
