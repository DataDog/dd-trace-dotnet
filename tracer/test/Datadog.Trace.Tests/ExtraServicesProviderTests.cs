// <copyright file="ExtraServicesProviderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests;

public class ExtraServicesProviderTests
{
    [Fact]
    public void Service_Names_Varying_Only_By_Case_Shouldnt_Be_Stored()
    {
        var target = new ExtraServicesProvider();
        target.AddService("extraVeg");
        target.AddService("ExTrAvEg");

        var servicesStored = target.GetExtraServices();
        servicesStored.Should().HaveCount(1);
        servicesStored.Should().HaveElementAt(0, "extraVeg");
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(63, 63)]
    [InlineData(64, 64)]
    [InlineData(65, 64)]
    [InlineData(100, 64)]
    public void Service_Stored_Should_Not_Exceed_Limit(int itemsToAdd, int expectCount)
    {
        var target = new ExtraServicesProvider();

        for (var i = 0; i < itemsToAdd; i++)
        {
            target.AddService($"extraVeg_{i}");
            target.AddService($"ExTrAvEg_{i}");
        }

        var servicesStored = target.GetExtraServices();
        servicesStored.Should().HaveCount(expectCount);
    }
}
