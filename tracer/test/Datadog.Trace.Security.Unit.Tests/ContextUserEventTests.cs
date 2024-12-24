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
    public void SdkOverrideTest()
    {
        var iWaf = new Mock<IWaf>().Object;
        var encoder = new Mock<Encoder>().Object;
        var context = Context.GetContext(IntPtr.Zero, iWaf, new Mock<WafLibraryInvoker>().Object, encoder);
        var security = new Mock<IDatadogSecurity>();
        security.Setup(s => s.AddressEnabled("test")).Returns(true);
        var userId = "toto";
        var addresses = context!.ShouldRunWith(security.Object, userId: userId);
        addresses.Should().Contain(new KeyValuePair<string, string>(AddressesConstants.UserId, userId));
    }
}
