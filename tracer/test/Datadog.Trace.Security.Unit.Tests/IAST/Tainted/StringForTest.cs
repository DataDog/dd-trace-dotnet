// <copyright file="StringForTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Security.Unit.Tests.Iast.Tainted;

public class StringForTest
{
    public StringForTest(string value)
    {
        Value = value;
    }

    public int Hash { get; set; }

    public string Value { get; private set; }

    public override int GetHashCode()
    {
        return Hash;
    }
}
