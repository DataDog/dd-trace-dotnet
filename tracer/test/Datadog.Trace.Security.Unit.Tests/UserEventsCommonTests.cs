// <copyright file="UserEventsCommonTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore.UserEvents;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests;

public class UserEventsCommonTests
{
    [Fact]
    public void GetId_IdIsDefaultWhenAvailable()
    {
        var user = new User()
        {
            Id = "my-id",
            Email = "my@email.com",
            UserName = "my-username",
        };
        var id = UserEventsCommon.GetId(user);

        id.Should().Be("my-id");
    }

    [Fact]
    public void GetId_EmailIsSecondChoice()
    {
        var user = new User()
        {
            Email = "my@email.com",
            UserName = "my-username",
        };
        var id = UserEventsCommon.GetId(user);

        id.Should().Be("my@email.com");
    }

    [Fact]
    public void GetId_UserNameIsThirdChoice()
    {
        var user = new User()
        {
            UserName = "my-username",
        };
        var id = UserEventsCommon.GetId(user);

        id.Should().Be("my-username");
    }

    [Fact]
    public void GetAnonId_ExampleFromRfc()
    {
        var id = "zouzou@sansgluten.com";
        var anonId = UserEventsCommon.GetAnonId(id);

        anonId.Should().Be("anon_0c76692372ebf01a7da6e9570fb7d0a1");
    }

    private class User : IIdentityUser
    {
        public object Id { get; set; }

        public string Email { get; set; }

        public string UserName { get; set; }
    }
}
