// <copyright file="ContextUserEventTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.AppSec.WafEncoding;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests;

public class ContextUserEventTests
{
    [Fact]
    public void NewValuesTests()
    {
        var iWaf = new Mock<IWaf>().Object;
        var encoder = new Mock<IEncoder>().Object;
        var context = Context.GetContext(IntPtr.Zero, iWaf, new Mock<IWafLibraryInvoker>().Object, encoder);
        var security = new Mock<IDatadogSecurity>();
        security.Setup(s => s.AddressEnabled(AddressesConstants.UserId)).Returns(true);
        var userId = "toto";
        var addresses = context!.FilterAddresses(security.Object, userId: userId);
        addresses.Should().HaveCount(1);
        addresses.Should().Contain(new KeyValuePair<string, object>(AddressesConstants.UserId, userId));
        context.CommitUserRuns(addresses, false);
        userId = "tata";
        // should run with a different value
        addresses = context!.FilterAddresses(security.Object, userId: userId);
        addresses.Should().Contain(new KeyValuePair<string, object>(AddressesConstants.UserId, userId));
        addresses.Should().HaveCount(1);
        context.CommitUserRuns(addresses, false);

        // should not run with same value
        addresses = context!.FilterAddresses(security.Object, userId: userId);
        addresses.Should().HaveCount(0);
    }

    [Fact]
    public void NewValuesSessionTests()
    {
        var iWaf = new Mock<IWaf>().Object;
        var encoder = new Mock<IEncoder>().Object;
        var context = Context.GetContext(IntPtr.Zero, iWaf, new Mock<IWafLibraryInvoker>().Object, encoder);
        var security = new Mock<IDatadogSecurity>();
        security.Setup(s => s.AddressEnabled(AddressesConstants.UserId)).Returns(true);
        security.Setup(s => s.AddressEnabled(AddressesConstants.UserSessionId)).Returns(true);

        var userId = "toto";
        var addresses = context!.FilterAddresses(security.Object, userId: userId, fromSdk: true);
        addresses.Should().HaveCount(1);
        addresses.Should().Contain(new KeyValuePair<string, object>(AddressesConstants.UserId, userId));
        context.CommitUserRuns(addresses, true);
        var ssessionId = "234";
        addresses = context!.FilterAddresses(security.Object, userSessionId: ssessionId);
        addresses.Should().HaveCount(1);
        addresses.Should().Contain(new KeyValuePair<string, object>(AddressesConstants.UserSessionId, ssessionId));
        context.CommitUserRuns(addresses, false);
        ssessionId = "tata";
        // should run with a different value
        addresses = context!.FilterAddresses(security.Object, userSessionId: ssessionId);
        addresses.Should().Contain(new KeyValuePair<string, object>(AddressesConstants.UserSessionId, ssessionId));
        addresses.Should().HaveCount(1);
        context.CommitUserRuns(addresses, false);

        // should not run with same value
        addresses = context!.FilterAddresses(security.Object, userSessionId: ssessionId);
        addresses.Should().HaveCount(0);
    }

    [Fact]
    public void AddressDisabledNo()
    {
        var iWaf = new Mock<IWaf>().Object;
        var encoder = new Mock<IEncoder>().Object;
        var context = Context.GetContext(IntPtr.Zero, iWaf, new Mock<IWafLibraryInvoker>().Object, encoder);
        var security = new Mock<IDatadogSecurity>();
        security.Setup(s => s.AddressEnabled(AddressesConstants.UserId)).Returns(false);
        security.Setup(s => s.AddressEnabled(AddressesConstants.UserSessionId)).Returns(true);
        var userId = "toto";
        var userSessionId = "123";
        var addresses = context!.FilterAddresses(security.Object, userId: userId);
        // waf shouldn't run with a disabled address
        addresses.Should().HaveCount(0);
        context.CommitUserRuns(addresses, false);

        // should run with a different value
        addresses = context!.FilterAddresses(security.Object, userId: userId, userSessionId: userSessionId);
        addresses.Should().Contain(new KeyValuePair<string, object>(AddressesConstants.UserSessionId, userSessionId));
        addresses.Should().HaveCount(1);
        context.CommitUserRuns(addresses, false);
    }

    [Fact]
    public void NullValueNo()
    {
        var iWaf = new Mock<IWaf>().Object;
        var encoder = new Mock<IEncoder>().Object;
        var context = Context.GetContext(IntPtr.Zero, iWaf, new Mock<IWafLibraryInvoker>().Object, encoder);
        var security = new Mock<IDatadogSecurity>();
        security.Setup(s => s.AddressEnabled(AddressesConstants.UserId)).Returns(true);
        var addresses = context!.FilterAddresses(security.Object, userId: null);
        // waf shouldn't run with a disabled address
        addresses.Should().HaveCount(0);
        context.CommitUserRuns(addresses, false);
    }

    [Fact]
    public void SdkOverrideTest()
    {
        var iWaf = new Mock<IWaf>().Object;
        var encoder = new Mock<IEncoder>().Object;
        var context = Context.GetContext(IntPtr.Zero, iWaf, new Mock<IWafLibraryInvoker>().Object, encoder);
        var security = new Mock<IDatadogSecurity>();
        security.Setup(s => s.AddressEnabled(AddressesConstants.UserId)).Returns(true);
        var userId = "toto";
        var addresses = context!.FilterAddresses(security.Object, userId: userId);
        addresses.Should().HaveCount(1);
        addresses.Should().Contain(new KeyValuePair<string, object>(AddressesConstants.UserId, userId));
        context.CommitUserRuns(addresses, true);
        addresses = context!.FilterAddresses(security.Object, userId: "other");
        addresses.Should().HaveCount(0);
    }
}
