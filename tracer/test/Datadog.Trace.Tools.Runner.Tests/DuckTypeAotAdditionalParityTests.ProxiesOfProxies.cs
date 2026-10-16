// <copyright file="DuckTypeAotAdditionalParityTests.ProxiesOfProxies.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Tools.Runner.DuckTypeAot;
using FluentAssertions;
using Xunit;

#pragma warning disable SA1201 // Nested test fixtures follow the test methods.

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// Forward proxies of forward proxy instances: dynamic duck typing creates the proxy of the runtime type of the instance, the
/// proxy type it generated, and the registry serves the proxy types it generates with the mappings of their proxy definition
/// type (the map the discovery recorder writes for them).
/// </summary>
public partial class DuckTypeAotAdditionalParityTests
{
    [Theory]
    // The same proxy definition over a proxy (with an out parameter, like Kafka's IHeaders.TryGetLastBytes).
    [InlineData("same-definition", "headers|key!|True|True|True")]
    // Another proxy definition over a proxy.
    [InlineData("other-definition", "headers|True|True")]
    // A class proxy over a proxy.
    [InlineData("class-proxy", "headers|True|True")]
    public void GeneratedRegistryShouldCreateProxiesOfProxiesLikeDynamicMode(string scenario, string expected)
    {
        var (exercise, mappings) = scenario switch
        {
            "same-definition" => (
                Run(() =>
                {
                    var first = DuckType.Create<IProxyOfProxyHeaders>(new ProxyOfProxyHeaders())!;
                    var second = (IProxyOfProxyHeaders)DuckType.Create(typeof(IProxyOfProxyHeaders), first)!;
                    var found = second.TryGetLast("key", out var value);
                    return $"{second.Name}|{value}|{found}|{((IDuckType)second).Type == first.GetType()}|{((IDuckType)second).Instance is IDuckType}";
                }),
                new[] { Mapping(typeof(IProxyOfProxyHeaders), typeof(ProxyOfProxyHeaders)), Mapping(typeof(IProxyOfProxyHeaders), typeof(IProxyOfProxyHeaders)) }),
            "other-definition" => (
                Run(() =>
                {
                    var first = DuckType.Create<IProxyOfProxyHeaders>(new ProxyOfProxyHeaders())!;
                    var second = DuckType.Create<IProxyOfProxyName>(first)!;
                    return $"{second.Name}|{((IDuckType)second).Type == first.GetType()}|{((IDuckType)second).Instance is IDuckType}";
                }),
                new[] { Mapping(typeof(IProxyOfProxyHeaders), typeof(ProxyOfProxyHeaders)), Mapping(typeof(IProxyOfProxyName), typeof(IProxyOfProxyHeaders)) }),
            "class-proxy" => (
                Run(() =>
                {
                    var first = DuckType.Create<IProxyOfProxyHeaders>(new ProxyOfProxyHeaders())!;
                    var second = DuckType.Create<IProxyOfProxyNameClass>(first)!;
                    return $"{second.Name}|{((IDuckType)second).Type == first.GetType()}|{((IDuckType)second).Instance is IDuckType}";
                }),
                new[] { Mapping(typeof(IProxyOfProxyHeaders), typeof(ProxyOfProxyHeaders)), Mapping(typeof(IProxyOfProxyNameClass), typeof(IProxyOfProxyHeaders)) }),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
        };

        AssertSameOutcome(expected, exercise, mappings);
    }

    [Fact]
    public void GeneratedProxyMethodsShouldHaveTheParametersOfTheMethodsTheyImplementLikeDynamicMode()
    {
        // Names, in/out/optional attributes and the default values the runtime stores (not a decimal, a native integer or a null
        // value-type default), like the methods dynamic duck typing defines; accessors have none.
        AssertSameOutcome(
            "Format(value:None=,when:Optional, HasDefault=2000-01-01,text:Optional, HasDefault=x,count:Optional, HasDefault=3,token:Optional=,amount:Optional=,offset:Optional=)|TryGetLast(key:None=,value:Out=)|set_Name(:None=)",
            () => string.Join(
                "|",
                DuckType.Create<IProxyOfProxyParameters>(new ProxyOfProxyParametersTarget())!.GetType().GetMethods()
                        .Where(method => method.Name is "Format" or "TryGetLast" or "set_Name")
                        .OrderBy(method => method.Name, StringComparer.Ordinal)
                        .Select(method => method.Name + "(" + string.Join(",", method.GetParameters().Select(parameter => $"{parameter.Name}:{parameter.Attributes}={(parameter.HasDefaultValue ? FormatDefault(parameter.DefaultValue) : string.Empty)}")) + ")")),
            Mapping(typeof(IProxyOfProxyParameters), typeof(ProxyOfProxyParametersTarget)));

        static string FormatDefault(object? value) => value is DateTime dateTime ? dateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    public interface IProxyOfProxyParameters
    {
        string Name { get; set; }

        string Format(int value, [Optional, DateTimeConstant(630822816000000000)] DateTime when, string text = "x", int count = 3, CancellationToken token = default, decimal amount = 1.5m, nint offset = 5);

        bool TryGetLast(string key, out string? value);
    }

    public class ProxyOfProxyParametersTarget
    {
        public string Name { get; set; } = "name";

        public string Format(int value, DateTime when, string text, int count, CancellationToken token, decimal amount, nint offset) => text;

        public bool TryGetLast(string key, out string? value)
        {
            value = key;
            return true;
        }
    }

    public interface IProxyOfProxyHeaders
    {
        string Name { get; }

        bool TryGetLast(string key, out string? value);
    }

    public interface IProxyOfProxyName
    {
        string Name { get; }
    }

    [DuckAsClass]
    public interface IProxyOfProxyNameClass
    {
        string Name { get; }
    }

    public class ProxyOfProxyHeaders
    {
        public string Name => "headers";

        public bool TryGetLast(string key, out string? value)
        {
            value = key + "!";
            return true;
        }
    }
}
