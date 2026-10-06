// <copyright file="DuckTypeAotAdditionalParityTests.MemberSelection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Tools.Runner.DuckTypeAot;
using FluentAssertions;
using Xunit;
using parityAlias = global::Datadog.Trace.Tools.Runner.Tests.ParityContracts;

#pragma warning disable SA1201 // Nested test fixtures follow the test methods.
#pragma warning disable SA1402 // The contracts implemented through a namespace alias are declared in their own namespace.
#pragma warning disable SA1403 // Single namespace

namespace Datadog.Trace.Tools.Runner.Tests
{
    /// <summary>
    /// Scenarios where the AOT registry has to select the same target member as dynamic duck typing.
    /// </summary>
    public partial class DuckTypeAotAdditionalParityTests
    {
        [Theory]
        // Like protoc messages: a property with the plain name wins over the (alias-qualified) explicit implementation.
        [InlineData("alias-explicit-with-static", "static")]
        // Without a plain property, an alias-qualified explicit name isn't "<Interface>.<Name>": not found in both engines.
        [InlineData("alias-explicit-only", "throws:DuckTypePropertyOrFieldNotFoundException")]
        // The plain name wins over a non-alias explicit implementation too.
        [InlineData("explicit-with-static", "static")]
        // Without a plain property, the explicit implementation is used.
        [InlineData("explicit-only", "explicit")]
        // With IgnoreCase, the properties with the plain name in another case aren't explicit implementations either: they follow
        // the rules of plain properties (the exact name wins).
        [InlineData("ignore-case-plain-candidates", "exact")]
        public void GeneratedRegistryShouldResolveExplicitInterfacePropertiesLikeDynamicMode(string scenario, string expected)
        {
            var (proxyType, targetType, exercise) = scenario switch
            {
                "ignore-case-plain-candidates" => (typeof(IIgnoreCaseExplicitProxy), typeof(IgnoreCaseCandidatesTarget), (Func<object?>)(() => DuckType.Create<IIgnoreCaseExplicitProxy>(new IgnoreCaseCandidatesTarget())!.Value)),
                "alias-explicit-with-static" => (typeof(IDescriptorProxy), typeof(AliasExplicitWithStaticMessage), (Func<object?>)(() => DuckType.Create<IDescriptorProxy>(new AliasExplicitWithStaticMessage())!.Descriptor)),
                "alias-explicit-only" => (typeof(IDescriptorProxy), typeof(AliasExplicitOnlyMessage), () => DuckType.Create<IDescriptorProxy>(new AliasExplicitOnlyMessage())!.Descriptor),
                "explicit-with-static" => (typeof(IDescriptorProxy), typeof(ExplicitWithStaticMessage), () => DuckType.Create<IDescriptorProxy>(new ExplicitWithStaticMessage())!.Descriptor),
                _ => (typeof(IDescriptorProxy), typeof(ExplicitOnlyMessage), () => DuckType.Create<IDescriptorProxy>(new ExplicitOnlyMessage())!.Descriptor),
            };

            AssertSameOutcome(expected, exercise, Mapping(proxyType, targetType));
        }

        [Fact]
        public void GeneratedRegistryShouldResolveExplicitGenericInterfaceMethodNamesWithCommas()
        {
            // The comma inside the generic arguments of the explicit implementation name isn't a fallback separator.
            AssertSameOutcome(
                "explicit:a:1",
                () => DuckType.Create<IGenericPairEchoProxy>(new GenericPairEchoTarget())!.Echo("a", 1),
                Mapping(typeof(IGenericPairEchoProxy), typeof(GenericPairEchoTarget)));
        }

