// <copyright file="DelegateWrapperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

#nullable enable
namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.AWS.Lambda;

public class DelegateWrapperTests
{
    private DelegateToWrap _targetDelegateSync = new DelegateToWrap(arg => new ReturnValue
    {
        Value = arg.Argument
    });

    private delegate ReturnValue DelegateToWrap(ArgumentValue argument);

    [Fact]
    public void WrapSyncDelegate()
    {
        var testStr = "Test1";
        var result = _targetDelegateSync.Invoke(new ArgumentValue { Argument = testStr });
        result.Value.Should().BeEquivalentTo(testStr);
    }

    private class ReturnValue
    {
        public string? Value { get; set; }
    }

    private class ArgumentValue
    {
        public string? Argument { get; set; }
    }
}
