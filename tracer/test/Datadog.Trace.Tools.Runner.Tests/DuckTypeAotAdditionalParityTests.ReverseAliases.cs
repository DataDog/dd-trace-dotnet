// <copyright file="DuckTypeAotAdditionalParityTests.ReverseAliases.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Globalization;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Tools.Runner.DuckTypeAot;
using FluentAssertions;
using Xunit;

#pragma warning disable SA1201 // Nested test fixtures follow the test methods.

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// Scenarios where a forward proxy is created for a reverse proxy instance: dynamic duck typing creates it for the runtime type
/// of the instance, the reverse proxy type, which the registry generates.
/// </summary>
public partial class DuckTypeAotAdditionalParityTests
{
    [Theory]
    // Interface contracts get struct reverse proxies (like in dynamic duck typing), [DuckAsClass] ones class proxies. The
    // ToString of the forward proxy is the one of the reverse proxy, which calls the delegation's.
    [InlineData("interface-contract")]
    [InlineData("duck-as-class-interface-contract")]
    [InlineData("abstract-class-contract")]
    public void GeneratedRegistryShouldCreateForwardProxiesOfReverseProxiesLikeDynamicMode(string scenario)
    {
        var contract = scenario switch
        {
            "interface-contract" => typeof(IAliasContract),
            "duck-as-class-interface-contract" => typeof(IAliasClassContract),
            _ => typeof(AliasAbstractContract),
        };

        AssertSameOutcome(
            "n|delegation-tostring|delegation-tostring|delegation-tostring|True",
            () =>
            {
                var reverse = DuckType.CreateReverse(contract, new AliasDelegation());
                var forward = DuckType.Create<IAliasForward>(reverse)!;
                return FormattableString.Invariant($"{forward.Name}|{reverse}|{forward}|{((IDuckType)forward).ToString()}|{((IDuckType)forward).Type == reverse.GetType()}");
            },
            Mapping(contract, typeof(AliasDelegation), reverse: true),
            Mapping(typeof(IAliasForward), contract));
    }

    [Theory]
    // When the delegation doesn't override object.ToString, or hides it with a `new virtual` ToString(), the ToString of the
    // reverse proxy is another method than object.ToString: like dynamic duck typing, IDuckType.ToString of the forward proxy
    // calls it.
    [InlineData("interface-contract", "plain", "delegation-name|proxy-name")]
    [InlineData("duck-as-class-interface-contract", "plain", "delegation-name|proxy-name")]
    [InlineData("abstract-class-contract", "plain", "delegation-name|proxy-name")]
    [InlineData("interface-contract", "new-virtual", "new-virtual|proxy-name")]
    public void GeneratedRegistryShouldCallTheToStringOfReverseProxiesLikeDynamicMode(string scenario, string delegation, string expected)
    {
        var contract = scenario switch
        {
            "interface-contract" => typeof(IAliasContract),
            "duck-as-class-interface-contract" => typeof(IAliasClassContract),
            _ => typeof(AliasAbstractContract),
        };
        var delegationType = delegation == "plain" ? typeof(AliasPlainDelegation) : typeof(AliasNewToStringDelegation);

        AssertSameOutcome(
            expected,
            () =>
            {
                var forward = DuckType.Create<IAliasForward>(DuckType.CreateReverse(contract, Activator.CreateInstance(delegationType)!))!;
                return Describe(((IDuckType)forward).ToString()) + "|" + Describe(forward.ToString());

                string? Describe(string? text) => text == delegationType.ToString() ? "delegation-name" : text == forward.GetType().ToString() ? "proxy-name" : text;
            },
            Mapping(contract, delegationType, reverse: true),
            Mapping(typeof(IAliasForward), contract));
    }

    [Theory]
    // Mappings of the same proxy type spelled differently (the assembly of a generic argument, the casing of the proxy assembly)
    // to targets of the reverse contract: the generated reverse proxy type gets one registration of the proxy type.
    [InlineData("generic-argument-assembly")]
    [InlineData("proxy-assembly-casing")]
    public void GeneratedRegistryShouldRegisterTheProxyOfAReverseProxyOnceWhateverItsSpelling(string scenario)
    {
        var assemblyName = typeof(AliasEchoContract).Assembly.GetName().Name!;
        var genericProxy = scenario == "generic-argument-assembly";
        var proxyType = genericProxy ? typeof(IAliasGenericEcho<string>) : typeof(IAliasEchoForward);
        var otherSpelling = new DuckTypeAotMapping(
            genericProxy ? typeof(IAliasGenericEcho<>).FullName + "[[System.String, mscorlib]]" : proxyType.FullName!,
            genericProxy ? assemblyName : assemblyName.ToLowerInvariant(),
            typeof(AliasEchoContract).FullName!,
            assemblyName,
            DuckTypeAotMappingMode.Forward,
            DuckTypeAotMappingSource.MapFile);

        AssertSameOutcome(
            "contract:x",
            () =>
            {
                var reverse = DuckType.CreateReverse(typeof(AliasEchoContract), new AliasEchoDelegation());
                return genericProxy ? DuckType.Create<IAliasGenericEcho<string>>(reverse)!.Echo("x") : DuckType.Create<IAliasEchoForward>(reverse)!.Echo("x");
            },
            Mapping(typeof(AliasEchoContract), typeof(AliasEchoDelegation), reverse: true),
            Mapping(proxyType, typeof(AliasEchoBase)),
            otherSpelling);
    }