        [Theory]
        // An inherited override: Type.GetMember returns the most derived property only (like Exception.Message).
        [InlineData("inherited-override", "override")]
        // A hiding property with another type: the proxy property type selects the base one.
        [InlineData("hidden-by-type", "base")]
        // [DuckField] fallback names: the first name found on the most derived type wins.
        [InlineData("field-fallback-names", "derived-b")]
        // Public binding flags find a public property: its private setter can be written, like dynamic duck typing does.
        [InlineData("private-setter", "updated")]
        // Non public binding flags don't find a public property, even if its setter is private.
        [InlineData("private-setter-non-public-flags", "throws:DuckTypePropertyOrFieldNotFoundException")]
        // [Duck] on the overridden abstract property of a class proxy is inherited by the override.
        [InlineData("class-proxy-inherited-duck", "other")]
        // A [DuckCopy] field honors ExplicitInterfaceTypeName.
        [InlineData("duck-copy-explicit", "explicit")]
        // Overloaded indexers: the one with the proxy's index parameter types.
        [InlineData("indexer-overloads", "string:k")]
        // IgnoreCase with names that only differ by case: the exact name.
        [InlineData("ignore-case", "Name")]
        public void GeneratedRegistryShouldSelectTheSameTargetPropertyOrFieldAsDynamicMode(string scenario, string expected)
        {
            var (proxyType, targetType, exercise) = scenario switch
            {
                "inherited-override" => (typeof(IMessageProxy), typeof(MessageLeaf), (Func<object?>)(() => DuckType.Create<IMessageProxy>(new MessageLeaf())!.Message)),
                "hidden-by-type" => (typeof(IObjectValueProxy), typeof(HidingValueTarget), () => DuckType.Create<IObjectValueProxy>(new HidingValueTarget())!.Value),
                "field-fallback-names" => (typeof(IFieldFallbackProxy), typeof(FieldFallbackDerived), () => DuckType.Create<IFieldFallbackProxy>(new FieldFallbackDerived())!.Value),
                "private-setter" => (typeof(IPublicFlagsNameProxy), typeof(PrivateSetterTarget), () =>
                {
                    var proxy = DuckType.Create<IPublicFlagsNameProxy>(new PrivateSetterTarget())!;
                    proxy.Name = "updated";
                    return proxy.Name;
                }),
                "private-setter-non-public-flags" => (typeof(INonPublicFlagsNameProxy), typeof(PrivateSetterTarget), () =>
                {
                    var proxy = DuckType.Create<INonPublicFlagsNameProxy>(new PrivateSetterTarget())!;
                    proxy.Name = "updated";
                    return proxy.Name;
                }),
                "class-proxy-inherited-duck" => (typeof(DerivedRenamedClassProxy), typeof(RenamedValueTarget), () => DuckType.Create<DerivedRenamedClassProxy>(new RenamedValueTarget())!.Value),
                "indexer-overloads" => (typeof(IStringIndexerProxy), typeof(OverloadedIndexerTarget), () => DuckType.Create<IStringIndexerProxy>(new OverloadedIndexerTarget())!["k"]),
                "ignore-case" => (typeof(IIgnoreCaseNameProxy), typeof(CaseVariantNamesTarget), () => DuckType.Create<IIgnoreCaseNameProxy>(new CaseVariantNamesTarget())!.Name),
                _ => (typeof(ExplicitDescriptorCopy), typeof(ExplicitOnlyMessage), () => DuckType.Create<ExplicitDescriptorCopy>(new ExplicitOnlyMessage()).Descriptor),
            };

            AssertSameOutcome(expected, exercise, Mapping(proxyType, targetType));
        }

        [Theory]
        // Dynamic duck typing implements one member per signature (the first interface's), which implements the members of every
        // interface with that signature.
        [InlineData("properties")]
        [InlineData("methods")]
        [InlineData("methods-class-proxy")]
        public void GeneratedRegistryShouldImplementDiamondMembersLikeDynamicMode(string scenario)
        {
            var (mapping, exercise) = scenario switch
            {
                "properties" => (Mapping(typeof(IDiamondProxy), typeof(DiamondTarget)), (Func<object?>)(() =>
                {
                    var proxy = DuckType.Create<IDiamondProxy>(new DiamondTarget())!;
                    return ((IDiamondA)proxy).Value + "|" + ((IDiamondB)proxy).Value;
                })),
                "methods" => (Mapping(typeof(IDiamondMethodsProxy), typeof(DiamondTarget)), () =>
                {
                    var proxy = DuckType.Create<IDiamondMethodsProxy>(new DiamondTarget())!;
                    return ((IDiamondMethodA)proxy).Get() + "|" + ((IDiamondMethodB)proxy).Get();
                }),
                _ => (Mapping(typeof(IDiamondMethodsClassProxy), typeof(DiamondTarget)), () =>
                {
                    var proxy = DuckType.Create<IDiamondMethodsClassProxy>(new DiamondTarget())!;
                    return ((IDiamondMethodA)proxy).Get() + "|" + ((IDiamondMethodB)proxy).Get();
                }),
            };

            AssertSameOutcome("a|a", exercise, mapping);
        }

