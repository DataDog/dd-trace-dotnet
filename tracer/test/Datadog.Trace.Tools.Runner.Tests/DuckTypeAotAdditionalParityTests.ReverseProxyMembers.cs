// <copyright file="DuckTypeAotAdditionalParityTests.ReverseProxyMembers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Tools.Runner.DuckTypeAot;
using FluentAssertions;
using Xunit;

#pragma warning disable SA1201 // Nested test fixtures follow the test methods.

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// Forward proxies of the reverse proxy types the registry generates, and class proxies whose sealed overrides hide a base
/// virtual method: the members they bind (protected overrides, the instance field, [DuckInclude] methods), and the failures
/// dynamic duck typing has with them, with the exception they wrap.
/// </summary>
public partial class DuckTypeAotAdditionalParityTests
{
    [Theory]
    // Members of the reverse proxy type that override protected members of the contract.
    [InlineData("protected-abstract-property")]
    [InlineData("protected-abstract-method")]
    [InlineData("protected-virtual-method")]
    [InlineData("duck-copy-protected-property")]
    [InlineData("class-proxy-protected-property")]
    // A reverse property implemented by a delegation property of another name.
    [InlineData("duck-copy-renamed-reverse-property")]
    [InlineData("duck-copy-renamed-reverse-property-struct-reverse")]
    // The private instance field of the reverse proxy type.
    [InlineData("current-instance-field")]
    [InlineData("current-instance-field-struct-reverse")]
    [InlineData("duck-copy-current-instance-field")]
    // Two get_Instance methods (the contract's and IDuckType's): the ambiguity dynamic duck typing wraps.
    [InlineData("duplicate-get-instance")]
    [InlineData("duplicate-get-instance-class-contract")]
    // [DuckInclude] methods of the contract the reverse proxy type overrides (sealed), or inherits.
    [InlineData("duck-include-overridden-by-reverse-proxy")]
    [InlineData("duck-include-not-implemented")]
    [InlineData("duck-include-inherited")]
    [InlineData("duck-include-inherited-shared-shape")]
    [InlineData("duck-include-shared-shape-other-contract")]
    // A sealed override of the class proxy hides the base virtual method.
    [InlineData("sealed-override-target-without-method")]
    [InlineData("sealed-override-target-with-method")]
    public void GeneratedRegistryShouldBindForwardProxiesOfReverseProxiesLikeDynamicMode(string scenario)
    {
        var (exercise, mappings) = scenario switch
        {
            "protected-abstract-property" => (
                Reverse<RevProtContract>(new RevProtDelegation(), r => DuckType.Create<IRevSecretView>(r)!.Secret + "|" + r.ReadSecret()),
                new[] { Mapping(typeof(RevProtContract), typeof(RevProtDelegation), reverse: true), Mapping(typeof(IRevSecretView), typeof(RevProtContract)) }),
            "protected-abstract-method" => (
                Reverse<RevProtContract>(new RevProtDelegation(), r => DuckType.Create<IRevComputeView>(r)!.Compute("x")),
                new[] { Mapping(typeof(RevProtContract), typeof(RevProtDelegation), reverse: true), Mapping(typeof(IRevComputeView), typeof(RevProtContract)) }),
            "protected-virtual-method" => (
                Reverse<RevProtVirtualContract>(new RevComputeDelegation(), r => DuckType.Create<IRevComputeView>(r)!.Compute("x")),
                new[] { Mapping(typeof(RevProtVirtualContract), typeof(RevComputeDelegation), reverse: true), Mapping(typeof(IRevComputeView), typeof(RevProtVirtualContract)) }),
            "duck-copy-protected-property" => (
                Reverse<RevProtContract>(new RevProtDelegation(), r => DuckType.Create<RevSecretCopy>(r).Secret),
                new[] { Mapping(typeof(RevProtContract), typeof(RevProtDelegation), reverse: true), Mapping(typeof(RevSecretCopy), typeof(RevProtContract)) }),
            "class-proxy-protected-property" => (
                Reverse<RevProtContract>(new RevProtDelegation(), r => DuckType.Create<RevSecretClassView>(r)!.Secret),
                new[] { Mapping(typeof(RevProtContract), typeof(RevProtDelegation), reverse: true), Mapping(typeof(RevSecretClassView), typeof(RevProtContract)) }),
            "duck-copy-renamed-reverse-property" => (
                Reverse<RevRenamedContract>(new RevRenamedDelegation(), r => DuckType.Create<RevMyValueCopy>(r).MyValue),
                new[] { Mapping(typeof(RevRenamedContract), typeof(RevRenamedDelegation), reverse: true), Mapping(typeof(RevMyValueCopy), typeof(RevRenamedContract)) }),
            "duck-copy-renamed-reverse-property-struct-reverse" => (
                Reverse<IRevRenamedContract>(new RevRenamedDelegation(), r => DuckType.Create<RevMyValueCopy>(r).MyValue),
                new[] { Mapping(typeof(IRevRenamedContract), typeof(RevRenamedDelegation), reverse: true), Mapping(typeof(RevMyValueCopy), typeof(IRevRenamedContract)) }),
            "current-instance-field" => (
                Reverse<RevRenamedContract>(new RevRenamedDelegation(), r => DuckType.Create<IRevCurrentInstanceView>(r)!.Inner.GetType().Name),
                new[] { Mapping(typeof(RevRenamedContract), typeof(RevRenamedDelegation), reverse: true), Mapping(typeof(IRevCurrentInstanceView), typeof(RevRenamedContract)) }),
            "current-instance-field-struct-reverse" => (
                Reverse<IRevRenamedContract>(new RevRenamedDelegation(), r => DuckType.Create<IRevCurrentInstanceView>(r)!.Inner.GetType().Name),
                new[] { Mapping(typeof(IRevRenamedContract), typeof(RevRenamedDelegation), reverse: true), Mapping(typeof(IRevCurrentInstanceView), typeof(IRevRenamedContract)) }),
            "duck-copy-current-instance-field" => (
                Reverse<RevRenamedContract>(new RevRenamedDelegation(), r => DuckType.Create<RevCurrentInstanceCopy>(r).Inner.GetType().Name),
                new[] { Mapping(typeof(RevRenamedContract), typeof(RevRenamedDelegation), reverse: true), Mapping(typeof(RevCurrentInstanceCopy), typeof(RevRenamedContract)) }),
            "duplicate-get-instance" => (
                Reverse<IRevInstanceContract>(new RevInstanceDelegation(), r => DuckType.Create<IRevGetterView>(r)!.GetIt()),
                new[] { Mapping(typeof(IRevInstanceContract), typeof(RevInstanceDelegation), reverse: true), Mapping(typeof(IRevGetterView), typeof(IRevInstanceContract)) }),
            "duplicate-get-instance-class-contract" => (
                Reverse<RevInstanceContract>(new RevInstanceDelegation(), r => DuckType.Create<IRevGetterView>(r)!.GetIt()),
                new[] { Mapping(typeof(RevInstanceContract), typeof(RevInstanceDelegation), reverse: true), Mapping(typeof(IRevGetterView), typeof(RevInstanceContract)) }),
            "duck-include-overridden-by-reverse-proxy" => (
                Reverse<RevIncludeContract>(new RevIncludeDelegation(), r => DescribeSealedProxy(DuckType.Create<RevSealedDescribeProxy>(r)!)),
                new[] { Mapping(typeof(RevIncludeContract), typeof(RevIncludeDelegation), reverse: true), Mapping(typeof(RevSealedDescribeProxy), typeof(RevIncludeContract)) }),
            "duck-include-not-implemented" => (
                Reverse<RevIncludeContract>(new RevIncludeDelegation(), r =>
                {
                    var proxy = DuckType.Create<IRevNameView>(r)!;
                    return proxy.Name + "|" + (proxy.GetType().GetMethod("Describe", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly) is not null);
                }),
                new[] { Mapping(typeof(RevIncludeContract), typeof(RevIncludeDelegation), reverse: true), Mapping(typeof(IRevNameView), typeof(RevIncludeContract)) }),
            "duck-include-inherited" => (
                Reverse<RevShapeBContract>(new RevNameDelegation(), r => DescribeSealedProxy(DuckType.Create<RevSealedDescribeProxy>(r)!)),
                new[] { Mapping(typeof(RevShapeBContract), typeof(RevNameDelegation), reverse: true), Mapping(typeof(RevSealedDescribeProxy), typeof(RevShapeBContract)) }),
            "duck-include-inherited-shared-shape" => (
                Reverse<RevShapeBContract>(new RevNameDelegation(), r => DescribeSealedProxy(DuckType.Create<RevSealedDescribeProxy>(r)!)),
                SharedShapeMappings()),
            "duck-include-shared-shape-other-contract" => (
                Reverse<RevShapeAContract>(new RevNameDelegation(), r => DescribeSealedProxy(DuckType.Create<RevSealedDescribeProxy>(r)!)),
                SharedShapeMappings()),
            "sealed-override-target-without-method" => (
                () => DescribeSealedProxy(DuckType.Create<RevSealedDescribeProxy>(new RevNameTarget())!),
                new[] { Mapping(typeof(RevSealedDescribeProxy), typeof(RevNameTarget)) }),
            _ => (
                () => DescribeSealedProxy(DuckType.Create<RevSealedDescribeProxy>(new RevNameDescribeTarget())!),
                new[] { Mapping(typeof(RevSealedDescribeProxy), typeof(RevNameDescribeTarget)) }),
        };

        DuckType.ResetRuntimeModeForTests();
        var expected = CaptureWithInnerException(exercise);
        WithGeneratedRegistry(() => CaptureWithInnerException(exercise).Should().Be(expected, "AOT duck typing should behave like dynamic duck typing"), mappings);

        static string DescribeSealedProxy(RevSealedDescribeProxy proxy) => proxy.Name + "|" + proxy.Describe() + "|" + ((RevDescribeBase)proxy).Describe();

        static DuckTypeAotMapping[] SharedShapeMappings()
            => new[]
            {
                Mapping(typeof(RevShapeAContract), typeof(RevNameDelegation), reverse: true),
                Mapping(typeof(RevShapeBContract), typeof(RevNameDelegation), reverse: true),
                Mapping(typeof(RevSealedDescribeProxy), typeof(RevShapeAContract)),
                Mapping(typeof(RevSealedDescribeProxy), typeof(RevShapeBContract)),
            };
    }

