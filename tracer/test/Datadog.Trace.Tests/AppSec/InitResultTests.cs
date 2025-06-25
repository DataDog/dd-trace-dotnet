// <copyright file="InitResultTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.AppSec;

public class InitResultTests
{
    [Fact]
    public void InitResult_Reported_ChangesToFalseAfterFirstCall()
    {
        var updateResult = UpdateResult.FromFailed("error");
        var initResult = InitResult.From(ref updateResult);
        initResult.Reported.Should().BeFalse();
        initResult.Reported.Should().BeTrue();
    }
}