        [Theory]
        // IDuckType.ToString() returns the target's ToString(), even when the target doesn't override object.ToString.
        [InlineData("plain-target")]
        [InlineData("class-proxy")]
        [InlineData("constrained-generic")]
        [InlineData("struct-target")]
        public void GeneratedRegistryShouldImplementIDuckTypeToStringLikeDynamicMode(string scenario)
        {
            var (mapping, exercise) = scenario switch
            {
                "plain-target" => (Mapping(typeof(INamedProxy), typeof(BaseNamedTarget)), (Func<object?>)(() => DescribeDuckTypeToString((IDuckType)DuckType.Create<INamedProxy>(new BaseNamedTarget())!))),
                "class-proxy" => (Mapping(typeof(INamedAsClassProxy), typeof(BaseNamedTarget)), () => DescribeDuckTypeToString((IDuckType)DuckType.Create<INamedAsClassProxy>(new BaseNamedTarget())!)),
                "constrained-generic" => (Mapping(typeof(INamedDuckProxy), typeof(BaseNamedTarget)), () => CallToString(DuckType.Create<INamedDuckProxy>(new BaseNamedTarget())!)),
                _ => (Mapping(typeof(INamedProxy), typeof(NamedStruct)), () =>
                {
                    var proxy = DuckType.Create<INamedProxy>(new NamedStruct())!;
                    return DescribeDuckTypeToString((IDuckType)proxy) + "|" + DescribeObjectToString(proxy);
                }),
            };

            DuckType.ResetRuntimeModeForTests();
            var expected = CaptureOutcome(exercise);
            expected.Should().NotStartWith("proxy-name");
            AssertSameOutcome(expected, exercise, mapping);

            // Through the IDuckType.ToString slot, and through the object.ToString slot.
            static string DescribeDuckTypeToString(IDuckType proxy) => proxy.ToString() == proxy.GetType().ToString() ? "proxy-name" : proxy.ToString();

            static string DescribeObjectToString(object proxy) => proxy.ToString() == proxy.GetType().ToString() ? "proxy-name" : proxy.ToString()!;

            static string CallToString<T>(T proxy)
                where T : INamedDuckProxy
                => proxy.ToString() == proxy.GetType().ToString() ? "proxy-name" : proxy.ToString()!;
        }

