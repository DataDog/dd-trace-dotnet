// <copyright file="CachedBasicPropertiesHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ;
using FluentAssertions;
using RabbitMQ.Client;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.RabbitMQ;

public class CachedBasicPropertiesHelperTests
{
    [Fact]
    public void CreateHeaders_With_Null_BasicProperties_Throws()
    {
        // the BasicProperties(IReadOnlyBasicProperties) constructor throws NullReferenceException if parameter is null
        var func = () => CachedBasicPropertiesHelper<BasicProperties>.CreateHeaders(null!);
        func.Should().ThrowExactly<NullReferenceException>();
    }

    [Fact]
    public void CreateHeaders_BasicProperties_With_Null_Headers()
    {
        var originalProperties = new BasicProperties();

        var newProperties = CachedBasicPropertiesHelper<BasicProperties>.CreateHeaders(originalProperties);

        newProperties.Should().NotBeNull();
        newProperties.Headers.Should().BeNull();
    }

    [Fact]
    public void CreateHeaders_BasicProperties_With_Empty_Headers()
    {
        var headers = new Dictionary<string, object>();

        var originalProperties = new BasicProperties
        {
            Headers = headers
        };

        var newProperties = CachedBasicPropertiesHelper<BasicProperties>.CreateHeaders(originalProperties);

        newProperties.Should().NotBeNull();
        newProperties.Headers.Should().BeSameAs(originalProperties.Headers)
                     .And.Subject.Should().BeEmpty();
    }

    [Fact]
    public void CreateHeaders_BasicProperties_With_Headers()
    {
        var originalProperties = new BasicProperties();
        originalProperties.Headers ??= new Dictionary<string, object>();
        originalProperties.Headers["key1"] = "value1";

        var newProperties = CachedBasicPropertiesHelper<BasicProperties>.CreateHeaders(originalProperties);

        newProperties.Should().NotBeNull();
        newProperties.Headers.Should().BeSameAs(originalProperties.Headers)
                     .And.Subject.Should().Contain("key1", "value1");
    }
}