    private static Func<object?> Reverse<TContract>(object delegation, Func<TContract, object?> exercise)
        where TContract : class
        => () => exercise((TContract)DuckType.CreateReverse(typeof(TContract), delegation));

    /// <summary>
    /// Captures the outcome of a scenario like <see cref="CaptureFailure"/>, with the type and message of the exception the
    /// DuckTypeException wraps, and the names of the generated types (dynamic or AOT) replaced.
    /// </summary>
    private static string CaptureWithInnerException(Func<object?> exercise)
    {
        string outcome;
        try
        {
            outcome = Convert.ToString(exercise(), CultureInfo.InvariantCulture) ?? "null";
        }
        catch (Exception ex)
        {
            var exceptionTypes = new List<string>();
            Exception? current = ex;
            while (current is not null && current is not DuckTypeException)
            {
                exceptionTypes.Add(current.GetType().Name);
                current = current.InnerException;
            }

            outcome = "throws:" + string.Join(">", exceptionTypes) +
                      (current is null ? string.Empty : $">{current.GetType().Name}:{current.Message}") +
                      (current?.InnerException is { } inner ? $" [inner {inner.GetType().Name}: {inner.Message}]" : string.Empty);
        }

        // Generated type names: the registry's, and the ones of dynamic duck typing (types, modules and assemblies).
        outcome = Regex.Replace(outcome, @"Datadog\.Trace\.DuckTyping\.Generated\.Proxies\.DuckTypeProxy_\d+_[0-9a-f]+", "<generated>");
        outcome = Regex.Replace(outcome, @"Datadog_[A-Za-z0-9_.]*__[0-9A-F]+[^' ]*", "<generated>");
        return Regex.Replace(outcome, @"Datadog\.DuckTypeAssembly[^']*", "<generated>");
    }

