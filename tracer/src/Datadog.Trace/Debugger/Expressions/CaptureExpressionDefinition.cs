// <copyright file="CaptureExpressionDefinition.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Debugger.Configurations.Models;

namespace Datadog.Trace.Debugger.Expressions;

internal readonly struct CaptureExpressionDefinition
{
    internal CaptureExpressionDefinition(string name, DebuggerExpression? expression, CaptureLimitInfo captureLimitInfo)
    {
        Name = name;
        Expression = expression;
        CaptureLimitInfo = captureLimitInfo;
    }

    internal string Name { get; }

    internal DebuggerExpression? Expression { get; }

    internal CaptureLimitInfo CaptureLimitInfo { get; }
}
