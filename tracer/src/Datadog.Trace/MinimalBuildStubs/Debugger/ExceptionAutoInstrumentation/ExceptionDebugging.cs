// <copyright file="ExceptionDebugging.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation;

internal static class ExceptionDebugging
{
    public static bool Enabled => false;

    public static void Initialize()
    {
    }

    public static void Report(Span span, Exception exception)
    {
    }

    public static void BeginRequest()
    {
    }

    public static void EndRequest()
    {
    }
}