    [Theory]
    // A struct reverse proxy (interface contract) can't be changed through a forward proxy, like any struct; a class one can.
    [InlineData("interface-contract", "throws:DuckTypeStructMembersCannotBeChangedException")]
    [InlineData("duck-as-class-interface-contract", "x")]
    public void GeneratedRegistryShouldNotChangeStructReverseProxiesThroughForwardProxies(string scenario, string expected)
    {
        var contract = scenario == "interface-contract" ? typeof(IAliasSettableContract) : typeof(IAliasSettableClassContract);
        AssertSameOutcome(
            expected,
            () =>
            {
                var forward = DuckType.Create<IAliasSettableForward>(DuckType.CreateReverse(contract, new AliasSettableDelegation()))!;
                forward.Name = "x";
                return forward.Name;
            },
            Mapping(contract, typeof(AliasSettableDelegation), reverse: true),
            Mapping(typeof(IAliasSettableForward), contract));
    }

    [Theory]
    // A class proxy with a sealed ToString can't be created for a reverse proxy, which has a ToString of its own (the proxy
    // overrides it), but it can for the mapped interface: the mapping is compatible, and both modes fail for the reverse proxy.
    [InlineData("interface-contract")]
    [InlineData("duck-as-class-interface-contract")]
    public void GeneratedRegistryShouldReplayFailuresOfProxiesOfReverseProxiesOnly(string scenario)
    {
        var contract = scenario == "interface-contract" ? typeof(IAliasContract) : typeof(IAliasClassContract);
        Func<object?> exercise = () => DuckType.Create<AliasSealedToStringForward>(DuckType.CreateReverse(contract, new AliasDelegation()))!.Name;

        DuckType.ResetRuntimeModeForTests();
        var expected = CaptureOutcome(exercise);
        expected.Should().Be("throws:DuckTypeException>TypeLoadException");
        WithGeneratedRegistry(
            () => CaptureOutcome(exercise).Should().Be(expected),
            matrix => matrix.Mappings.Should().OnlyContain(mapping => mapping.Status == DuckTypeAotCompatibilityStatuses.Compatible),
            Mapping(contract, typeof(AliasDelegation), reverse: true),
            Mapping(typeof(AliasSealedToStringForward), contract));
    }

    [Theory]
    // A closed generic mapped target, implemented by a reverse contract (struct or class reverse proxy).
    [InlineData("interface-contract")]
    [InlineData("abstract-class-contract")]
    public void GeneratedRegistryShouldCreateForwardProxiesOfReverseProxiesForClosedGenericTargets(string scenario)
    {
        var contract = scenario == "interface-contract" ? typeof(IAliasGenericContract) : typeof(AliasGenericAbstractContract);
        AssertSameOutcome(
            "g|True",
            () =>
            {
                var reverse = DuckType.CreateReverse(contract, new AliasGenericDelegation());
                var forward = DuckType.Create<IAliasGenericForward>(reverse)!;
                return FormattableString.Invariant($"{forward.Get()}|{((IDuckType)forward).Type == reverse.GetType()}");
            },
            Mapping(contract, typeof(AliasGenericDelegation), reverse: true),
            Mapping(typeof(IAliasGenericForward), typeof(IAliasGenericBase<string>)));
    }

    [Fact]
    public void GeneratedRegistryShouldCreateClosedGenericForwardProxiesOfReverseProxies()
    {
        AssertSameOutcome(
            "g",
            () => DuckType.Create<IAliasGenericEcho<string>>(DuckType.CreateReverse(typeof(ReverseDescendant), new AnnotatedDelegation()))!.Echo("g"),
            Mapping(typeof(ReverseDescendant), typeof(AnnotatedDelegation), reverse: true),
            Mapping(typeof(IAliasGenericEcho<string>), typeof(ReverseAncestor)));
    }