    public abstract class RevProtContract
    {
        protected abstract string Secret { get; }

        public string ReadSecret() => Secret;

        protected abstract string Compute(string value);
    }

    public abstract class RevProtVirtualContract
    {
        protected virtual string Compute(string value) => "base:" + value;
    }

    public class RevProtDelegation
    {
        [DuckReverseMethod]
        public string Secret => "secret";

        [DuckReverseMethod]
        public string Compute(string value) => "computed:" + value;
    }

    public class RevComputeDelegation
    {
        [DuckReverseMethod]
        public string Compute(string value) => "computed:" + value;
    }

    public interface IRevSecretView
    {
        string Secret { get; }
    }

    public interface IRevComputeView
    {
        string Compute(string value);
    }

    [DuckCopy]
    public struct RevSecretCopy
    {
        public string Secret;
    }

    public abstract class RevSecretClassView
    {
        public abstract string Secret { get; }
    }

    public abstract class RevRenamedContract
    {
        public abstract string Value { get; }
    }

    public interface IRevRenamedContract
    {
        string Value { get; }
    }

    public class RevRenamedDelegation
    {
        [DuckReverseMethod(Name = "Value")]
        public string MyValue => "my-value";
    }

    [DuckCopy]
    public struct RevMyValueCopy
    {
        public string MyValue;
    }

