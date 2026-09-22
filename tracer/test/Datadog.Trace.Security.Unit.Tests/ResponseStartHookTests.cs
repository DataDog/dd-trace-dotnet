// <copyright file="ResponseStartHookTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
#if !NETFRAMEWORK
using Datadog.Trace.AppSec.Coordinator;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests;

/// <summary>
/// The end of the pipeline only scans the response by itself on servers that never fire the instrumented
/// response start hook, and those are recognised by the type of their IHttpResponseFeature. Getting this
/// wrong either loses the response status (HTTP.sys) or runs before a hook that could have blocked.
/// </summary>
public class ResponseStartHookTests
{
    [Theory]
    [InlineData("Microsoft.AspNetCore.Server.HttpSys.FeatureContext", true)]
    [InlineData("Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.HttpProtocol", false)]
    [InlineData("Microsoft.AspNetCore.Server.IIS.Core.IISHttpContext", false)]
    [InlineData("Microsoft.AspNetCore.TestHost.ClientHandler+RequestState", false)]
    [InlineData(null, false)]
    public void GivenAResponseFeature_WhenItsServerIsChecked_ThenOnlyHttpSysHasNoStartHook(string? responseFeatureTypeName, bool expected)
        => SecurityCoordinatorHelpers.HasNoResponseStartHook(responseFeatureTypeName).Should().Be(expected);
}
#endif