        [Theory]
        // Dynamic duck typing implements the accessors of the public properties of the proxy (the most derived declaration of a
        // class property, with its own accessors), and no event accessors: the others keep the proxy's implementation.
        [InlineData("getter-only-override", "target|target")]
        [InlineData("protected-property", "n|proxy-secret")]
        [InlineData("virtual-event", "n|False")]
        // [DuckIgnore] on an accessor: dynamic duck typing reads the attributes of the property, which it implements.
        [InlineData("ignore-attribute-on-accessor", "n|target-initial")]
#if NETCOREAPP3_0_OR_GREATER
        [InlineData("internal-interface-property", "n|dim")]
#endif
        // All the properties an interface declares are implemented, overloaded indexers too.
        [InlineData("indexers", "int:1|string:a")]
        [InlineData("duck-as-class-indexers", "int:2|string:b|i2=x;sb=y;")]
        // Type.GetProperties() keeps the base virtual property a static property of the proxy (another signature), or a property of
        // a base type that isn't inherited (all its accessors are private), or one with another signature (`new string Value`
        // over a `virtual T Value` closed with string) declares with the same name: dynamic duck typing implements it.
        [InlineData("static-new-property", "target-initial|static")]
        [InlineData("static-new-property-in-base", "target-initial|n")]
        [InlineData("private-new-property-in-base", "target-initial|n")]
        [InlineData("new-property-over-generic-base", "target-initial|new")]
        public void GeneratedRegistryShouldImplementTheAccessorsDynamicModeImplements(string scenario, string expected)
        {
            var (mapping, exercise) = scenario switch
            {
                "getter-only-override" => (Mapping(typeof(GetterOnlyOverrideProxy), typeof(AccessorTarget)), (Func<object?>)(() =>
                {
                    var target = new AccessorTarget { Value = "target" };
                    var proxy = DuckType.Create<GetterOnlyOverrideProxy>(target)!;
                    proxy.Value = "updated";
                    return proxy.Value + "|" + target.Value;
                })),
                "ignore-attribute-on-accessor" => (Mapping(typeof(IIgnoredAccessorProxy), typeof(AccessorTarget)), () =>
                {
                    var proxy = DuckType.Create<IIgnoredAccessorProxy>(new AccessorTarget())!;
                    return proxy.Name + "|" + proxy.Value;
                }),
                "protected-property" => (Mapping(typeof(ProtectedPropertyProxy), typeof(AccessorTarget)), () =>
                {
                    var proxy = DuckType.Create<ProtectedPropertyProxy>(new AccessorTarget())!;
                    return proxy.Name + "|" + proxy.ReadSecret();
                }),
#if NETCOREAPP3_0_OR_GREATER
                "internal-interface-property" => (Mapping(typeof(IInternalPropertyProxy), typeof(AccessorTarget)), () =>
                {
                    var proxy = DuckType.Create<IInternalPropertyProxy>(new AccessorTarget())!;
                    return proxy.Name + "|" + proxy.Secret;
                }),
#endif
                "indexers" => (Mapping(typeof(IIndexersProxy), typeof(IndexersTarget)), () =>
                {
                    var proxy = DuckType.Create<IIndexersProxy>(new IndexersTarget())!;
                    return proxy[1] + "|" + proxy["a"];
                }),
                "duck-as-class-indexers" => (Mapping(typeof(IIndexersClassProxy), typeof(IndexersTarget)), () =>
                {
                    var target = new IndexersTarget();
                    var proxy = DuckType.Create<IIndexersClassProxy>(target)!;
                    proxy[2] = "x";
                    proxy["b"] = "y";
                    return proxy[2] + "|" + proxy["b"] + "|" + target.Log;
                }),
                "static-new-property" => (Mapping(typeof(StaticNewValueProxy), typeof(AccessorTarget)), () =>
                    ((VirtualValueBaseProxy)DuckType.Create<StaticNewValueProxy>(new AccessorTarget())!).Value + "|" + StaticNewValueProxy.Value),
                "static-new-property-in-base" => (Mapping(typeof(StaticNewValueLeafProxy), typeof(AccessorTarget)), () =>
                {
                    var proxy = DuckType.Create<StaticNewValueLeafProxy>(new AccessorTarget())!;
                    return ((VirtualValueBaseProxy)proxy).Value + "|" + proxy.Name;
                }),
                "private-new-property-in-base" => (Mapping(typeof(PrivateNewValueLeafProxy), typeof(AccessorTarget)), () =>
                {
                    var proxy = DuckType.Create<PrivateNewValueLeafProxy>(new AccessorTarget())!;
                    return ((VirtualValueBaseProxy)proxy).Value + "|" + proxy.Name;
                }),
                "new-property-over-generic-base" => (Mapping(typeof(NewValueOverGenericBaseProxy), typeof(AccessorTarget)), () =>
                {
                    var proxy = DuckType.Create<NewValueOverGenericBaseProxy>(new AccessorTarget())!;
                    return ((GenericVirtualValueBaseProxy<string>)proxy).Value + "|" + proxy.Value;
                }),
                _ => (Mapping(typeof(VirtualEventProxy), typeof(AccessorTarget)), () =>
                {
                    var target = new AccessorTarget();
                    var proxy = DuckType.Create<VirtualEventProxy>(target)!;
                    var raised = false;
                    proxy.Changed += (_, _) => raised = true;
                    target.RaiseChanged();
                    return proxy.Name + "|" + raised;
                }),
            };

            AssertSameOutcome(expected, exercise, mapping);
        }

