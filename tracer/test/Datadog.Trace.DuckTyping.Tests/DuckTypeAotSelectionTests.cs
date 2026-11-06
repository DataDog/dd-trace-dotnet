// <copyright file="DuckTypeAotSelectionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using FluentAssertions;
using Xunit;

#pragma warning disable SA1201 // Elements should appear in the correct order
#pragma warning disable SA1401 // Fields should be private

namespace Datadog.Trace.DuckTyping.Tests
{
    /// <summary>
    /// The members the AOT registry generator binds come from dynamic duck typing's own selection: each test checks that the
    /// member a selection returns is the one a dynamic proxy reads.
    /// </summary>
    public class DuckTypeAotSelectionTests
    {
        [Theory]
        [InlineData(nameof(IForwardProxy.Name), typeof(PropertyInfo), "property")]
        [InlineData(nameof(IForwardProxy.Secret), typeof(FieldInfo), "field")]
        [InlineData(nameof(IForwardProxy.SecretField), typeof(FieldInfo), "field")]
        [InlineData(nameof(IForwardProxy.Fallback), typeof(PropertyInfo), "property")]
        [InlineData(nameof(IForwardProxy.BaseSecret), typeof(FieldInfo), "base field")]
        public void ForwardPropertySelectionIsTheMemberTheProxyReads(string proxyPropertyName, Type memberType, string value)
        {
            var target = new ForwardTarget();
            var proxyProperty = typeof(IForwardProxy).GetProperty(proxyPropertyName)!;

            var selected = DuckType.SelectForwardTargetPropertyOrFieldForAot(typeof(IForwardProxy), typeof(ForwardTarget), proxyProperty.GetMethod!);

            selected.Should().BeAssignableTo(memberType);
            ValueOf(selected!, target).Should().Be(value);
            proxyProperty.GetValue(target.DuckCast<IForwardProxy>()).Should().Be(value);
        }

        [Fact]
        public void ForwardPropertySelectionIsNullWhenTheProxyCantBeCreated()
        {
            var proxyProperty = typeof(IMissingPropertyProxy).GetProperty(nameof(IMissingPropertyProxy.Secret))!;

            DuckType.SelectForwardTargetPropertyOrFieldForAot(typeof(IMissingPropertyProxy), typeof(ForwardTarget), proxyProperty.GetMethod!).Should().BeNull();
            new ForwardTarget().Invoking(target => target.DuckCast<IMissingPropertyProxy>()).Should().Throw<DuckTypePropertyOrFieldNotFoundException>();
        }

        [Theory]
        [InlineData(nameof(CopyProxy.Name), typeof(PropertyInfo), "property")]
        [InlineData(nameof(CopyProxy.Secret), typeof(FieldInfo), "field")]
        public void DuckCopySelectionIsTheMemberTheCopyReads(string proxyFieldName, Type memberType, string value)
        {
            var target = new ForwardTarget();
            var proxyField = typeof(CopyProxy).GetField(proxyFieldName)!;

            var selected = DuckType.SelectDuckCopyTargetMemberForAot(typeof(ForwardTarget), proxyField);

            selected.Should().BeAssignableTo(memberType);
            ValueOf(selected!, target).Should().Be(value);
            proxyField.GetValue(target.DuckCast<CopyProxy>()).Should().Be(value);
        }

        [Fact]
        public void ReverseSelectionsAreTheMembersTheProxyImplements()
        {
            var methods = DuckType.SelectReverseImplementationMethodsForAot(typeof(IReverseProxy), typeof(ReverseDelegation));
            var accessors = DuckType.SelectReverseImplementationAccessorsForAot(typeof(IReverseProxy), typeof(ReverseDelegation));

            // Only the [DuckReverseMethod] members, each with the member it implements.
            methods.Should().ContainSingle().Which.Should().Be(new KeyValuePair<MethodInfo, MethodInfo>(
                typeof(IReverseProxy).GetMethod(nameof(IReverseProxy.Get))!,
                typeof(ReverseDelegation).GetMethod(nameof(ReverseDelegation.Get))!));
            accessors.Should().ContainSingle().Which.Should().Be(new KeyValuePair<MethodInfo, MethodInfo>(
                typeof(IReverseProxy).GetProperty(nameof(IReverseProxy.Value))!.GetMethod!,
                typeof(ReverseDelegation).GetProperty(nameof(ReverseDelegation.Value))!.GetMethod!));

            var proxy = (IReverseProxy)new ReverseDelegation().DuckImplement(typeof(IReverseProxy));
            proxy.Get().Should().Be("method");
            proxy.Value.Should().Be("property");
        }

        private static object? ValueOf(MemberInfo member, object target)
            => member switch
            {
                PropertyInfo property => property.GetValue(target),
                FieldInfo field => field.GetValue(target),
                _ => throw new ArgumentException(member.ToString()),
            };

        public class ForwardTargetBase
        {
            private string _baseSecret = "base field";

            public override string ToString() => _baseSecret;
        }

        public class ForwardTarget : ForwardTargetBase
        {
            private string _secret = "field";

            public string Name => "property";

            public override string ToString() => _secret;
        }

        public interface IForwardProxy
        {
            // DuckKind.Property, the default
            string Name { get; }

            // DuckKind.PropertyOrField without a property: the field
            [Duck(Name = "_secret", Kind = DuckKind.PropertyOrField)]
            string Secret { get; }

            [Duck(Name = "_secret", Kind = DuckKind.Field)]
            string SecretField { get; }

            // The first name the target has
            [Duck(Name = "Missing,Name")]
            string Fallback { get; }

            [Duck(Name = "_baseSecret", Kind = DuckKind.Field, FallbackToBaseTypes = true)]
            string BaseSecret { get; }
        }

        public interface IMissingPropertyProxy
        {
            [Duck(Name = "_secret", Kind = DuckKind.Property)]
            string Secret { get; }
        }

        [DuckCopy]
        public struct CopyProxy
        {
            public string Name;

            [Duck(Name = "_secret", Kind = DuckKind.Field)]
            public string Secret;
        }

        public interface IReverseProxy
        {
            string Value { get; }

            string Get();
        }

        public class ReverseDelegation
        {
            [DuckReverseMethod]
            public string Value => "property";

            [DuckReverseMethod]
            public string Get() => "method";

            public string NotImplemented() => "not selected";
        }
    }
}