    [Theory]
    // A virtual overload with the same number of parameters, declared between the mapped target and the reverse contract,
    // doesn't start another slot: the forward proxy calls the override of the contract.
    [InlineData("intermediate-overload", "contract:x")]
    // The reverse contract hides a member of the mapped target with `new`: like dynamic duck typing, which binds the members of
    // the runtime type, the proxy binds the contract's.
    [InlineData("hidden-member", "contract|e")]
    // The reverse proxy type has members of its own: its IDuckType.Instance (the delegation instance) hides the contract's
    // Instance property, and its ToString is the delegation's (a `new virtual` one included).
    [InlineData("instance-of-class-contract", "delegation")]
    [InlineData("tostring-of-plain-delegation", "AliasInstanceDelegation")]
    [InlineData("tostring-of-new-virtual-delegation", "delegation-new-virtual")]
    // A struct reverse proxy of an interface declaring `object Instance`.
    [InlineData("instance-of-struct-reverse-proxy", "delegation|delegation-instance")]
    public void GeneratedRegistryShouldBindTheMembersOfReverseProxiesLikeDynamicMode(string scenario, string expected)
    {
        var (exercise, mappings) = scenario switch
        {
            "instance-of-class-contract" => ((Func<object?>)(() =>
            {
                var delegation = new AliasInstanceDelegation();
                return DescribeAliasValue(DuckType.Create<IAliasInstanceView>(DuckType.CreateReverse(typeof(AliasInstanceContract), delegation))!.Value, delegation);
            }),
            new[] { Mapping(typeof(AliasInstanceContract), typeof(AliasInstanceDelegation), reverse: true), Mapping(typeof(IAliasInstanceView), typeof(AliasInstanceContract)) }),
            "tostring-of-plain-delegation" => (() => DuckType.Create<IAliasDescribeView>(DuckType.CreateReverse(typeof(AliasInstanceContract), new AliasInstanceDelegation()))!.Describe().Replace(typeof(DuckTypeAotAdditionalParityTests).FullName + "+", string.Empty),
                                              new[] { Mapping(typeof(AliasInstanceContract), typeof(AliasInstanceDelegation), reverse: true), Mapping(typeof(IAliasDescribeView), typeof(AliasInstanceContract)) }),
            "tostring-of-new-virtual-delegation" => (() => DuckType.Create<IAliasDescribeView>(DuckType.CreateReverse(typeof(AliasToStringContract), new AliasNewToStringInstanceDelegation()))!.Describe(),
                                                    new[] { Mapping(typeof(AliasToStringContract), typeof(AliasNewToStringInstanceDelegation), reverse: true), Mapping(typeof(IAliasDescribeView), typeof(AliasToStringContract)) }),
            "instance-of-struct-reverse-proxy" => (() =>
            {
                var delegation = new AliasInstanceMemberDelegation();
                var reverse = DuckType.CreateReverse(typeof(IAliasInstanceMemberContract), delegation);
                return DescribeAliasValue(((IAliasInstanceMemberContract)reverse).Instance, delegation) + "|" + DescribeAliasValue(DuckType.Create<IAliasInstanceView>(reverse)!.Value, delegation);
            },
            new[] { Mapping(typeof(IAliasInstanceMemberContract), typeof(AliasInstanceMemberDelegation), reverse: true), Mapping(typeof(IAliasInstanceView), typeof(IAliasInstanceMemberContract)) }),
            "intermediate-overload" => ((Func<object?>)(() => DuckType.Create<IAliasEchoForward>(DuckType.CreateReverse(typeof(AliasOverloadContract), new AliasOverloadDelegation()))!.Echo("x")),
                                        new[] { Mapping(typeof(AliasOverloadContract), typeof(AliasOverloadDelegation), reverse: true), Mapping(typeof(IAliasEchoForward), typeof(AliasOverloadBase)) }),
            _ => (() =>
            {
                var forward = DuckType.Create<IAliasNamedEchoForward>(DuckType.CreateReverse(typeof(AliasHidingContract), new AliasHidingDelegation()))!;
                return forward.Name + "|" + forward.Echo("e");
            },
            new[] { Mapping(typeof(AliasHidingContract), typeof(AliasHidingDelegation), reverse: true), Mapping(typeof(IAliasNamedEchoForward), typeof(AliasHidingBase)) }),
        };

        AssertSameOutcome(expected, exercise, mappings);

        static string DescribeAliasValue(object? value, object delegation)
            => ReferenceEquals(value, delegation) ? "delegation" : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "null";
    }

    [Fact]
    public void GeneratedRegistryShouldReplayForwardFailuresForReverseProxies()
    {
        // The forward mapping fails like in dynamic duck typing: so does the proxy of a reverse proxy instance, which isn't left
        // without registration.
        AssertSameOutcome(
            "throws:DuckTypeTargetMethodNotFoundException",
            () => DuckType.Create<IAliasMissingForward>(DuckType.CreateReverse(typeof(IAliasContract), new AliasDelegation())),
            Mapping(typeof(IAliasContract), typeof(AliasDelegation), reverse: true),
            Mapping(typeof(IAliasMissingForward), typeof(IAliasContract)));
    }

