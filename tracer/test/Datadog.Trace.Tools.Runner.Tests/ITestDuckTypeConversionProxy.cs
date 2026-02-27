// <copyright file="ITestDuckTypeConversionProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Tools.Runner.Tests;

internal interface ITestDuckTypeConversionProxy
{
    int AddOne(object value);

    object ReadNumber();

    string EchoString(object value);

    object EchoObject();

    DayOfWeek ParseEnum(object value);

    object EchoEnumObject(DayOfWeek value);

    IComparable ReadEnumComparable();
}