        [Theory]
        // A static ToString() hides the inherited one from Type.GetMethod: the proxy has no ToString of its own.
        [InlineData("static-tostring", "proxy-name|proxy-name")]
        // The `new virtual` ToString() of a generic target: IDuckType.ToString calls it, object.ToString doesn't.
        [InlineData("generic-new-virtual-tostring", "newvirtual|proxy-name")]
        // A proxy interface declaring ToString() binds it to the static ToString() of the target, which every slot reaches.
        [InlineData("proxy-declared-tostring", "static|static|static")]
        // Type.GetMethod doesn't return the static methods of base types: the static ToString() of a base type doesn't hide the
        // override further up.
        [InlineData("static-tostring-in-base", "override-base|override-base")]
        public void GeneratedRegistryShouldUseTheTargetToStringLikeDynamicMode(string scenario, string expected)
        {
            var (mapping, exercise) = scenario switch
            {
                "static-tostring" => (Mapping(typeof(INamedProxy), typeof(StaticToStringTarget)), (Func<object?>)(() => DescribeBothToStrings(DuckType.Create<INamedProxy>(new StaticToStringTarget())!))),
                "generic-new-virtual-tostring" => (Mapping(typeof(INamedProxy), typeof(GenericNewVirtualToStringTarget<int>)), () => DescribeBothToStrings(DuckType.Create<INamedProxy>(new GenericNewVirtualToStringTarget<int>())!)),
                "static-tostring-in-base" => (Mapping(typeof(INamedProxy), typeof(StaticToStringInBaseTarget)), () => DescribeBothToStrings(DuckType.Create<INamedProxy>(new StaticToStringInBaseTarget())!)),
                _ => (Mapping(typeof(IToStringDeclaringProxy), typeof(StaticToStringTarget)), () =>
                {
                    var proxy = DuckType.Create<IToStringDeclaringProxy>(new StaticToStringTarget())!;
                    return Describe(proxy, proxy.ToString()) + "|" + DescribeBothToStrings(proxy);
                }),
            };

            AssertSameOutcome(expected, exercise, mapping);

            // Through the IDuckType.ToString slot, and through the object.ToString slot.
            static string DescribeBothToStrings(object proxy) => Describe(proxy, ((IDuckType)proxy).ToString()) + "|" + Describe(proxy, proxy.ToString());

            static string Describe(object proxy, string? text) => text == proxy.GetType().ToString() ? "proxy-name" : text ?? "null";
        }

        /// <summary>
        /// Runs a scenario in dynamic mode and then with a generated registry, and checks both engines have the expected outcome:
        /// the returned value, or the exception types (outermost first) prefixed with "throws:".
        /// </summary>
        private static void AssertSameOutcome(string expected, Func<object?> exercise, params DuckTypeAotMapping[] mappings)
        {
            DuckType.ResetRuntimeModeForTests();
            CaptureOutcome(exercise).Should().Be(expected, "dynamic duck typing should behave as expected");
            WithGeneratedRegistry(() => CaptureOutcome(exercise).Should().Be(expected, "AOT duck typing should behave like dynamic duck typing"), mappings);
        }

        private static string CaptureOutcome(Func<object?> exercise)
        {
            try
            {
                return Convert.ToString(exercise(), CultureInfo.InvariantCulture) ?? "null";
            }
            catch (Exception ex)
            {
                var exceptionTypes = new List<string>();
                for (Exception? current = ex; current is not null; current = current.InnerException)
                {
                    exceptionTypes.Add(current.GetType().Name);
                }

                return "throws:" + string.Join(">", exceptionTypes);
            }
        }

        public interface IDescriptorProxy
        {
            [Duck(ExplicitInterfaceTypeName = "Datadog.Trace.Tools.Runner.Tests.ParityContracts.IDescriptorMessage")]
            string Descriptor { get; }
        }

        public class AliasExplicitWithStaticMessage : parityAlias::IDescriptorMessage
        {
            public static string Descriptor => "static";

            // Like protoc output, the explicit implementation is named "parityAlias::...IDescriptorMessage.Descriptor".
            string parityAlias::IDescriptorMessage.Descriptor => "explicit";
        }

        public class AliasExplicitOnlyMessage : parityAlias::IDescriptorMessage
        {
            string parityAlias::IDescriptorMessage.Descriptor => "explicit";
        }

        public class ExplicitWithStaticMessage : ParityContracts.IDescriptorMessage
        {
            public static string Descriptor => "static";

            string ParityContracts.IDescriptorMessage.Descriptor => "explicit";
        }

        public class ExplicitOnlyMessage : ParityContracts.IDescriptorMessage
        {
            string ParityContracts.IDescriptorMessage.Descriptor => "explicit";
        }