    public interface IAliasContract
    {
        string Name { get; }
    }

    [DuckAsClass]
    public interface IAliasClassContract
    {
        string Name { get; }
    }

    public abstract class AliasAbstractContract
    {
        public abstract string Name { get; }
    }

    public class AliasDelegation
    {
        [DuckReverseMethod]
        public string Name => "n";

        public override string ToString() => "delegation-tostring";
    }

    public interface IAliasForward
    {
        string Name { get; }
    }

    public class AliasPlainDelegation
    {
        [DuckReverseMethod]
        public string Name => "n";
    }

    public class AliasNewToStringDelegation
    {
        [DuckReverseMethod]
        public string Name => "n";

        public new virtual string ToString() => "new-virtual";
    }

    public abstract class AliasEchoBase
    {
        public virtual string Echo(string value) => "base:" + value;

        public abstract string Other();
    }

    public abstract class AliasEchoContract : AliasEchoBase
    {
        public override string Echo(string value) => "contract:" + value;
    }

    public class AliasEchoDelegation
    {
        [DuckReverseMethod]
        public string Other() => "other";
    }

    public interface IAliasSettableContract
    {
        string Name { get; set; }
    }

    [DuckAsClass]
    public interface IAliasSettableClassContract
    {
        string Name { get; set; }
    }

    public class AliasSettableDelegation
    {
        [DuckReverseMethod]
        public string Name { get; set; } = "n";
    }

    public interface IAliasSettableForward
    {
        string Name { get; set; }
    }

    public abstract class AliasSealedToStringForward
    {
        public abstract string Name { get; }

        public sealed override string ToString() => "forward";
    }

    public interface IAliasMissingForward
    {
        string Name { get; }

        string Missing();
    }

    public interface IAliasGenericBase<T>
    {
        T Get();
    }

    public interface IAliasGenericContract : IAliasGenericBase<string>
    {
    }

    public abstract class AliasGenericAbstractContract : IAliasGenericBase<string>
    {
        public abstract string Get();
    }

    public class AliasGenericDelegation
    {
        [DuckReverseMethod]
        public string Get() => "g";
    }

    public interface IAliasGenericForward
    {
        string Get();
    }

    public interface IAliasGenericEcho<T>
    {
        T Echo(T value);
    }

    public abstract class AliasOverloadBase
    {
        public virtual string Echo(string value) => "base:" + value;

        public abstract string Other();
    }

    public abstract class AliasOverloadMid : AliasOverloadBase
    {
        public virtual string Echo(object value) => "mid-overload";
    }

    public abstract class AliasOverloadContract : AliasOverloadMid
    {
        public override string Echo(string value) => "contract:" + value;
    }

    public class AliasOverloadDelegation
    {
        [DuckReverseMethod]
        public string Other() => "other";
    }

    public interface IAliasEchoForward
    {
        string Echo(string value);
    }

    public abstract class AliasHidingBase
    {
        public string Name => "base";

        public abstract string Echo(string value);
    }

    public abstract class AliasHidingContract : AliasHidingBase
    {
        public new string Name => "contract";
    }

    public class AliasHidingDelegation
    {
        [DuckReverseMethod]
        public string Echo(string value) => value;
    }

    public interface IAliasNamedEchoForward
    {
        string Name { get; }

        string Echo(string value);
    }

    public abstract class AliasInstanceContract
    {
        public abstract string Name { get; }

        public virtual object Instance => "contract-instance";
    }

    public abstract class AliasToStringContract
    {
        public abstract string Name { get; }

        public override string ToString() => "contract-tostring";
    }

    public class AliasInstanceDelegation
    {
        [DuckReverseMethod]
        public string Name => "n";
    }

    public class AliasNewToStringInstanceDelegation
    {
        [DuckReverseMethod]
        public string Name => "n";

        public new virtual string ToString() => "delegation-new-virtual";
    }

    public interface IAliasInstanceMemberContract
    {
        object Instance { get; }

        string Name { get; }
    }

    public class AliasInstanceMemberDelegation
    {
        [DuckReverseMethod]
        public object Instance => "delegation-instance";

        [DuckReverseMethod]
        public string Name => "n";
    }

    public interface IAliasInstanceView
    {
        [Duck(Name = "Instance")]
        object Value { get; }
    }

    public interface IAliasDescribeView
    {
        [Duck(Name = "ToString")]
        string Describe();
    }
}