    public interface IRevCurrentInstanceView
    {
        [DuckField(Name = "_currentInstance")]
        object Inner { get; }
    }

    [DuckCopy]
    public struct RevCurrentInstanceCopy
    {
        [DuckField(Name = "_currentInstance")]
        public object Inner;
    }

    public interface IRevInstanceContract
    {
        object Instance { get; }
    }

    public abstract class RevInstanceContract
    {
        public abstract object Instance { get; }
    }

    public class RevInstanceDelegation
    {
        [DuckReverseMethod]
        public object Instance => "delegation-instance";
    }

    public interface IRevGetterView
    {
        [Duck(Name = "get_Instance")]
        object GetIt();
    }

    public interface IRevNameView
    {
        string Name { get; }
    }

    public abstract class RevIncludeContract
    {
        public abstract string Name { get; }

        [DuckInclude]
        public virtual string Describe() => "contract-describe";
    }

    public class RevIncludeDelegation
    {
        [DuckReverseMethod]
        public string Name => "name";

        [DuckReverseMethod]
        public string Describe() => "delegation-describe";
    }

    public abstract class RevShapeAContract
    {
        public abstract string Name { get; }
    }

    public abstract class RevShapeBContract
    {
        public abstract string Name { get; }

        [DuckInclude]
        public virtual string Describe() => "shape-b-describe";
    }

    public class RevNameDelegation
    {
        [DuckReverseMethod]
        public string Name => "name";
    }

    public abstract class RevDescribeBase
    {
        public virtual string Describe() => "base-describe";
    }

    public abstract class RevSealedDescribeProxy : RevDescribeBase
    {
        public abstract string Name { get; }

        public sealed override string Describe() => "sealed-describe";
    }

    public class RevNameTarget
    {
        public string Name => "target-name";
    }

    public class RevNameDescribeTarget
    {
        public string Name => "target-name";

        public string Describe() => "target-describe";
    }
}