        [DuckCopy]
        public struct ExplicitDescriptorCopy
        {
            [Duck(ExplicitInterfaceTypeName = "Datadog.Trace.Tools.Runner.Tests.ParityContracts.IDescriptorMessage")]
            public string Descriptor;
        }

        public interface IGenericPairEchoProxy
        {
            [Duck(Name = "Datadog.Trace.Tools.Runner.Tests.ParityContracts.IGenericPairEcho<System.String,System.Int32>.Echo")]
            string Echo(string first, int second);
        }

        public class GenericPairEchoTarget : ParityContracts.IGenericPairEcho<string, int>
        {
            string ParityContracts.IGenericPairEcho<string, int>.Echo(string first, int second) => FormattableString.Invariant($"explicit:{first}:{second}");
        }

        public interface IMessageProxy
        {
            string Message { get; }
        }

        public class MessageBase
        {
            public virtual string Message => "base";
        }

        public class MessageOverride : MessageBase
        {
            public override string Message => "override";
        }

        public class MessageLeaf : MessageOverride
        {
        }

        public interface IObjectValueProxy
        {
            object Value { get; }
        }

        public class HidingValueBase
        {
            public object Value => "base";
        }

        public class HidingValueTarget : HidingValueBase
        {
            public new string Value => "derived";
        }

        public interface IFieldFallbackProxy
        {
            [DuckField(Name = "_a,_b", FallbackToBaseTypes = true)]
            string Value { get; }
        }

        public class FieldFallbackBase
        {
#pragma warning disable SA1401, CS0414 // Read through duck typing
            private string _a = "base-a";
#pragma warning restore SA1401, CS0414
        }

        public class FieldFallbackDerived : FieldFallbackBase
        {
#pragma warning disable SA1401, CS0414 // Read through duck typing
            private string _b = "derived-b";
#pragma warning restore SA1401, CS0414
        }

        public interface IPublicFlagsNameProxy
        {
            [Duck(BindingFlags = BindingFlags.Public | BindingFlags.Instance)]
            string Name { get; set; }
        }

        public interface INonPublicFlagsNameProxy
        {
            [Duck(BindingFlags = BindingFlags.NonPublic | BindingFlags.Instance)]
            string Name { get; set; }
        }

        public class PrivateSetterTarget
        {
            public string Name { get; private set; } = "initial";
        }

        public abstract class BaseRenamedClassProxy
        {
            [Duck(Name = "Other")]
            public abstract string Value { get; }
        }

        public abstract class DerivedRenamedClassProxy : BaseRenamedClassProxy
        {
            public abstract override string Value { get; }
        }

        public class RenamedValueTarget
        {
            public string Value => "value";

            public string Other => "other";
        }

        public interface IDiamondA
        {
            [Duck(Name = "A")]
            string Value { get; }
        }

        public interface IDiamondB
        {
            [Duck(Name = "B")]
            string Value { get; }
        }

        public interface IDiamondProxy : IDiamondA, IDiamondB
        {
        }

        public interface IDiamondMethodA
        {
            [Duck(Name = "GetA")]
            string Get();
        }

        public interface IDiamondMethodB
        {
            [Duck(Name = "GetB")]
            string Get();
        }

        public interface IDiamondMethodsProxy : IDiamondMethodA, IDiamondMethodB
        {
        }

        [DuckAsClass]
        public interface IDiamondMethodsClassProxy : IDiamondMethodA, IDiamondMethodB
        {
        }

        public class DiamondTarget
        {
            public string A => "a";

            public string B => "b";

            public string GetA() => "a";

            public string GetB() => "b";
        }

        [DuckAsClass]
        public interface INamedAsClassProxy
        {
            string Name { get; }
        }

        public interface INamedDuckProxy : IDuckType
        {
            string Name { get; }
        }

        public struct NamedStruct
        {
            public string Name => "struct";
        }

        public class AccessorTarget
        {
            public event EventHandler? Changed;

            public string Name => "n";

            public string Value { get; set; } = "target-initial";

            public string Secret => "target-secret";

            public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
        }

        public class AccessorBaseProxy
        {
            public virtual string Value { get; set; } = "proxy";
        }

        public class GetterOnlyOverrideProxy : AccessorBaseProxy
        {
            // Only the getter is overridden: dynamic duck typing doesn't implement the inherited setter.
            public override string Value => base.Value;
        }

        public interface IIgnoredAccessorProxy
        {
            string Name { get; }

