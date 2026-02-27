// <copyright file="TestDuckTypeConversionTarget.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Tools.Runner.Tests;

internal class TestDuckTypeConversionTarget
{
    private int AddOne(int value)
    {
        return value + 1;
    }

    private int ReadNumber()
    {
        return 42;
    }

    private string EchoString(string value)
    {
        return value;
    }

    private string EchoObject()
    {
        return "text";
    }

    private DayOfWeek ParseEnum(DayOfWeek value)
    {
        return value;
    }

    private DayOfWeek EchoEnumObject(DayOfWeek value)
    {
        return value;
    }

    private DayOfWeek ReadEnumComparable()
    {
        return DayOfWeek.Friday;
    }
}