            string Value
            {
                [DuckIgnore]
                get;
            }
        }

        public abstract class ProtectedPropertyProxy
        {
            public abstract string Name { get; }

            protected virtual string Secret => "proxy-secret";

            public string ReadSecret() => Secret;
        }

        public abstract class VirtualEventProxy
        {
            public virtual event EventHandler? Changed;

            public abstract string Name { get; }

            protected void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
        }

#if NETCOREAPP3_0_OR_GREATER
        public interface IInternalPropertyProxy
        {
            string Name { get; }

            internal string Secret => "dim";
        }
#endif

        public class StaticToStringTarget
        {
            public string Name => "static-target";

            public static new string ToString() => "static";
        }

        public class OverrideToStringTarget
        {
            public string Name => "n";

            public override string ToString() => "override-base";
        }

        public class StaticToStringOverOverrideTarget : OverrideToStringTarget
        {
            public static new string ToString() => "static";
        }

        public class StaticToStringInBaseTarget : StaticToStringOverOverrideTarget
        {
        }

        public interface IIndexersProxy
        {
            string this[int index] { get; }

            string this[string key] { get; }
        }

        [DuckAsClass]
        public interface IIndexersClassProxy
        {
            string this[int index] { get; set; }

            string this[string key] { get; set; }
        }

        public class IndexersTarget
        {
            public string Log { get; private set; } = string.Empty;

            public string this[int index]
            {
                get => "int:" + index.ToString(CultureInfo.InvariantCulture);
                set => Log += "i" + index.ToString(CultureInfo.InvariantCulture) + "=" + value + ";";
            }

            public string this[string key]
            {
                get => "string:" + key;
                set => Log += "s" + key + "=" + value + ";";
            }
        }

        public abstract class VirtualValueBaseProxy
        {
            public virtual string Value => "base";
        }

        public abstract class StaticNewValueProxy : VirtualValueBaseProxy
        {
            public static new string Value => "static";
        }

        public abstract class StaticNewValueMidProxy : VirtualValueBaseProxy
        {
            public static new string Value => "static-mid";
        }

        public abstract class StaticNewValueLeafProxy : StaticNewValueMidProxy
        {
            public abstract string Name { get; }
        }

        public abstract class PrivateNewValueMidProxy : VirtualValueBaseProxy
        {
            private new string Value => "private-mid";
        }

        public abstract class PrivateNewValueLeafProxy : PrivateNewValueMidProxy
        {
            public abstract string Name { get; }
        }

        public abstract class GenericVirtualValueBaseProxy<T>
        {
            public virtual T Value => default!;
        }

        public abstract class NewValueOverGenericBaseProxy : GenericVirtualValueBaseProxy<string>
        {
            public new string Value => "new";
        }

        public class GenericNewVirtualToStringTarget<T>
        {
            public string Name => "generic";

            public new virtual string ToString() => "newvirtual";
        }

        public interface IToStringDeclaringProxy
        {
            string Name { get; }

            string ToString();
        }

        public interface IIgnoreCaseExplicitProxy
        {
            [Duck(ExplicitInterfaceTypeName = "*", BindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)]
            string Value { get; }
        }

        public class IgnoreCaseCandidatesTarget
        {
            // Declared first, so GetMember returns it first.
            public string VALUE => "upper";

            public string Value => "exact";
        }

        public interface IStringIndexerProxy
        {
            string this[string key] { get; }
        }

        public class OverloadedIndexerTarget
        {
            public string this[string key] => "string:" + key;

            public string this[object key] => "object:" + key;
        }

        public interface IIgnoreCaseNameProxy
        {
            [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
            string Name { get; }
        }

        public class CaseVariantNamesTarget
        {
            public string Name => "Name";

#pragma warning disable SA1300 // Element should begin with upper-case letter: the scenario needs names that only differ by case.
            public string name => "name";
#pragma warning restore SA1300
        }
    }
}

// Interfaces implemented explicitly by the member selection scenarios, in their own namespace so it can be used through an alias.
namespace Datadog.Trace.Tools.Runner.Tests.ParityContracts
{
    public interface IDescriptorMessage
    {
        string Descriptor { get; }
    }

    public interface IGenericPairEcho<T1, T2>
    {
        string Echo(T1 first, T2 second);
    }
}
