// <copyright file="DuckTypeAotDifferentialParityTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Reflection;
using FluentAssertions;
using Xunit;

#pragma warning disable SA1201 // Elements should appear in the correct order
#pragma warning disable SA1401 // Fields should be private

namespace Datadog.Trace.DuckTyping.Tests
{
    [Collection(nameof(GetAssemblyTestsCollection))]
    public class DuckTypeAotDifferentialParityTests
    {
        private static readonly MethodInfo? DynamicForwardFactory = typeof(DuckType).GetMethod("GetOrCreateDynamicProxyType", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly MethodInfo? DynamicReverseFactory = typeof(DuckType).GetMethod("GetOrCreateDynamicReverseProxyType", BindingFlags.NonPublic | BindingFlags.Static);

        public DuckTypeAotDifferentialParityTests()
        {
            DuckTypeAotEngine.ResetForTests();
        }

        [Fact]
        public void DifferentialParityA01ForwardPropertyShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "A-01";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IForwardValueProxy),
                typeof(ForwardValueTarget),
                typeof(ForwardValueAotProxy),
                instance => new ForwardValueAotProxy((ForwardValueTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IForwardValueProxy), typeof(ForwardValueTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IForwardValueProxy), typeof(ForwardValueTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var target = new ForwardValueTarget("alpha");
            var dynamicProxy = dynamicResult.CreateInstance<IForwardValueProxy>(target);
            var aotProxy = aotResult.CreateInstance<IForwardValueProxy>(target);

            aotProxy.Value.Should().Be(dynamicProxy.Value, $"scenario {scenarioId} should produce equivalent values");
        }

        [Fact]
        public void DifferentialParityA02ForwardMethodShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "A-02";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IForwardMathProxy),
                typeof(ForwardMathTarget),
                typeof(ForwardMathAotProxy),
                instance => new ForwardMathAotProxy((ForwardMathTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IForwardMathProxy), typeof(ForwardMathTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IForwardMathProxy), typeof(ForwardMathTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var target = new ForwardMathTarget();
            var dynamicProxy = dynamicResult.CreateInstance<IForwardMathProxy>(target);
            var aotProxy = aotResult.CreateInstance<IForwardMathProxy>(target);

            aotProxy.Add(3, 9).Should().Be(dynamicProxy.Add(3, 9), $"scenario {scenarioId} should produce equivalent method results");
            aotProxy.Add(-4, 2).Should().Be(dynamicProxy.Add(-4, 2), $"scenario {scenarioId} should preserve behavior across different inputs");
        }

        [Fact]
        public void DifferentialParityA03ForwardPropertySetShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "A-03";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IForwardMutableValueProxy),
                typeof(ForwardMutableValueTarget),
                typeof(ForwardMutableValueAotProxy),
                instance => new ForwardMutableValueAotProxy((ForwardMutableValueTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IForwardMutableValueProxy), typeof(ForwardMutableValueTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IForwardMutableValueProxy), typeof(ForwardMutableValueTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicTarget = new ForwardMutableValueTarget();
            var aotTarget = new ForwardMutableValueTarget();

            var dynamicProxy = dynamicResult.CreateInstance<IForwardMutableValueProxy>(dynamicTarget);
            var aotProxy = aotResult.CreateInstance<IForwardMutableValueProxy>(aotTarget);

            dynamicProxy.Value = "delta";
            aotProxy.Value = "delta";

            aotProxy.Value.Should().Be(dynamicProxy.Value, $"scenario {scenarioId} should preserve proxy setter/getter behavior");
            aotTarget.Value.Should().Be(dynamicTarget.Value, $"scenario {scenarioId} should apply the same target mutation");
        }

        [Fact]
        public void DifferentialParityA04ForwardRefOutMethodShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "A-04";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IRefOutMathProxy),
                typeof(RefOutMathTarget),
                typeof(RefOutMathAotProxy),
                instance => new RefOutMathAotProxy((RefOutMathTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IRefOutMathProxy), typeof(RefOutMathTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IRefOutMathProxy), typeof(RefOutMathTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IRefOutMathProxy>(new RefOutMathTarget());
            var aotProxy = aotResult.CreateInstance<IRefOutMathProxy>(new RefOutMathTarget());

            var dynamicValue = -7;
            var aotValue = -7;

            dynamicProxy.Normalize(ref dynamicValue, out var dynamicDoubled);
            aotProxy.Normalize(ref aotValue, out var aotDoubled);

            aotValue.Should().Be(dynamicValue, $"scenario {scenarioId} should preserve ref parameter behavior");
            aotDoubled.Should().Be(dynamicDoubled, $"scenario {scenarioId} should preserve out parameter behavior");
        }

        [Fact]
        public void DifferentialParityA05ForwardAliasedMethodShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "A-05";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IAliasMethodProxy),
                typeof(AliasMethodTarget),
                typeof(AliasMethodAotProxy),
                instance => new AliasMethodAotProxy((AliasMethodTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IAliasMethodProxy), typeof(AliasMethodTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IAliasMethodProxy), typeof(AliasMethodTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IAliasMethodProxy>(new AliasMethodTarget());
            var aotProxy = aotResult.CreateInstance<IAliasMethodProxy>(new AliasMethodTarget());

            aotProxy.Add(4, 9).Should().Be(dynamicProxy.Add(4, 9), $"scenario {scenarioId} should preserve aliased method behavior");
        }

        [Fact]
        public void DifferentialParityA06ForwardNullableReturnShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "A-06";
            DuckTypeAotEngine.RegisterProxy(
                typeof(INullableValueProxy),
                typeof(NullableValueTarget),
                typeof(NullableValueAotProxy),
                instance => new NullableValueAotProxy((NullableValueTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(INullableValueProxy), typeof(NullableValueTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(INullableValueProxy), typeof(NullableValueTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<INullableValueProxy>(new NullableValueTarget());
            var aotProxy = aotResult.CreateInstance<INullableValueProxy>(new NullableValueTarget());

            aotProxy.Maybe(8).Should().Be(dynamicProxy.Maybe(8), $"scenario {scenarioId} should preserve nullable non-null return behavior");
            aotProxy.Maybe(-1).Should().Be(dynamicProxy.Maybe(-1), $"scenario {scenarioId} should preserve nullable null return behavior");
        }

        [Fact]
        public void DifferentialParityA07ForwardGenericMethodShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "A-07";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IGenericEchoProxy),
                typeof(GenericEchoTarget),
                typeof(GenericEchoAotProxy),
                instance => new GenericEchoAotProxy((GenericEchoTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IGenericEchoProxy), typeof(GenericEchoTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IGenericEchoProxy), typeof(GenericEchoTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IGenericEchoProxy>(new GenericEchoTarget());
            var aotProxy = aotResult.CreateInstance<IGenericEchoProxy>(new GenericEchoTarget());

            aotProxy.Echo("omega").Should().Be(dynamicProxy.Echo("omega"), $"scenario {scenarioId} should preserve generic method behavior for reference types");
            aotProxy.Echo(42).Should().Be(dynamicProxy.Echo(42), $"scenario {scenarioId} should preserve generic method behavior for value types");
        }

        [Fact]
        public void DifferentialParityA08ForwardStaticMethodShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "A-08";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IStaticMathProxy),
                typeof(StaticMathTarget),
                typeof(StaticMathAotProxy),
                instance => new StaticMathAotProxy((StaticMathTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IStaticMathProxy), typeof(StaticMathTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IStaticMathProxy), typeof(StaticMathTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IStaticMathProxy>(new StaticMathTarget());
            var aotProxy = aotResult.CreateInstance<IStaticMathProxy>(new StaticMathTarget());

            aotProxy.Triple(5).Should().Be(dynamicProxy.Triple(5), $"scenario {scenarioId} should preserve static method behavior");
        }

        [Fact]
        public void DifferentialParityA09ForwardOverloadDisambiguationShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "A-09";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IOverloadIntProxy),
                typeof(OverloadTarget),
                typeof(OverloadIntAotProxy),
                instance => new OverloadIntAotProxy((OverloadTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IOverloadIntProxy), typeof(OverloadTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IOverloadIntProxy), typeof(OverloadTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IOverloadIntProxy>(new OverloadTarget());
            var aotProxy = aotResult.CreateInstance<IOverloadIntProxy>(new OverloadTarget());

            aotProxy.ComputeInt(10).Should().Be(dynamicProxy.ComputeInt(10), $"scenario {scenarioId} should preserve overload disambiguation behavior");
        }

        [Fact]
        public void DifferentialParityA10ForwardMemberVisibilityShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "A-10";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IVisibilityAccessProxy),
                typeof(VisibilityAccessTarget),
                typeof(VisibilityAccessAotProxy),
                instance => new VisibilityAccessAotProxy((VisibilityAccessTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IVisibilityAccessProxy), typeof(VisibilityAccessTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IVisibilityAccessProxy), typeof(VisibilityAccessTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicTarget = new VisibilityAccessTarget();
            var aotTarget = new VisibilityAccessTarget();

            var dynamicProxy = dynamicResult.CreateInstance<IVisibilityAccessProxy>(dynamicTarget);
            var aotProxy = aotResult.CreateInstance<IVisibilityAccessProxy>(aotTarget);

            dynamicProxy.PublicCount = 3;
            dynamicProxy.InternalCount = 5;
            dynamicProxy.PrivateCount = 7;

            aotProxy.PublicCount = 3;
            aotProxy.InternalCount = 5;
            aotProxy.PrivateCount = 7;

            aotProxy.PublicCount.Should().Be(dynamicProxy.PublicCount, $"scenario {scenarioId} should preserve public member behavior");
            aotProxy.InternalCount.Should().Be(dynamicProxy.InternalCount, $"scenario {scenarioId} should preserve internal member behavior");
            aotProxy.PrivateCount.Should().Be(dynamicProxy.PrivateCount, $"scenario {scenarioId} should preserve private member behavior");
            aotTarget.ReadPrivate().Should().Be(dynamicTarget.ReadPrivate(), $"scenario {scenarioId} should apply equivalent private-field mutation");
        }

        [Fact]
        public void DifferentialParityA11ForwardDuckIgnoreShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "A-11";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IgnoreFeatureProxyBase),
                typeof(IgnoreFeatureTarget),
                typeof(IgnoreFeatureAotProxy),
                instance => new IgnoreFeatureAotProxy((IgnoreFeatureTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IgnoreFeatureProxyBase), typeof(IgnoreFeatureTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IgnoreFeatureProxyBase), typeof(IgnoreFeatureTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IgnoreFeatureProxyBase>(new IgnoreFeatureTarget());
            var aotProxy = aotResult.CreateInstance<IgnoreFeatureProxyBase>(new IgnoreFeatureTarget());

            aotProxy.LocalValue().Should().Be(dynamicProxy.LocalValue(), $"scenario {scenarioId} should preserve [DuckIgnore] behavior");
        }

        [Fact]
        public void DifferentialParityA12ForwardDuckIncludeShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "A-12";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IEmptyIncludeProxy),
                typeof(IncludeHashTarget),
                typeof(IncludeHashAotProxy),
                instance => new IncludeHashAotProxy((IncludeHashTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IEmptyIncludeProxy), typeof(IncludeHashTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IEmptyIncludeProxy), typeof(IncludeHashTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var target = new IncludeHashTarget();
            var dynamicProxy = dynamicResult.CreateInstance<IEmptyIncludeProxy>(target);
            var aotProxy = aotResult.CreateInstance<IEmptyIncludeProxy>(target);

            aotProxy.GetHashCode().Should().Be(dynamicProxy.GetHashCode(), $"scenario {scenarioId} should preserve [DuckInclude] object-method forwarding");
            aotProxy.GetHashCode().Should().Be(target.GetHashCode(), $"scenario {scenarioId} should forward GetHashCode to target");
        }

        [Fact]
        public void DifferentialParityA13ForwardObjectMethodSkipShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "A-13";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IEmptyNoIncludeProxy),
                typeof(NoIncludeHashTarget),
                typeof(NoIncludeHashAotProxy),
                instance => new NoIncludeHashAotProxy((NoIncludeHashTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IEmptyNoIncludeProxy), typeof(NoIncludeHashTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IEmptyNoIncludeProxy), typeof(NoIncludeHashTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var target = new NoIncludeHashTarget();
            var dynamicProxy = dynamicResult.CreateInstance<IEmptyNoIncludeProxy>(target);
            var aotProxy = aotResult.CreateInstance<IEmptyNoIncludeProxy>(target);

            dynamicProxy.GetHashCode().Should().NotBe(target.GetHashCode(), $"scenario {scenarioId} dynamic mode should skip object-level methods without [DuckInclude]");
            aotProxy.GetHashCode().Should().NotBe(target.GetHashCode(), $"scenario {scenarioId} AOT mode should skip object-level methods without [DuckInclude]");
        }

        [Fact]
        public void DifferentialParityA14ForwardToStringShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "A-14";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IEmptyToStringProxy),
                typeof(ToStringTarget),
                typeof(ToStringAotProxy),
                instance => new ToStringAotProxy((ToStringTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IEmptyToStringProxy), typeof(ToStringTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IEmptyToStringProxy), typeof(ToStringTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var target = new ToStringTarget();
            var dynamicProxy = dynamicResult.CreateInstance<IEmptyToStringProxy>(target);
            var aotProxy = aotResult.CreateInstance<IEmptyToStringProxy>(target);

            aotProxy.ToString().Should().Be(dynamicProxy.ToString(), $"scenario {scenarioId} should preserve ToString forwarding behavior");
            aotProxy.ToString().Should().Be(target.ToString(), $"scenario {scenarioId} should forward ToString to target");
        }

        [Fact]
        public void DifferentialParityA15ForwardStructMutationGuardShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "A-15";
            var dynamicResult = InvokeDynamicForward(typeof(IStructMutationGuardProxy), typeof(StructMutationGuardTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IStructMutationGuardProxy), typeof(StructMutationGuardTarget));

            AssertCannotCreate(scenarioId, dynamicResult, aotResult);
        }

        [Fact]
        public void DifferentialParityB16ForwardFieldBackedPropertyShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "B-16";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IFieldCountProxy),
                typeof(FieldCountTarget),
                typeof(FieldCountAotProxy),
                instance => new FieldCountAotProxy((FieldCountTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IFieldCountProxy), typeof(FieldCountTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IFieldCountProxy), typeof(FieldCountTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicTarget = new FieldCountTarget();
            var aotTarget = new FieldCountTarget();

            var dynamicProxy = dynamicResult.CreateInstance<IFieldCountProxy>(dynamicTarget);
            var aotProxy = aotResult.CreateInstance<IFieldCountProxy>(aotTarget);

            dynamicProxy.Count = 11;
            aotProxy.Count = 11;

            aotProxy.Count.Should().Be(dynamicProxy.Count, $"scenario {scenarioId} should preserve field-backed value behavior");
            aotTarget.Count.Should().Be(dynamicTarget.Count, $"scenario {scenarioId} should mutate field-backed targets equivalently");
        }

        [Fact]
        public void DifferentialParityB17ForwardDuckFieldAttributeShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "B-17";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IDuckFieldCountProxy),
                typeof(DuckFieldCountTarget),
                typeof(DuckFieldCountAotProxy),
                instance => new DuckFieldCountAotProxy((DuckFieldCountTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IDuckFieldCountProxy), typeof(DuckFieldCountTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IDuckFieldCountProxy), typeof(DuckFieldCountTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IDuckFieldCountProxy>(new DuckFieldCountTarget());
            var aotProxy = aotResult.CreateInstance<IDuckFieldCountProxy>(new DuckFieldCountTarget());

            dynamicProxy.Count = 33;
            aotProxy.Count = 33;

            aotProxy.Count.Should().Be(dynamicProxy.Count, $"scenario {scenarioId} should preserve [DuckField] binding behavior");
        }

        [Fact]
        public void DifferentialParityB18ForwardPrivateFieldShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "B-18";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IPrivateFieldProxy),
                typeof(PrivateFieldTarget),
                typeof(PrivateFieldAotProxy),
                instance => new PrivateFieldAotProxy((PrivateFieldTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IPrivateFieldProxy), typeof(PrivateFieldTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IPrivateFieldProxy), typeof(PrivateFieldTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IPrivateFieldProxy>(new PrivateFieldTarget());
            var aotProxy = aotResult.CreateInstance<IPrivateFieldProxy>(new PrivateFieldTarget());

            dynamicProxy.Count = 17;
            aotProxy.Count = 17;

            aotProxy.Count.Should().Be(dynamicProxy.Count, $"scenario {scenarioId} should preserve private field binding behavior");
        }

        [Fact]
        public void DifferentialParityB19ForwardOptionalParametersShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "B-19";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IOptionalParameterProxy),
                typeof(OptionalParameterTarget),
                typeof(OptionalParameterAotProxy),
                instance => new OptionalParameterAotProxy((OptionalParameterTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IOptionalParameterProxy), typeof(OptionalParameterTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IOptionalParameterProxy), typeof(OptionalParameterTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IOptionalParameterProxy>(new OptionalParameterTarget());
            var aotProxy = aotResult.CreateInstance<IOptionalParameterProxy>(new OptionalParameterTarget());

            aotProxy.AddWithDefault(4).Should().Be(dynamicProxy.AddWithDefault(4), $"scenario {scenarioId} should preserve optional-argument behavior");
        }

        [Fact]
        public void DifferentialParityB20ForwardRefOutConversionShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "B-20";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IRefOutConversionProxy),
                typeof(RefOutConversionTarget),
                typeof(RefOutConversionAotProxy),
                instance => new RefOutConversionAotProxy((RefOutConversionTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IRefOutConversionProxy), typeof(RefOutConversionTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IRefOutConversionProxy), typeof(RefOutConversionTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IRefOutConversionProxy>(new RefOutConversionTarget());
            var aotProxy = aotResult.CreateInstance<IRefOutConversionProxy>(new RefOutConversionTarget());

            var dynamicValue = -11;
            var aotValue = -11;
            dynamicProxy.Normalize(ref dynamicValue, out var dynamicDoubled);
            aotProxy.Normalize(ref aotValue, out var aotDoubled);

            aotValue.Should().Be(dynamicValue, $"scenario {scenarioId} should preserve ref behavior");
            aotDoubled.Should().Be(dynamicDoubled, $"scenario {scenarioId} should preserve out behavior");
        }

        [Fact]
        public void DifferentialParityB21ForwardParameterTypeNamesShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "B-21";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IParameterTypeNamesProxy),
                typeof(ParameterTypeNamesTarget),
                typeof(ParameterTypeNamesAotProxy),
                instance => new ParameterTypeNamesAotProxy((ParameterTypeNamesTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IParameterTypeNamesProxy), typeof(ParameterTypeNamesTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IParameterTypeNamesProxy), typeof(ParameterTypeNamesTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IParameterTypeNamesProxy>(new ParameterTypeNamesTarget());
            var aotProxy = aotResult.CreateInstance<IParameterTypeNamesProxy>(new ParameterTypeNamesTarget());

            aotProxy.ComputeLong(15).Should().Be(dynamicProxy.ComputeLong(15), $"scenario {scenarioId} should preserve ParameterTypeNames disambiguation behavior");
        }

        [Fact]
        public void DifferentialParityB22ForwardGenericPublicMethodShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "B-22";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IGenericWrapProxy),
                typeof(GenericWrapTarget),
                typeof(GenericWrapAotProxy),
                instance => new GenericWrapAotProxy((GenericWrapTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IGenericWrapProxy), typeof(GenericWrapTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IGenericWrapProxy), typeof(GenericWrapTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IGenericWrapProxy>(new GenericWrapTarget());
            var aotProxy = aotResult.CreateInstance<IGenericWrapProxy>(new GenericWrapTarget());

            var dynamicTuple = dynamicProxy.Wrap(7, "seven");
            var aotTuple = aotProxy.Wrap(7, "seven");
            aotTuple.Item1.Should().Be(dynamicTuple.Item1, $"scenario {scenarioId} should preserve generic method first value");
            aotTuple.Item2.Should().Be(dynamicTuple.Item2, $"scenario {scenarioId} should preserve generic method second value");
        }

        [Fact]
        public void DifferentialParityB23ForwardNonPublicGenericWithExplicitTypeNamesShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "B-23";
            DuckTypeAotEngine.RegisterProxy(
                typeof(INonPublicGenericBindingProxy),
                typeof(NonPublicGenericBindingTarget),
                typeof(NonPublicGenericBindingAotProxy),
                instance => new NonPublicGenericBindingAotProxy((NonPublicGenericBindingTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(INonPublicGenericBindingProxy), typeof(NonPublicGenericBindingTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(INonPublicGenericBindingProxy), typeof(NonPublicGenericBindingTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<INonPublicGenericBindingProxy>(new NonPublicGenericBindingTarget());
            var aotProxy = aotResult.CreateInstance<INonPublicGenericBindingProxy>(new NonPublicGenericBindingTarget());

            aotProxy.GetDefaultInt().Should().Be(dynamicProxy.GetDefaultInt(), $"scenario {scenarioId} should preserve explicit generic type-name binding for int");
            aotProxy.GetDefaultString().Should().Be(dynamicProxy.GetDefaultString(), $"scenario {scenarioId} should preserve explicit generic type-name binding for string");
        }

        [Fact]
        public void DifferentialParityB24ForwardGenericMethodInNonPublicTargetShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "B-24";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IGenericNonPublicMethodProxy),
                typeof(GenericNonPublicMethodTarget),
                typeof(GenericNonPublicMethodAotProxy),
                instance => new GenericNonPublicMethodAotProxy((GenericNonPublicMethodTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IGenericNonPublicMethodProxy), typeof(GenericNonPublicMethodTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IGenericNonPublicMethodProxy), typeof(GenericNonPublicMethodTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IGenericNonPublicMethodProxy>(new GenericNonPublicMethodTarget());
            var aotProxy = aotResult.CreateInstance<IGenericNonPublicMethodProxy>(new GenericNonPublicMethodTarget());

            aotProxy.GetDefault<int>().Should().Be(dynamicProxy.GetDefault<int>(), $"scenario {scenarioId} should preserve non-public generic method binding for int");
            aotProxy.GetDefault<string>().Should().Be(dynamicProxy.GetDefault<string>(), $"scenario {scenarioId} should preserve non-public generic method binding for string");
        }

        [Fact]
        public void DifferentialParityB25ForwardExplicitInterfaceBindingShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "B-25";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IExplicitMathProxy),
                typeof(ExplicitMathTarget),
                typeof(ExplicitMathAotProxy),
                instance => new ExplicitMathAotProxy((ExplicitMathTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IExplicitMathProxy), typeof(ExplicitMathTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IExplicitMathProxy), typeof(ExplicitMathTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IExplicitMathProxy>(new ExplicitMathTarget());
            var aotProxy = aotResult.CreateInstance<IExplicitMathProxy>(new ExplicitMathTarget());

            aotProxy.Compute(9).Should().Be(dynamicProxy.Compute(9), $"scenario {scenarioId} should preserve explicit-interface method binding behavior");
        }

        [Fact]
        public void DifferentialParityB26ForwardAmbiguousMethodShouldFailInBothModes()
        {
            const string scenarioId = "B-26";
            var dynamicResult = InvokeDynamicForward(typeof(IAmbiguousMethodProxy), typeof(AmbiguousMethodTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IAmbiguousMethodProxy), typeof(AmbiguousMethodTarget));

            AssertCannotCreate(scenarioId, dynamicResult, aotResult);
        }

        [Fact]
        public void DifferentialParityB27ReverseMethodAttributeShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "B-27";
            DuckTypeAotEngine.RegisterReverseProxy(
                typeof(IReverseB27Proxy),
                typeof(ReverseB27Delegation),
                typeof(ReverseB27AotProxy),
                instance => new ReverseB27AotProxy((ReverseB27Delegation)instance!));

            var dynamicResult = InvokeDynamicReverse(typeof(IReverseB27Proxy), typeof(ReverseB27Delegation));
            var aotResult = DuckTypeAotEngine.GetOrCreateReverseProxyType(typeof(IReverseB27Proxy), typeof(ReverseB27Delegation));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IReverseB27Proxy>(new ReverseB27Delegation());
            var aotProxy = aotResult.CreateInstance<IReverseB27Proxy>(new ReverseB27Delegation());

            aotProxy.Increment(5).Should().Be(dynamicProxy.Increment(5), $"scenario {scenarioId} should preserve reverse method mapping with [DuckReverseMethod]");
        }

        [Fact]
        public void DifferentialParityC28ForwardDuckChainingShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "C-28";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IChainInnerProxy),
                typeof(ChainInnerTarget),
                typeof(ChainInnerAotProxy),
                instance => new ChainInnerAotProxy((ChainInnerTarget)instance!));
            DuckTypeAotEngine.RegisterProxy(
                typeof(IChainOuterProxy),
                typeof(ChainOuterTarget),
                typeof(ChainOuterAotProxy),
                instance => new ChainOuterAotProxy((ChainOuterTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IChainOuterProxy), typeof(ChainOuterTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IChainOuterProxy), typeof(ChainOuterTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IChainOuterProxy>(new ChainOuterTarget(new ChainInnerTarget("chain")));
            var aotProxy = aotResult.CreateInstance<IChainOuterProxy>(new ChainOuterTarget(new ChainInnerTarget("chain")));

            aotProxy.Inner.Should().NotBeNull();
            dynamicProxy.Inner.Should().NotBeNull();
            aotProxy.Inner.Name.Should().Be(dynamicProxy.Inner.Name, $"scenario {scenarioId} should preserve chained proxy behavior");
        }

        [Fact]
        public void DifferentialParityC29ForwardDuckChainingNullShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "C-29";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IChainInnerProxy),
                typeof(ChainInnerTarget),
                typeof(ChainInnerAotProxy),
                instance => new ChainInnerAotProxy((ChainInnerTarget)instance!));
            DuckTypeAotEngine.RegisterProxy(
                typeof(IChainNullableOuterProxy),
                typeof(ChainNullableOuterTarget),
                typeof(ChainNullableOuterAotProxy),
                instance => new ChainNullableOuterAotProxy((ChainNullableOuterTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IChainNullableOuterProxy), typeof(ChainNullableOuterTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IChainNullableOuterProxy), typeof(ChainNullableOuterTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IChainNullableOuterProxy>(new ChainNullableOuterTarget(inner: null));
            var aotProxy = aotResult.CreateInstance<IChainNullableOuterProxy>(new ChainNullableOuterTarget(inner: null));

            aotProxy.Inner.Should().BeNull($"scenario {scenarioId} should preserve null duck chaining");
            dynamicProxy.Inner.Should().BeNull($"scenario {scenarioId} should preserve null duck chaining in dynamic mode");
        }

        [Fact]
        public void DifferentialParityC30ForwardDuckChainingFromMethodShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "C-30";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IChainInnerProxy),
                typeof(ChainInnerTarget),
                typeof(ChainInnerAotProxy),
                instance => new ChainInnerAotProxy((ChainInnerTarget)instance!));
            DuckTypeAotEngine.RegisterProxy(
                typeof(IChainMethodOuterProxy),
                typeof(ChainMethodOuterTarget),
                typeof(ChainMethodOuterAotProxy),
                instance => new ChainMethodOuterAotProxy((ChainMethodOuterTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IChainMethodOuterProxy), typeof(ChainMethodOuterTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IChainMethodOuterProxy), typeof(ChainMethodOuterTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IChainMethodOuterProxy>(new ChainMethodOuterTarget(new ChainInnerTarget("inner")));
            var aotProxy = aotResult.CreateInstance<IChainMethodOuterProxy>(new ChainMethodOuterTarget(new ChainInnerTarget("inner")));

            aotProxy.GetInner().Name.Should().Be(dynamicProxy.GetInner().Name, $"scenario {scenarioId} should preserve method-return duck chaining");
        }

        [Fact]
        public void DifferentialParityC31ForwardNullableDuckChainingShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "C-31";
            DuckTypeAotEngine.RegisterProxy(
                typeof(INullableDuckChainProxy),
                typeof(NullableDuckChainTarget),
                typeof(NullableDuckChainAotProxy),
                instance => new NullableDuckChainAotProxy((NullableDuckChainTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(INullableDuckChainProxy), typeof(NullableDuckChainTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(INullableDuckChainProxy), typeof(NullableDuckChainTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<INullableDuckChainProxy>(new NullableDuckChainTarget());
            var aotProxy = aotResult.CreateInstance<INullableDuckChainProxy>(new NullableDuckChainTarget());

            var dynamicHasValue = dynamicProxy.TryGetInner(true);
            var aotHasValue = aotProxy.TryGetInner(true);
            aotHasValue.HasValue.Should().Be(dynamicHasValue.HasValue, $"scenario {scenarioId} should preserve nullable duck chaining has-value behavior");
            if (!aotHasValue.HasValue || !dynamicHasValue.HasValue)
            {
                throw new InvalidOperationException($"scenario {scenarioId} test precondition should produce a value");
            }

            aotHasValue.Value.Number.Should().Be(dynamicHasValue.Value.Number, $"scenario {scenarioId} should preserve nullable duck chaining value projection");

            var dynamicNoValue = dynamicProxy.TryGetInner(false);
            var aotNoValue = aotProxy.TryGetInner(false);
            aotNoValue.HasValue.Should().Be(dynamicNoValue.HasValue, $"scenario {scenarioId} should preserve nullable duck chaining null behavior");
        }

        [Fact]
        public void DifferentialParityC32ForwardValueWithTypeWrapperShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "C-32";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IValueWithTypeProxy),
                typeof(ValueWithTypeTarget),
                typeof(ValueWithTypeAotProxy),
                instance => new ValueWithTypeAotProxy((ValueWithTypeTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IValueWithTypeProxy), typeof(ValueWithTypeTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IValueWithTypeProxy), typeof(ValueWithTypeTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicTarget = new ValueWithTypeTarget();
            var aotTarget = new ValueWithTypeTarget();
            var dynamicProxy = dynamicResult.CreateInstance<IValueWithTypeProxy>(dynamicTarget);
            var aotProxy = aotResult.CreateInstance<IValueWithTypeProxy>(aotTarget);

            var dynamicRead = dynamicProxy.Count;
            var aotRead = aotProxy.Count;
            aotRead.Value.Should().Be(dynamicRead.Value, $"scenario {scenarioId} should preserve ValueWithType return value");
            aotRead.Type.Should().Be(dynamicRead.Type, $"scenario {scenarioId} should preserve ValueWithType return runtime type metadata");

            var input = ValueWithType<int>.Create(37, typeof(int));
            dynamicProxy.Count = input;
            aotProxy.Count = input;
            aotProxy.Count.Value.Should().Be(dynamicProxy.Count.Value, $"scenario {scenarioId} should preserve ValueWithType setter extraction");
        }

        [Fact]
        public void DifferentialParityC33ForwardEnumNormalizationShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "C-33";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IEnumConversionProxy),
                typeof(EnumConversionTarget),
                typeof(EnumConversionAotProxy),
                instance => new EnumConversionAotProxy((EnumConversionTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IEnumConversionProxy), typeof(EnumConversionTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IEnumConversionProxy), typeof(EnumConversionTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IEnumConversionProxy>(new EnumConversionTarget());
            var aotProxy = aotResult.CreateInstance<IEnumConversionProxy>(new EnumConversionTarget());

            aotProxy.Echo(EnumConversionValue.Two).Should().Be(dynamicProxy.Echo(EnumConversionValue.Two), $"scenario {scenarioId} should preserve enum underlying-type normalization");
        }

        [Fact]
        public void DifferentialParityD34ReverseInterfaceShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "D-34";
            DuckTypeAotEngine.RegisterReverseProxy(
                typeof(IReverseGreetingProxy),
                typeof(ReverseGreetingDelegation),
                typeof(ReverseGreetingAotProxy),
                instance => new ReverseGreetingAotProxy((ReverseGreetingDelegation)instance!));

            var dynamicResult = InvokeDynamicReverse(typeof(IReverseGreetingProxy), typeof(ReverseGreetingDelegation));
            var aotResult = DuckTypeAotEngine.GetOrCreateReverseProxyType(typeof(IReverseGreetingProxy), typeof(ReverseGreetingDelegation));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var delegation = new ReverseGreetingDelegation("beta");
            var dynamicProxy = dynamicResult.CreateInstance<IReverseGreetingProxy>(delegation);
            var aotProxy = aotResult.CreateInstance<IReverseGreetingProxy>(delegation);

            aotProxy.Greet().Should().Be(dynamicProxy.Greet(), $"scenario {scenarioId} should produce equivalent reverse proxy behavior");
        }

        [Fact]
        public void DifferentialParityD35ReversePropertyShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "D-35";
            DuckTypeAotEngine.RegisterReverseProxy(
                typeof(IReverseStateProxy),
                typeof(ReverseStateDelegation),
                typeof(ReverseStateAotProxy),
                instance => new ReverseStateAotProxy((ReverseStateDelegation)instance!));

            var dynamicResult = InvokeDynamicReverse(typeof(IReverseStateProxy), typeof(ReverseStateDelegation));
            var aotResult = DuckTypeAotEngine.GetOrCreateReverseProxyType(typeof(IReverseStateProxy), typeof(ReverseStateDelegation));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IReverseStateProxy>(new ReverseStateDelegation());
            var aotProxy = aotResult.CreateInstance<IReverseStateProxy>(new ReverseStateDelegation());

            dynamicProxy.State = 21;
            aotProxy.State = 21;

            aotProxy.State.Should().Be(dynamicProxy.State, $"scenario {scenarioId} should preserve reverse proxy property behavior");
        }

        [Fact]
        public void DifferentialParityD36ReverseMethodShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "D-36";
            DuckTypeAotEngine.RegisterReverseProxy(
                typeof(IReverseMathProxy),
                typeof(ReverseMathDelegation),
                typeof(ReverseMathAotProxy),
                instance => new ReverseMathAotProxy((ReverseMathDelegation)instance!));

            var dynamicResult = InvokeDynamicReverse(typeof(IReverseMathProxy), typeof(ReverseMathDelegation));
            var aotResult = DuckTypeAotEngine.GetOrCreateReverseProxyType(typeof(IReverseMathProxy), typeof(ReverseMathDelegation));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IReverseMathProxy>(new ReverseMathDelegation());
            var aotProxy = aotResult.CreateInstance<IReverseMathProxy>(new ReverseMathDelegation());

            aotProxy.Multiply(6, 7).Should().Be(dynamicProxy.Multiply(6, 7), $"scenario {scenarioId} should preserve reverse method behavior");
        }

        [Fact]
        public void DifferentialParityD37ReverseRefOutShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "D-37";
            DuckTypeAotEngine.RegisterReverseProxy(
                typeof(IReverseRefOutProxy),
                typeof(ReverseRefOutDelegation),
                typeof(ReverseRefOutAotProxy),
                instance => new ReverseRefOutAotProxy((ReverseRefOutDelegation)instance!));

            var dynamicResult = InvokeDynamicReverse(typeof(IReverseRefOutProxy), typeof(ReverseRefOutDelegation));
            var aotResult = DuckTypeAotEngine.GetOrCreateReverseProxyType(typeof(IReverseRefOutProxy), typeof(ReverseRefOutDelegation));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IReverseRefOutProxy>(new ReverseRefOutDelegation());
            var aotProxy = aotResult.CreateInstance<IReverseRefOutProxy>(new ReverseRefOutDelegation());

            var dynamicValue = -9;
            var aotValue = -9;

            dynamicProxy.Normalize(ref dynamicValue, out var dynamicDoubled);
            aotProxy.Normalize(ref aotValue, out var aotDoubled);

            aotValue.Should().Be(dynamicValue, $"scenario {scenarioId} should preserve reverse ref behavior");
            aotDoubled.Should().Be(dynamicDoubled, $"scenario {scenarioId} should preserve reverse out behavior");
        }

        [Fact]
        public void DifferentialParityE38ReverseAbstractOverrideShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "E-38";
            DuckTypeAotEngine.RegisterReverseProxy(
                typeof(ReverseAbstractBase),
                typeof(ReverseAbstractDelegation),
                typeof(ReverseAbstractAotProxy),
                instance => new ReverseAbstractAotProxy((ReverseAbstractDelegation)instance!));

            var dynamicResult = InvokeDynamicReverse(typeof(ReverseAbstractBase), typeof(ReverseAbstractDelegation));
            var aotResult = DuckTypeAotEngine.GetOrCreateReverseProxyType(typeof(ReverseAbstractBase), typeof(ReverseAbstractDelegation));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<ReverseAbstractBase>(new ReverseAbstractDelegation());
            var aotProxy = aotResult.CreateInstance<ReverseAbstractBase>(new ReverseAbstractDelegation());

            aotProxy.Compute(8, 3).Should().Be(dynamicProxy.Compute(8, 3), $"scenario {scenarioId} should preserve reverse abstract override behavior");
        }

        [Fact]
        public void DifferentialParityE39ReverseMissingRequiredImplementationShouldFailInBothModes()
        {
            const string scenarioId = "E-39";
            var dynamicResult = InvokeDynamicReverse(typeof(ReverseRequiredMethodBase), typeof(ReverseRequiredMethodDelegation));
            var aotResult = DuckTypeAotEngine.GetOrCreateReverseProxyType(typeof(ReverseRequiredMethodBase), typeof(ReverseRequiredMethodDelegation));

            AssertCannotCreate(scenarioId, dynamicResult, aotResult);
        }

        [Fact]
        public void DifferentialParityE40ReverseGenericContractMismatchShouldFailInBothModes()
        {
            const string scenarioId = "E-40";
            var dynamicResult = InvokeDynamicReverse(typeof(ReverseGenericContractBase), typeof(ReverseGenericMismatchDelegation));
            var aotResult = DuckTypeAotEngine.GetOrCreateReverseProxyType(typeof(ReverseGenericContractBase), typeof(ReverseGenericMismatchDelegation));

            AssertCannotCreate(scenarioId, dynamicResult, aotResult);
        }

        [Fact]
        public void DifferentialParityE41ReverseCustomAttributeCopyShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "E-41";
            DuckTypeAotEngine.RegisterReverseProxy(
                typeof(IReverseAttributeCopyProxy),
                typeof(ReverseAttributeCopyDelegation),
                typeof(ReverseAttributeCopyAotProxy),
                instance => new ReverseAttributeCopyAotProxy((ReverseAttributeCopyDelegation)instance!));

            var dynamicResult = InvokeDynamicReverse(typeof(IReverseAttributeCopyProxy), typeof(ReverseAttributeCopyDelegation));
            var aotResult = DuckTypeAotEngine.GetOrCreateReverseProxyType(typeof(IReverseAttributeCopyProxy), typeof(ReverseAttributeCopyDelegation));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicAttribute = dynamicResult.ProxyType!.GetCustomAttribute<ReverseMarkerAttribute>();
            var aotAttribute = aotResult.ProxyType!.GetCustomAttribute<ReverseMarkerAttribute>();
            dynamicAttribute.Should().NotBeNull($"scenario {scenarioId} dynamic mode should expose copied reverse custom attributes");
            aotAttribute.Should().NotBeNull($"scenario {scenarioId} AOT mode should expose equivalent custom attributes");
            aotAttribute!.Marker.Should().Be(dynamicAttribute!.Marker, $"scenario {scenarioId} should preserve reverse custom attribute values");
        }

        [Fact]
        public void DifferentialParityE42ReverseTypeConstraintsShouldFailInBothModes()
        {
            const string scenarioId = "E-42";
            var dynamicResult = InvokeDynamicReverse(typeof(ReverseStructBase), typeof(ReverseStructDelegation));
            var aotResult = DuckTypeAotEngine.GetOrCreateReverseProxyType(typeof(ReverseStructBase), typeof(ReverseStructDelegation));

            AssertCannotCreate(scenarioId, dynamicResult, aotResult);
        }

        [Fact]
        public void DifferentialParityFG1PropertyGetterPublicReferenceShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "FG-1";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IFg1GetterProxy),
                typeof(Fg1GetterTarget),
                typeof(Fg1GetterAotProxy),
                instance => new Fg1GetterAotProxy((Fg1GetterTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IFg1GetterProxy), typeof(Fg1GetterTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IFg1GetterProxy), typeof(Fg1GetterTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IFg1GetterProxy>(new Fg1GetterTarget("fg-alpha"));
            var aotProxy = aotResult.CreateInstance<IFg1GetterProxy>(new Fg1GetterTarget("fg-alpha"));

            aotProxy.Name.Should().Be(dynamicProxy.Name, $"scenario {scenarioId} should preserve public reference getter behavior");
        }

        [Fact]
        public void DifferentialParityFS1PropertySetterPublicShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "FS-1";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IFs1SetterProxy),
                typeof(Fs1SetterTarget),
                typeof(Fs1SetterAotProxy),
                instance => new Fs1SetterAotProxy((Fs1SetterTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IFs1SetterProxy), typeof(Fs1SetterTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IFs1SetterProxy), typeof(Fs1SetterTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicTarget = new Fs1SetterTarget();
            var aotTarget = new Fs1SetterTarget();
            var dynamicProxy = dynamicResult.CreateInstance<IFs1SetterProxy>(dynamicTarget);
            var aotProxy = aotResult.CreateInstance<IFs1SetterProxy>(aotTarget);

            dynamicProxy.Count = 19;
            aotProxy.Count = 19;

            aotProxy.Count.Should().Be(dynamicProxy.Count, $"scenario {scenarioId} should preserve setter/getter parity");
            aotTarget.Count.Should().Be(dynamicTarget.Count, $"scenario {scenarioId} should preserve target mutation parity");
        }

        [Fact]
        public void DifferentialParityFF1FieldGetSetShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "FF-1";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IFf1FieldProxy),
                typeof(Ff1FieldTarget),
                typeof(Ff1FieldAotProxy),
                instance => new Ff1FieldAotProxy((Ff1FieldTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IFf1FieldProxy), typeof(Ff1FieldTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IFf1FieldProxy), typeof(Ff1FieldTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicTarget = new Ff1FieldTarget();
            var aotTarget = new Ff1FieldTarget();
            var dynamicProxy = dynamicResult.CreateInstance<IFf1FieldProxy>(dynamicTarget);
            var aotProxy = aotResult.CreateInstance<IFf1FieldProxy>(aotTarget);

            aotProxy.Value.Should().Be(dynamicProxy.Value, $"scenario {scenarioId} should preserve private field getter behavior");

            dynamicProxy.Value = 42;
            aotProxy.Value = 42;
            aotTarget.Peek().Should().Be(dynamicTarget.Peek(), $"scenario {scenarioId} should preserve private field setter behavior");
        }

        [Fact]
        public void DifferentialParityFM1MethodCallShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "FM-1";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IFm1MethodProxy),
                typeof(Fm1MethodTarget),
                typeof(Fm1MethodAotProxy),
                instance => new Fm1MethodAotProxy((Fm1MethodTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IFm1MethodProxy), typeof(Fm1MethodTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IFm1MethodProxy), typeof(Fm1MethodTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IFm1MethodProxy>(new Fm1MethodTarget("left"));
            var aotProxy = aotResult.CreateInstance<IFm1MethodProxy>(new Fm1MethodTarget("left"));

            aotProxy.Join("right").Should().Be(dynamicProxy.Join("right"), $"scenario {scenarioId} should preserve instance method call behavior");
        }

        [Fact]
        public void DifferentialParityRT1VoidReturnShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "RT-1";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IRt1VoidProxy),
                typeof(Rt1VoidTarget),
                typeof(Rt1VoidAotProxy),
                instance => new Rt1VoidAotProxy((Rt1VoidTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IRt1VoidProxy), typeof(Rt1VoidTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IRt1VoidProxy), typeof(Rt1VoidTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IRt1VoidProxy>(new Rt1VoidTarget());
            var aotProxy = aotResult.CreateInstance<IRt1VoidProxy>(new Rt1VoidTarget());

            dynamicProxy.Touch(5);
            aotProxy.Touch(5);
            dynamicProxy.Touch(-2);
            aotProxy.Touch(-2);

            aotProxy.Read().Should().Be(dynamicProxy.Read(), $"scenario {scenarioId} should preserve void-return side-effect behavior");
        }

        [Fact]
        public void DifferentialParityFG2PropertyGetterPublicValueShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "FG-2";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IFg2GetterProxy),
                typeof(Fg2GetterTarget),
                typeof(Fg2GetterAotProxy),
                instance => new Fg2GetterAotProxy((Fg2GetterTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IFg2GetterProxy), typeof(Fg2GetterTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IFg2GetterProxy), typeof(Fg2GetterTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IFg2GetterProxy>(new Fg2GetterTarget(29));
            var aotProxy = aotResult.CreateInstance<IFg2GetterProxy>(new Fg2GetterTarget(29));

            aotProxy.Count.Should().Be(dynamicProxy.Count, $"scenario {scenarioId} should preserve public value getter behavior");
        }

        [Fact]
        public void DifferentialParityFS5StaticSetterShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "FS-5";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IFs5StaticSetterProxy),
                typeof(Fs5StaticSetterTarget),
                typeof(Fs5StaticSetterAotProxy),
                instance => new Fs5StaticSetterAotProxy((Fs5StaticSetterTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IFs5StaticSetterProxy), typeof(Fs5StaticSetterTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IFs5StaticSetterProxy), typeof(Fs5StaticSetterTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            Fs5StaticSetterTarget.GlobalCount = 0;
            var dynamicProxy = dynamicResult.CreateInstance<IFs5StaticSetterProxy>(new Fs5StaticSetterTarget());
            var aotProxy = aotResult.CreateInstance<IFs5StaticSetterProxy>(new Fs5StaticSetterTarget());

            dynamicProxy.Count = 61;
            aotProxy.Count = 61;

            aotProxy.Count.Should().Be(dynamicProxy.Count, $"scenario {scenarioId} should preserve static setter/getter behavior");
            Fs5StaticSetterTarget.GlobalCount.Should().Be(61, $"scenario {scenarioId} should mutate static target state");
        }

        [Fact]
        public void DifferentialParityFF2StaticFieldShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "FF-2";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IFf2StaticFieldProxy),
                typeof(Ff2StaticFieldTarget),
                typeof(Ff2StaticFieldAotProxy),
                instance => new Ff2StaticFieldAotProxy((Ff2StaticFieldTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IFf2StaticFieldProxy), typeof(Ff2StaticFieldTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IFf2StaticFieldProxy), typeof(Ff2StaticFieldTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            Ff2StaticFieldTarget.GlobalValue = 3;
            var dynamicProxy = dynamicResult.CreateInstance<IFf2StaticFieldProxy>(new Ff2StaticFieldTarget());
            var aotProxy = aotResult.CreateInstance<IFf2StaticFieldProxy>(new Ff2StaticFieldTarget());

            aotProxy.Value.Should().Be(dynamicProxy.Value, $"scenario {scenarioId} should preserve static field getter behavior");

            dynamicProxy.Value = 72;
            aotProxy.Value = 72;
            Ff2StaticFieldTarget.GlobalValue.Should().Be(72, $"scenario {scenarioId} should preserve static field setter behavior");
        }

        [Fact]
        public void DifferentialParityFM8OptionalParameterShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "FM-8";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IFm8OptionalProxy),
                typeof(Fm8OptionalTarget),
                typeof(Fm8OptionalAotProxy),
                instance => new Fm8OptionalAotProxy((Fm8OptionalTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IFm8OptionalProxy), typeof(Fm8OptionalTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IFm8OptionalProxy), typeof(Fm8OptionalTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IFm8OptionalProxy>(new Fm8OptionalTarget());
            var aotProxy = aotResult.CreateInstance<IFm8OptionalProxy>(new Fm8OptionalTarget());

            aotProxy.AddWithDefault(10).Should().Be(dynamicProxy.AddWithDefault(10), $"scenario {scenarioId} should preserve default optional parameter binding");
            aotProxy.AddWithDefault(10, 5).Should().Be(dynamicProxy.AddWithDefault(10, 5), $"scenario {scenarioId} should preserve explicit optional parameter binding");
        }

        [Fact]
        public void DifferentialParityRT3ReturnConversionShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "RT-3";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IRt3ReturnConversionProxy),
                typeof(Rt3ReturnConversionTarget),
                typeof(Rt3ReturnConversionAotProxy),
                instance => new Rt3ReturnConversionAotProxy((Rt3ReturnConversionTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IRt3ReturnConversionProxy), typeof(Rt3ReturnConversionTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IRt3ReturnConversionProxy), typeof(Rt3ReturnConversionTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IRt3ReturnConversionProxy>(new Rt3ReturnConversionTarget());
            var aotProxy = aotResult.CreateInstance<IRt3ReturnConversionProxy>(new Rt3ReturnConversionTarget());

            aotProxy.GetCount().Should().Be(dynamicProxy.GetCount(), $"scenario {scenarioId} should preserve conversion-only return semantics");
        }

        [Fact]
        public void DifferentialParityFG3PropertyGetterNonPublicShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "FG-3";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IFg3NonPublicGetterProxy),
                typeof(Fg3NonPublicGetterTarget),
                typeof(Fg3NonPublicGetterAotProxy),
                instance => new Fg3NonPublicGetterAotProxy((Fg3NonPublicGetterTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IFg3NonPublicGetterProxy), typeof(Fg3NonPublicGetterTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IFg3NonPublicGetterProxy), typeof(Fg3NonPublicGetterTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IFg3NonPublicGetterProxy>(new Fg3NonPublicGetterTarget(17));
            var aotProxy = aotResult.CreateInstance<IFg3NonPublicGetterProxy>(new Fg3NonPublicGetterTarget(17));

            aotProxy.Secret.Should().Be(dynamicProxy.Secret, $"scenario {scenarioId} should preserve non-public getter behavior");
        }

        [Fact]
        public void DifferentialParityFG4PropertyGetterStaticShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "FG-4";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IFg4StaticGetterProxy),
                typeof(Fg4StaticGetterTarget),
                typeof(Fg4StaticGetterAotProxy),
                instance => new Fg4StaticGetterAotProxy((Fg4StaticGetterTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IFg4StaticGetterProxy), typeof(Fg4StaticGetterTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IFg4StaticGetterProxy), typeof(Fg4StaticGetterTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            Fg4StaticGetterTarget.Global = 77;
            var dynamicProxy = dynamicResult.CreateInstance<IFg4StaticGetterProxy>(new Fg4StaticGetterTarget());
            var aotProxy = aotResult.CreateInstance<IFg4StaticGetterProxy>(new Fg4StaticGetterTarget());

            aotProxy.Global.Should().Be(dynamicProxy.Global, $"scenario {scenarioId} should preserve static getter behavior");
        }

        [Fact]
        public void DifferentialParityFG5PropertyGetterValueWithTypeShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "FG-5";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IFg5ValueWithTypeGetterProxy),
                typeof(Fg5ValueWithTypeGetterTarget),
                typeof(Fg5ValueWithTypeGetterAotProxy),
                instance => new Fg5ValueWithTypeGetterAotProxy((Fg5ValueWithTypeGetterTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IFg5ValueWithTypeGetterProxy), typeof(Fg5ValueWithTypeGetterTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IFg5ValueWithTypeGetterProxy), typeof(Fg5ValueWithTypeGetterTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IFg5ValueWithTypeGetterProxy>(new Fg5ValueWithTypeGetterTarget(21));
            var aotProxy = aotResult.CreateInstance<IFg5ValueWithTypeGetterProxy>(new Fg5ValueWithTypeGetterTarget(21));

            aotProxy.Count.Value.Should().Be(dynamicProxy.Count.Value, $"scenario {scenarioId} should preserve ValueWithType getter value");
            aotProxy.Count.Type.Should().Be(dynamicProxy.Count.Type, $"scenario {scenarioId} should preserve ValueWithType getter type metadata");
        }

        [Fact]
        public void DifferentialParityFG6PropertyGetterDuckChainingShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "FG-6";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IFg6ChainInnerProxy),
                typeof(Fg6ChainInnerTarget),
                typeof(Fg6ChainInnerAotProxy),
                instance => new Fg6ChainInnerAotProxy((Fg6ChainInnerTarget)instance!));
            DuckTypeAotEngine.RegisterProxy(
                typeof(IFg6ChainOuterProxy),
                typeof(Fg6ChainOuterTarget),
                typeof(Fg6ChainOuterAotProxy),
                instance => new Fg6ChainOuterAotProxy((Fg6ChainOuterTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IFg6ChainOuterProxy), typeof(Fg6ChainOuterTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IFg6ChainOuterProxy), typeof(Fg6ChainOuterTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IFg6ChainOuterProxy>(new Fg6ChainOuterTarget(new Fg6ChainInnerTarget("fg6")));
            var aotProxy = aotResult.CreateInstance<IFg6ChainOuterProxy>(new Fg6ChainOuterTarget(new Fg6ChainInnerTarget("fg6")));

            aotProxy.Inner.Name.Should().Be(dynamicProxy.Inner.Name, $"scenario {scenarioId} should preserve forward getter duck chaining");
        }

        [Fact]
        public void DifferentialParityFG9PropertyGetterFallbackShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "FG-9";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IFg9FallbackGetterProxy),
                typeof(Fg9FallbackGetterTarget),
                typeof(Fg9FallbackGetterAotProxy),
                instance => new Fg9FallbackGetterAotProxy((Fg9FallbackGetterTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IFg9FallbackGetterProxy), typeof(Fg9FallbackGetterTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IFg9FallbackGetterProxy), typeof(Fg9FallbackGetterTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IFg9FallbackGetterProxy>(new Fg9FallbackGetterTarget(81));
            var aotProxy = aotResult.CreateInstance<IFg9FallbackGetterProxy>(new Fg9FallbackGetterTarget(81));

            aotProxy.Hidden.Should().Be(dynamicProxy.Hidden, $"scenario {scenarioId} should preserve fallback getter behavior");
        }

        [Fact]
        public void DifferentialParityFS2PropertySetterNonPublicShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "FS-2";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IFs2NonPublicSetterProxy),
                typeof(Fs2NonPublicSetterTarget),
                typeof(Fs2NonPublicSetterAotProxy),
                instance => new Fs2NonPublicSetterAotProxy((Fs2NonPublicSetterTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IFs2NonPublicSetterProxy), typeof(Fs2NonPublicSetterTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IFs2NonPublicSetterProxy), typeof(Fs2NonPublicSetterTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicTarget = new Fs2NonPublicSetterTarget();
            var aotTarget = new Fs2NonPublicSetterTarget();
            var dynamicProxy = dynamicResult.CreateInstance<IFs2NonPublicSetterProxy>(dynamicTarget);
            var aotProxy = aotResult.CreateInstance<IFs2NonPublicSetterProxy>(aotTarget);

            dynamicProxy.Hidden = 34;
            aotProxy.Hidden = 34;

            aotProxy.Read().Should().Be(dynamicProxy.Read(), $"scenario {scenarioId} should preserve non-public setter behavior");
        }

        [Fact]
        public void DifferentialParityFS6PropertySetterFallbackShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "FS-6";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IFs6FallbackSetterProxy),
                typeof(Fs6FallbackSetterTarget),
                typeof(Fs6FallbackSetterAotProxy),
                instance => new Fs6FallbackSetterAotProxy((Fs6FallbackSetterTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IFs6FallbackSetterProxy), typeof(Fs6FallbackSetterTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IFs6FallbackSetterProxy), typeof(Fs6FallbackSetterTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicTarget = new Fs6FallbackSetterTarget();
            var aotTarget = new Fs6FallbackSetterTarget();
            var dynamicProxy = dynamicResult.CreateInstance<IFs6FallbackSetterProxy>(dynamicTarget);
            var aotProxy = aotResult.CreateInstance<IFs6FallbackSetterProxy>(aotTarget);

            dynamicProxy.Hidden = 48;
            aotProxy.Hidden = 48;

            aotProxy.Read().Should().Be(dynamicProxy.Read(), $"scenario {scenarioId} should preserve fallback setter behavior");
        }

        [Fact]
        public void DifferentialParityFF3FieldSetterInstanceShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "FF-3";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IFf3InstanceFieldSetProxy),
                typeof(Ff3InstanceFieldSetTarget),
                typeof(Ff3InstanceFieldSetAotProxy),
                instance => new Ff3InstanceFieldSetAotProxy((Ff3InstanceFieldSetTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IFf3InstanceFieldSetProxy), typeof(Ff3InstanceFieldSetTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IFf3InstanceFieldSetProxy), typeof(Ff3InstanceFieldSetTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicTarget = new Ff3InstanceFieldSetTarget();
            var aotTarget = new Ff3InstanceFieldSetTarget();
            var dynamicProxy = dynamicResult.CreateInstance<IFf3InstanceFieldSetProxy>(dynamicTarget);
            var aotProxy = aotResult.CreateInstance<IFf3InstanceFieldSetProxy>(aotTarget);

            dynamicProxy.Value = 64;
            aotProxy.Value = 64;

            aotTarget.Read().Should().Be(dynamicTarget.Read(), $"scenario {scenarioId} should preserve instance field setter behavior");
        }

        [Fact]
        public void DifferentialParityFF4FieldSetterStaticShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "FF-4";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IFf4StaticFieldSetProxy),
                typeof(Ff4StaticFieldSetTarget),
                typeof(Ff4StaticFieldSetAotProxy),
                instance => new Ff4StaticFieldSetAotProxy((Ff4StaticFieldSetTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IFf4StaticFieldSetProxy), typeof(Ff4StaticFieldSetTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IFf4StaticFieldSetProxy), typeof(Ff4StaticFieldSetTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IFf4StaticFieldSetProxy>(new Ff4StaticFieldSetTarget());
            var aotProxy = aotResult.CreateInstance<IFf4StaticFieldSetProxy>(new Ff4StaticFieldSetTarget());

            Ff4StaticFieldSetTarget.Reset();
            dynamicProxy.Value = 33;
            var dynamicRead = Ff4StaticFieldSetTarget.Read();

            Ff4StaticFieldSetTarget.Reset();
            aotProxy.Value = 33;
            var aotRead = Ff4StaticFieldSetTarget.Read();

            aotRead.Should().Be(dynamicRead, $"scenario {scenarioId} should preserve static field setter behavior");
        }

        [Fact]
        public void DifferentialParityFF5FieldFallbackShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "FF-5";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IFf5FallbackFieldProxy),
                typeof(Ff5FallbackFieldTarget),
                typeof(Ff5FallbackFieldAotProxy),
                instance => new Ff5FallbackFieldAotProxy((Ff5FallbackFieldTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IFf5FallbackFieldProxy), typeof(Ff5FallbackFieldTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IFf5FallbackFieldProxy), typeof(Ff5FallbackFieldTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IFf5FallbackFieldProxy>(new Ff5FallbackFieldTarget(99));
            var aotProxy = aotResult.CreateInstance<IFf5FallbackFieldProxy>(new Ff5FallbackFieldTarget(99));

            aotProxy.Hidden.Should().Be(dynamicProxy.Hidden, $"scenario {scenarioId} should preserve fallback field getter behavior");
        }

        [Fact]
        public void DifferentialParityRT2VoidMismatchShouldFailInBothModes()
        {
            const string scenarioId = "RT-2";
            var dynamicResult = InvokeDynamicForward(typeof(IRt2VoidMismatchProxy), typeof(Rt2VoidMismatchTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IRt2VoidMismatchProxy), typeof(Rt2VoidMismatchTarget));

            AssertCannotCreate(scenarioId, dynamicResult, aotResult);
        }

        [Fact]
        public void DifferentialParityFG7PropertyGetterReverseFlowShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "FG-7";
            DuckTypeAotEngine.RegisterReverseProxy(
                typeof(IFg7ReverseGetterProxy),
                typeof(Fg7ReverseGetterDelegation),
                typeof(Fg7ReverseGetterAotProxy),
                instance => new Fg7ReverseGetterAotProxy((Fg7ReverseGetterDelegation)instance!));

            var dynamicResult = InvokeDynamicReverse(typeof(IFg7ReverseGetterProxy), typeof(Fg7ReverseGetterDelegation));
            var aotResult = DuckTypeAotEngine.GetOrCreateReverseProxyType(typeof(IFg7ReverseGetterProxy), typeof(Fg7ReverseGetterDelegation));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IFg7ReverseGetterProxy>(new Fg7ReverseGetterDelegation("fg7"));
            var aotProxy = aotResult.CreateInstance<IFg7ReverseGetterProxy>(new Fg7ReverseGetterDelegation("fg7"));

            aotProxy.Name.Should().Be(dynamicProxy.Name, $"scenario {scenarioId} should preserve reverse getter flow behavior");
        }

        [Fact]
        public void DifferentialParityFG8PropertyIndexerConversionShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "FG-8";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IFg8IndexerInnerProxy),
                typeof(Fg8IndexerInnerTarget),
                typeof(Fg8IndexerInnerAotProxy),
                instance => new Fg8IndexerInnerAotProxy((Fg8IndexerInnerTarget)instance!));
            DuckTypeAotEngine.RegisterProxy(
                typeof(IFg8IndexerProxy),
                typeof(Fg8IndexerTarget),
                typeof(Fg8IndexerAotProxy),
                instance => new Fg8IndexerAotProxy((Fg8IndexerTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IFg8IndexerProxy), typeof(Fg8IndexerTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IFg8IndexerProxy), typeof(Fg8IndexerTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IFg8IndexerProxy>(new Fg8IndexerTarget());
            var aotProxy = aotResult.CreateInstance<IFg8IndexerProxy>(new Fg8IndexerTarget());

            aotProxy[7].Number.Should().Be(dynamicProxy[7].Number, $"scenario {scenarioId} should preserve indexer argument conversion and duck extraction");
        }

        [Fact]
        public void DifferentialParityFS3SetterDuckExtractionShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "FS-3";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IFs3SetterInnerProxy),
                typeof(Fs3SetterInnerTarget),
                typeof(Fs3SetterInnerAotProxy),
                instance => new Fs3SetterInnerAotProxy((Fs3SetterInnerTarget)instance!));
            DuckTypeAotEngine.RegisterProxy(
                typeof(IFs3SetterDuckExtractProxy),
                typeof(Fs3SetterDuckExtractTarget),
                typeof(Fs3SetterDuckExtractAotProxy),
                instance => new Fs3SetterDuckExtractAotProxy((Fs3SetterDuckExtractTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IFs3SetterDuckExtractProxy), typeof(Fs3SetterDuckExtractTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IFs3SetterDuckExtractProxy), typeof(Fs3SetterDuckExtractTarget));
            var dynamicInnerResult = InvokeDynamicForward(typeof(IFs3SetterInnerProxy), typeof(Fs3SetterInnerTarget));
            var aotInnerResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IFs3SetterInnerProxy), typeof(Fs3SetterInnerTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");
            dynamicInnerResult.CanCreate().Should().BeTrue($"scenario {scenarioId} inner proxy must be creatable in dynamic mode");
            aotInnerResult.CanCreate().Should().BeTrue($"scenario {scenarioId} inner proxy must be creatable in AOT mode");

            var dynamicTarget = new Fs3SetterDuckExtractTarget();
            var aotTarget = new Fs3SetterDuckExtractTarget();
            var dynamicProxy = dynamicResult.CreateInstance<IFs3SetterDuckExtractProxy>(dynamicTarget);
            var aotProxy = aotResult.CreateInstance<IFs3SetterDuckExtractProxy>(aotTarget);
            var dynamicInnerProxy = dynamicInnerResult.CreateInstance<IFs3SetterInnerProxy>(new Fs3SetterInnerTarget(15));
            var aotInnerProxy = aotInnerResult.CreateInstance<IFs3SetterInnerProxy>(new Fs3SetterInnerTarget(15));

            dynamicProxy.Inner = dynamicInnerProxy;
            aotProxy.Inner = aotInnerProxy;

            aotProxy.Read().Should().Be(dynamicProxy.Read(), $"scenario {scenarioId} should preserve setter duck extraction behavior");
        }

        [Fact]
        public void DifferentialParityFS4SetterDuckCreationShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "FS-4";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IFs4SetterDuckCreateProxy),
                typeof(Fs4SetterDuckCreateTarget),
                typeof(Fs4SetterDuckCreateAotProxy),
                instance => new Fs4SetterDuckCreateAotProxy((Fs4SetterDuckCreateTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IFs4SetterDuckCreateProxy), typeof(Fs4SetterDuckCreateTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IFs4SetterDuckCreateProxy), typeof(Fs4SetterDuckCreateTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicTarget = new Fs4SetterDuckCreateTarget();
            var aotTarget = new Fs4SetterDuckCreateTarget();
            var dynamicProxy = dynamicResult.CreateInstance<IFs4SetterDuckCreateProxy>(dynamicTarget);
            var aotProxy = aotResult.CreateInstance<IFs4SetterDuckCreateProxy>(aotTarget);

            Action dynamicSet = () => dynamicProxy.Inner = new Fs4SetterConcrete(28);
            Action aotSet = () => aotProxy.Inner = new Fs4SetterConcrete(28);

            dynamicSet.Should().Throw<InvalidCastException>($"scenario {scenarioId} dynamic mode should reject unsupported setter duck creation input");
            aotSet.Should().Throw<InvalidCastException>($"scenario {scenarioId} AOT mode should reject unsupported setter duck creation input");
        }

        [Fact]
        public void DifferentialParityFM2MethodValueTypeReceiverShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "FM-2";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IFm2ValueReceiverProxy),
                typeof(Fm2ValueReceiverTarget),
                typeof(Fm2ValueReceiverAotProxy),
                instance => new Fm2ValueReceiverAotProxy((Fm2ValueReceiverTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IFm2ValueReceiverProxy), typeof(Fm2ValueReceiverTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IFm2ValueReceiverProxy), typeof(Fm2ValueReceiverTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var target = new Fm2ValueReceiverTarget { Offset = 4 };
            var dynamicProxy = dynamicResult.CreateInstance<IFm2ValueReceiverProxy>(target);
            var aotProxy = aotResult.CreateInstance<IFm2ValueReceiverProxy>(target);

            aotProxy.Increment(6).Should().Be(dynamicProxy.Increment(6), $"scenario {scenarioId} should preserve value-type receiver method behavior");
        }

        [Fact]
        public void DifferentialParityFM3MethodStaticShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "FM-3";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IFm3StaticMethodProxy),
                typeof(Fm3StaticMethodTarget),
                typeof(Fm3StaticMethodAotProxy),
                instance => new Fm3StaticMethodAotProxy((Fm3StaticMethodTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IFm3StaticMethodProxy), typeof(Fm3StaticMethodTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IFm3StaticMethodProxy), typeof(Fm3StaticMethodTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IFm3StaticMethodProxy>(new Fm3StaticMethodTarget());
            var aotProxy = aotResult.CreateInstance<IFm3StaticMethodProxy>(new Fm3StaticMethodTarget());

            aotProxy.Multiply(7, 8).Should().Be(dynamicProxy.Multiply(7, 8), $"scenario {scenarioId} should preserve static method behavior");
        }

        [Fact]
        public void DifferentialParityFM4MethodNonPublicShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "FM-4";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IFm4NonPublicMethodProxy),
                typeof(Fm4NonPublicMethodTarget),
                typeof(Fm4NonPublicMethodAotProxy),
                instance => new Fm4NonPublicMethodAotProxy((Fm4NonPublicMethodTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IFm4NonPublicMethodProxy), typeof(Fm4NonPublicMethodTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IFm4NonPublicMethodProxy), typeof(Fm4NonPublicMethodTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IFm4NonPublicMethodProxy>(new Fm4NonPublicMethodTarget());
            var aotProxy = aotResult.CreateInstance<IFm4NonPublicMethodProxy>(new Fm4NonPublicMethodTarget());

            aotProxy.Add(9, 11).Should().Be(dynamicProxy.Add(9, 11), $"scenario {scenarioId} should preserve non-public method behavior");
        }

        [Fact]
        public void DifferentialParityFM5MethodGenericShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "FM-5";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IFm5GenericMethodProxy),
                typeof(Fm5GenericMethodTarget),
                typeof(Fm5GenericMethodAotProxy),
                instance => new Fm5GenericMethodAotProxy((Fm5GenericMethodTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IFm5GenericMethodProxy), typeof(Fm5GenericMethodTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IFm5GenericMethodProxy), typeof(Fm5GenericMethodTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IFm5GenericMethodProxy>(new Fm5GenericMethodTarget());
            var aotProxy = aotResult.CreateInstance<IFm5GenericMethodProxy>(new Fm5GenericMethodTarget());

            aotProxy.Echo("fm5").Should().Be(dynamicProxy.Echo("fm5"), $"scenario {scenarioId} should preserve generic method behavior for reference types");
            aotProxy.Echo(55).Should().Be(dynamicProxy.Echo(55), $"scenario {scenarioId} should preserve generic method behavior for value types");
        }

        [Fact]
        public void DifferentialParityFM6MethodFallbackShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "FM-6";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IFm6FallbackMethodProxy),
                typeof(Fm6FallbackMethodTarget),
                typeof(Fm6FallbackMethodAotProxy),
                instance => new Fm6FallbackMethodAotProxy((Fm6FallbackMethodTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IFm6FallbackMethodProxy), typeof(Fm6FallbackMethodTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IFm6FallbackMethodProxy), typeof(Fm6FallbackMethodTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IFm6FallbackMethodProxy>(new Fm6FallbackMethodTarget());
            var aotProxy = aotResult.CreateInstance<IFm6FallbackMethodProxy>(new Fm6FallbackMethodTarget());

            aotProxy.Compute(6).Should().Be(dynamicProxy.Compute(6), $"scenario {scenarioId} should preserve fallback method behavior");
        }

        [Fact]
        public void DifferentialParityFM7MethodRefOutMismatchShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "FM-7";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IFm7RefOutMismatchProxy),
                typeof(Fm7RefOutMismatchTarget),
                typeof(Fm7RefOutMismatchAotProxy),
                instance => new Fm7RefOutMismatchAotProxy((Fm7RefOutMismatchTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IFm7RefOutMismatchProxy), typeof(Fm7RefOutMismatchTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IFm7RefOutMismatchProxy), typeof(Fm7RefOutMismatchTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IFm7RefOutMismatchProxy>(new Fm7RefOutMismatchTarget());
            var aotProxy = aotResult.CreateInstance<IFm7RefOutMismatchProxy>(new Fm7RefOutMismatchTarget());

            object dynamicValue = -7;
            object aotValue = -7;
            dynamicProxy.Normalize(ref dynamicValue, out var dynamicDoubled);
            aotProxy.Normalize(ref aotValue, out var aotDoubled);

            aotValue.Should().Be(dynamicValue, $"scenario {scenarioId} should preserve ref conversion behavior");
            aotDoubled.Should().Be(dynamicDoubled, $"scenario {scenarioId} should preserve out conversion behavior");
        }

        [Fact]
        public void DifferentialParityRT4ReturnDuckChainShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "RT-4";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IRt4DuckChainInnerProxy),
                typeof(Rt4DuckChainInnerTarget),
                typeof(Rt4DuckChainInnerAotProxy),
                instance => new Rt4DuckChainInnerAotProxy((Rt4DuckChainInnerTarget)instance!));
            DuckTypeAotEngine.RegisterProxy(
                typeof(IRt4DuckChainReturnProxy),
                typeof(Rt4DuckChainReturnTarget),
                typeof(Rt4DuckChainReturnAotProxy),
                instance => new Rt4DuckChainReturnAotProxy((Rt4DuckChainReturnTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IRt4DuckChainReturnProxy), typeof(Rt4DuckChainReturnTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IRt4DuckChainReturnProxy), typeof(Rt4DuckChainReturnTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IRt4DuckChainReturnProxy>(new Rt4DuckChainReturnTarget(new Rt4DuckChainInnerTarget(42)));
            var aotProxy = aotResult.CreateInstance<IRt4DuckChainReturnProxy>(new Rt4DuckChainReturnTarget(new Rt4DuckChainInnerTarget(42)));

            aotProxy.GetInner().Number.Should().Be(dynamicProxy.GetInner().Number, $"scenario {scenarioId} should preserve duck-chain return behavior");
        }

        [Fact]
        public void DifferentialParityRT5ReturnValueWithTypeShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "RT-5";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IRt5ValueWithTypeReturnProxy),
                typeof(Rt5ValueWithTypeReturnTarget),
                typeof(Rt5ValueWithTypeReturnAotProxy),
                instance => new Rt5ValueWithTypeReturnAotProxy((Rt5ValueWithTypeReturnTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IRt5ValueWithTypeReturnProxy), typeof(Rt5ValueWithTypeReturnTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IRt5ValueWithTypeReturnProxy), typeof(Rt5ValueWithTypeReturnTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IRt5ValueWithTypeReturnProxy>(new Rt5ValueWithTypeReturnTarget());
            var aotProxy = aotResult.CreateInstance<IRt5ValueWithTypeReturnProxy>(new Rt5ValueWithTypeReturnTarget());

            aotProxy.GetCount().Value.Should().Be(dynamicProxy.GetCount().Value, $"scenario {scenarioId} should preserve ValueWithType return value");
            aotProxy.GetCount().Type.Should().Be(dynamicProxy.GetCount().Type, $"scenario {scenarioId} should preserve ValueWithType return metadata");
        }

        private static DuckType.CreateTypeResult InvokeDynamicForward(Type proxyDefinitionType, Type targetType)
        {
            if (DynamicForwardFactory is null)
            {
                throw new InvalidOperationException("Unable to resolve DuckType.GetOrCreateDynamicProxyType(Type, Type) for differential parity tests.");
            }

            var result = DynamicForwardFactory.Invoke(null, new object[] { proxyDefinitionType, targetType });
            if (result is DuckType.CreateTypeResult createTypeResult)
            {
                return createTypeResult;
            }

            throw new InvalidOperationException("Dynamic forward factory did not return DuckType.CreateTypeResult.");
        }

        private static DuckType.CreateTypeResult InvokeDynamicReverse(Type typeToDeriveFrom, Type delegationType)
        {
            if (DynamicReverseFactory is null)
            {
                throw new InvalidOperationException("Unable to resolve DuckType.GetOrCreateDynamicReverseProxyType(Type, Type) for differential parity tests.");
            }

            var result = DynamicReverseFactory.Invoke(null, new object[] { typeToDeriveFrom, delegationType });
            if (result is DuckType.CreateTypeResult createTypeResult)
            {
                return createTypeResult;
            }

            throw new InvalidOperationException("Dynamic reverse factory did not return DuckType.CreateTypeResult.");
        }

        private static void AssertCannotCreate(string scenarioId, DuckType.CreateTypeResult dynamicResult, DuckType.CreateTypeResult aotResult)
        {
            dynamicResult.CanCreate().Should().BeFalse($"scenario {scenarioId} must be non-creatable in dynamic mode");
            aotResult.CanCreate().Should().BeFalse($"scenario {scenarioId} must be non-creatable in AOT mode");
        }

        private interface IForwardValueProxy
        {
            string Value { get; }
        }

        private class ForwardValueTarget
        {
            public ForwardValueTarget(string value)
            {
                Value = value;
            }

            public string Value { get; }
        }

        private class ForwardValueAotProxy : IForwardValueProxy
        {
            private readonly ForwardValueTarget _target;

            public ForwardValueAotProxy(ForwardValueTarget target)
            {
                _target = target;
            }

            public string Value => _target.Value;
        }

        private interface IForwardMathProxy
        {
            int Add(int left, int right);
        }

        private class ForwardMathTarget
        {
            public int Add(int left, int right)
            {
                return left + right;
            }
        }

        private class ForwardMathAotProxy : IForwardMathProxy
        {
            private readonly ForwardMathTarget _target;

            public ForwardMathAotProxy(ForwardMathTarget target)
            {
                _target = target;
            }

            public int Add(int left, int right)
            {
                return _target.Add(left, right);
            }
        }

        private interface IForwardMutableValueProxy
        {
            string Value { get; set; }
        }

        private class ForwardMutableValueTarget
        {
            public string Value { get; set; } = string.Empty;
        }

        private class ForwardMutableValueAotProxy : IForwardMutableValueProxy
        {
            private readonly ForwardMutableValueTarget _target;

            public ForwardMutableValueAotProxy(ForwardMutableValueTarget target)
            {
                _target = target;
            }

            public string Value
            {
                get => _target.Value;
                set => _target.Value = value;
            }
        }

        private interface IRefOutMathProxy
        {
            void Normalize(ref int value, out int doubled);
        }

        private class RefOutMathTarget
        {
            public void Normalize(ref int value, out int doubled)
            {
                value = Math.Abs(value);
                doubled = value * 2;
            }
        }

        private class RefOutMathAotProxy : IRefOutMathProxy
        {
            private readonly RefOutMathTarget _target;

            public RefOutMathAotProxy(RefOutMathTarget target)
            {
                _target = target;
            }

            public void Normalize(ref int value, out int doubled)
            {
                _target.Normalize(ref value, out doubled);
            }
        }

        private interface IAliasMethodProxy
        {
            [Duck(Name = "ComputeSum")]
            int Add(int left, int right);
        }

        private class AliasMethodTarget
        {
            public int ComputeSum(int left, int right)
            {
                return left + right;
            }
        }

        private class AliasMethodAotProxy : IAliasMethodProxy
        {
            private readonly AliasMethodTarget _target;

            public AliasMethodAotProxy(AliasMethodTarget target)
            {
                _target = target;
            }

            public int Add(int left, int right)
            {
                return _target.ComputeSum(left, right);
            }
        }

        private interface INullableValueProxy
        {
            int? Maybe(int value);
        }

        private class NullableValueTarget
        {
            public int? Maybe(int value)
            {
                return value >= 0 ? value : null;
            }
        }

        private class NullableValueAotProxy : INullableValueProxy
        {
            private readonly NullableValueTarget _target;

            public NullableValueAotProxy(NullableValueTarget target)
            {
                _target = target;
            }

            public int? Maybe(int value)
            {
                return _target.Maybe(value);
            }
        }

        private interface IGenericEchoProxy
        {
            T Echo<T>(T value);
        }

        private class GenericEchoTarget
        {
            public T Echo<T>(T value)
            {
                return value;
            }
        }

        private class GenericEchoAotProxy : IGenericEchoProxy
        {
            private readonly GenericEchoTarget _target;

            public GenericEchoAotProxy(GenericEchoTarget target)
            {
                _target = target;
            }

            public T Echo<T>(T value)
            {
                return _target.Echo(value);
            }
        }

        private interface IStaticMathProxy
        {
            int Triple(int value);
        }

        private class StaticMathTarget
        {
            public static int Triple(int value)
            {
                return value * 3;
            }
        }

        private class StaticMathAotProxy : IStaticMathProxy
        {
            public StaticMathAotProxy(StaticMathTarget target)
            {
                _ = target;
            }

            public int Triple(int value)
            {
                return StaticMathTarget.Triple(value);
            }
        }

        private interface IOverloadIntProxy
        {
            [Duck(Name = "Compute", ParameterTypeNames = new[] { "System.Int32" })]
            int ComputeInt(int value);
        }

        private class OverloadTarget
        {
            public int Compute(int value)
            {
                return value + 10;
            }

            public string Compute(string value)
            {
                return value + value;
            }
        }

        private class OverloadIntAotProxy : IOverloadIntProxy
        {
            private readonly OverloadTarget _target;

            public OverloadIntAotProxy(OverloadTarget target)
            {
                _target = target;
            }

            public int ComputeInt(int value)
            {
                return _target.Compute(value);
            }
        }

        private interface IVisibilityAccessProxy
        {
            [DuckField(Name = "PublicCount")]
            int PublicCount { get; set; }

            [DuckField(Name = "_internalCount")]
            int InternalCount { get; set; }

            [DuckField(Name = "_privateCount")]
            int PrivateCount { get; set; }
        }

        private class VisibilityAccessTarget
        {
            public int PublicCount;
            internal int _internalCount;
            private int _privateCount = 0;

            public int ReadPrivate()
            {
                return _privateCount;
            }
        }

        private class VisibilityAccessAotProxy : IVisibilityAccessProxy
        {
            private static readonly FieldInfo PrivateCountField = GetPrivateCountField();
            private readonly VisibilityAccessTarget _target;

            public VisibilityAccessAotProxy(VisibilityAccessTarget target)
            {
                _target = target;
            }

            public int PublicCount
            {
                get => _target.PublicCount;
                set => _target.PublicCount = value;
            }

            public int InternalCount
            {
                get => _target._internalCount;
                set => _target._internalCount = value;
            }

            public int PrivateCount
            {
                get => (int)PrivateCountField.GetValue(_target)!;
                set => PrivateCountField.SetValue(_target, value);
            }

            private static FieldInfo GetPrivateCountField()
            {
                return typeof(VisibilityAccessTarget).GetField("_privateCount", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("Unable to resolve private field '_privateCount' for A-10 parity proxy.");
            }
        }

        private abstract class IgnoreFeatureProxyBase
        {
            public abstract string Value { get; }

            [DuckIgnore]
            public string LocalValue()
            {
                return Value + "-local";
            }
        }

        private class IgnoreFeatureTarget
        {
            public string Value => "ignored";

            public string LocalValue()
            {
                return "target-local";
            }
        }

        private class IgnoreFeatureAotProxy : IgnoreFeatureProxyBase
        {
            private readonly IgnoreFeatureTarget _target;

            public IgnoreFeatureAotProxy(IgnoreFeatureTarget target)
            {
                _target = target;
            }

            public override string Value => _target.Value;
        }

        private interface IEmptyIncludeProxy
        {
        }

        private class IncludeHashTarget
        {
            [DuckInclude]
            public override int GetHashCode()
            {
                return 4242;
            }
        }

        private class IncludeHashAotProxy : IEmptyIncludeProxy
        {
            private readonly IncludeHashTarget _target;

            public IncludeHashAotProxy(IncludeHashTarget target)
            {
                _target = target;
            }

            public override int GetHashCode()
            {
                return _target.GetHashCode();
            }
        }

        private interface IEmptyNoIncludeProxy
        {
        }

        private class NoIncludeHashTarget
        {
            public override int GetHashCode()
            {
                return 2424;
            }
        }

        private class NoIncludeHashAotProxy : IEmptyNoIncludeProxy
        {
            public NoIncludeHashAotProxy(NoIncludeHashTarget target)
            {
                _ = target;
            }
        }

        private interface IEmptyToStringProxy
        {
        }

        private class ToStringTarget
        {
            public override string ToString()
            {
                return "target-to-string";
            }
        }

        private class ToStringAotProxy : IEmptyToStringProxy
        {
            private readonly ToStringTarget _target;

            public ToStringAotProxy(ToStringTarget target)
            {
                _target = target;
            }

            public override string ToString()
            {
                return _target.ToString();
            }
        }

        private interface IStructMutationGuardProxy
        {
            int Count { get; set; }
        }

        private struct StructMutationGuardTarget
        {
            public int Count { get; set; }
        }

        private interface IFieldCountProxy
        {
            [DuckPropertyOrField]
            int Count { get; set; }
        }

        private class FieldCountTarget
        {
            public int Count;
        }

        private class FieldCountAotProxy : IFieldCountProxy
        {
            private readonly FieldCountTarget _target;

            public FieldCountAotProxy(FieldCountTarget target)
            {
                _target = target;
            }

            public int Count
            {
                get => _target.Count;
                set => _target.Count = value;
            }
        }

        private interface IDuckFieldCountProxy
        {
            [DuckField(Name = "CountField")]
            int Count { get; set; }
        }

        private class DuckFieldCountTarget
        {
            public int CountField;
        }

        private class DuckFieldCountAotProxy : IDuckFieldCountProxy
        {
            private readonly DuckFieldCountTarget _target;

            public DuckFieldCountAotProxy(DuckFieldCountTarget target)
            {
                _target = target;
            }

            public int Count
            {
                get => _target.CountField;
                set => _target.CountField = value;
            }
        }

        private interface IOptionalParameterProxy
        {
            [Duck(Name = "Add")]
            int AddWithDefault(int left, int right = 6);
        }

        private class OptionalParameterTarget
        {
            public int Add(int left, int right = 6)
            {
                return left + right;
            }
        }

        private class OptionalParameterAotProxy : IOptionalParameterProxy
        {
            private readonly OptionalParameterTarget _target;

            public OptionalParameterAotProxy(OptionalParameterTarget target)
            {
                _target = target;
            }

            public int AddWithDefault(int left, int right = 6)
            {
                return _target.Add(left, right);
            }
        }

        private interface IRefOutConversionProxy
        {
            void Normalize(ref int value, out int doubled);
        }

        private class RefOutConversionTarget
        {
            public void Normalize(ref int value, out int doubled)
            {
                value = Math.Abs(value);
                doubled = value * 2;
            }
        }

        private class RefOutConversionAotProxy : IRefOutConversionProxy
        {
            private readonly RefOutConversionTarget _target;

            public RefOutConversionAotProxy(RefOutConversionTarget target)
            {
                _target = target;
            }

            public void Normalize(ref int value, out int doubled)
            {
                _target.Normalize(ref value, out doubled);
            }
        }

        private interface IParameterTypeNamesProxy
        {
            [Duck(Name = "Compute", ParameterTypeNames = new[] { "System.Int64" })]
            long ComputeLong(long value);
        }

        private class ParameterTypeNamesTarget
        {
            public int Compute(int value)
            {
                return value + 1;
            }

            public long Compute(long value)
            {
                return value + 2;
            }
        }

        private class ParameterTypeNamesAotProxy : IParameterTypeNamesProxy
        {
            private readonly ParameterTypeNamesTarget _target;

            public ParameterTypeNamesAotProxy(ParameterTypeNamesTarget target)
            {
                _target = target;
            }

            public long ComputeLong(long value)
            {
                return _target.Compute(value);
            }
        }

        private interface IGenericWrapProxy
        {
            Tuple<TLeft, TRight> Wrap<TLeft, TRight>(TLeft left, TRight right);
        }

        private class GenericWrapTarget
        {
            public Tuple<TLeft, TRight> Wrap<TLeft, TRight>(TLeft left, TRight right)
            {
                return Tuple.Create(left, right);
            }
        }

        private class GenericWrapAotProxy : IGenericWrapProxy
        {
            private readonly GenericWrapTarget _target;

            public GenericWrapAotProxy(GenericWrapTarget target)
            {
                _target = target;
            }

            public Tuple<TLeft, TRight> Wrap<TLeft, TRight>(TLeft left, TRight right)
            {
                return _target.Wrap(left, right);
            }
        }

        private interface INonPublicGenericBindingProxy
        {
            [Duck(Name = "GetDefault", GenericParameterTypeNames = new[] { "System.Int32" })]
            int GetDefaultInt();

            [Duck(Name = "GetDefault", GenericParameterTypeNames = new[] { "System.String" })]
            string GetDefaultString();
        }

        private class NonPublicGenericBindingTarget
        {
            internal T GetDefault<T>()
            {
                if (typeof(T) == typeof(string))
                {
                    return (T)(object)"default-string";
                }

                if (typeof(T) == typeof(int))
                {
                    return (T)(object)17;
                }

                return default!;
            }
        }

        private class NonPublicGenericBindingAotProxy : INonPublicGenericBindingProxy
        {
            private readonly NonPublicGenericBindingTarget _target;

            public NonPublicGenericBindingAotProxy(NonPublicGenericBindingTarget target)
            {
                _target = target;
            }

            public int GetDefaultInt()
            {
                return _target.GetDefault<int>();
            }

            public string GetDefaultString()
            {
                return _target.GetDefault<string>();
            }
        }

        private interface IGenericNonPublicMethodProxy
        {
            T GetDefault<T>();
        }

        private class GenericNonPublicMethodTarget
        {
            internal T GetDefault<T>()
            {
                return default!;
            }
        }

        private class GenericNonPublicMethodAotProxy : IGenericNonPublicMethodProxy
        {
            private readonly GenericNonPublicMethodTarget _target;

            public GenericNonPublicMethodAotProxy(GenericNonPublicMethodTarget target)
            {
                _target = target;
            }

            public T GetDefault<T>()
            {
                return _target.GetDefault<T>();
            }
        }

        private interface IExplicitMathProxy
        {
            [Duck(Name = "Compute", ExplicitInterfaceTypeName = "*")]
            int Compute(int value);
        }

        private interface IExplicitMath
        {
            int Compute(int value);
        }

        private class ExplicitMathTarget : IExplicitMath
        {
            int IExplicitMath.Compute(int value)
            {
                return value + 100;
            }
        }

        private class ExplicitMathAotProxy : IExplicitMathProxy
        {
            private readonly ExplicitMathTarget _target;

            public ExplicitMathAotProxy(ExplicitMathTarget target)
            {
                _target = target;
            }

            public int Compute(int value)
            {
                return ((IExplicitMath)_target).Compute(value);
            }
        }

        private interface IAmbiguousMethodProxy
        {
            void Add(string key, object value);

            void Add(string key, string value);
        }

        private class AmbiguousMethodTarget
        {
            public void Add(string key, int value)
            {
                _ = key;
                _ = value;
            }

            public void Add(string key, string value)
            {
                _ = key;
                _ = value;
            }
        }

        private interface IPrivateFieldProxy
        {
            [DuckField(Name = "_count")]
            int Count { get; set; }
        }

        private class PrivateFieldTarget
        {
            private int _count = 0;

            public int CountForDiagnostics => _count;
        }

        private class PrivateFieldAotProxy : IPrivateFieldProxy
        {
            private static readonly FieldInfo CountField = GetCountField();
            private readonly PrivateFieldTarget _target;

            public PrivateFieldAotProxy(PrivateFieldTarget target)
            {
                _target = target;
            }

            public int Count
            {
                get => (int)CountField.GetValue(_target)!;
                set => CountField.SetValue(_target, value);
            }

            private static FieldInfo GetCountField()
            {
                return typeof(PrivateFieldTarget).GetField("_count", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("Unable to resolve private field '_count' for B-18 parity proxy.");
            }
        }

        private interface IChainInnerProxy
        {
            string Name { get; }
        }

        private class ChainInnerTarget
        {
            public ChainInnerTarget(string name)
            {
                Name = name;
            }

            public string Name { get; }
        }

        private class ChainInnerAotProxy : IChainInnerProxy
        {
            private readonly ChainInnerTarget _target;

            public ChainInnerAotProxy(ChainInnerTarget target)
            {
                _target = target;
            }

            public string Name => _target.Name;
        }

        private interface IChainOuterProxy
        {
            IChainInnerProxy Inner { get; }
        }

        private class ChainOuterTarget
        {
            public ChainOuterTarget(ChainInnerTarget inner)
            {
                Inner = inner;
            }

            public ChainInnerTarget Inner { get; }
        }

        private class ChainOuterAotProxy : IChainOuterProxy
        {
            private readonly ChainOuterTarget _target;

            public ChainOuterAotProxy(ChainOuterTarget target)
            {
                _target = target;
            }

            public IChainInnerProxy Inner => new ChainInnerAotProxy(_target.Inner);
        }

        private interface IChainNullableOuterProxy
        {
            IChainInnerProxy? Inner { get; }
        }

        private class ChainNullableOuterTarget
        {
            public ChainNullableOuterTarget(ChainInnerTarget? inner)
            {
                Inner = inner;
            }

            public ChainInnerTarget? Inner { get; }
        }

        private class ChainNullableOuterAotProxy : IChainNullableOuterProxy
        {
            private readonly ChainNullableOuterTarget _target;

            public ChainNullableOuterAotProxy(ChainNullableOuterTarget target)
            {
                _target = target;
            }

            public IChainInnerProxy? Inner => _target.Inner is null ? null : new ChainInnerAotProxy(_target.Inner);
        }

        private interface IChainMethodOuterProxy
        {
            IChainInnerProxy GetInner();
        }

        private class ChainMethodOuterTarget
        {
            private readonly ChainInnerTarget _inner;

            public ChainMethodOuterTarget(ChainInnerTarget inner)
            {
                _inner = inner;
            }

            public ChainInnerTarget GetInner()
            {
                return _inner;
            }
        }

        private class ChainMethodOuterAotProxy : IChainMethodOuterProxy
        {
            private readonly ChainMethodOuterTarget _target;

            public ChainMethodOuterAotProxy(ChainMethodOuterTarget target)
            {
                _target = target;
            }

            public IChainInnerProxy GetInner()
            {
                return new ChainInnerAotProxy(_target.GetInner());
            }
        }

        [DuckCopy]
        private struct NullableInnerDuckCopy
        {
            public int Number;
        }

        private interface INullableDuckChainProxy
        {
            [Duck(Name = "TryGetInner")]
            NullableInnerDuckCopy? TryGetInner(bool hasValue);
        }

        private class NullableDuckChainTarget
        {
            public object? TryGetInner(bool hasValue)
            {
                return hasValue ? new NullableDuckChainInnerTarget(23) : null;
            }
        }

        private class NullableDuckChainInnerTarget
        {
            public NullableDuckChainInnerTarget(int number)
            {
                Number = number;
            }

            public int Number { get; }
        }

        private class NullableDuckChainAotProxy : INullableDuckChainProxy
        {
            private readonly NullableDuckChainTarget _target;

            public NullableDuckChainAotProxy(NullableDuckChainTarget target)
            {
                _target = target;
            }

            public NullableInnerDuckCopy? TryGetInner(bool hasValue)
            {
                var instance = _target.TryGetInner(hasValue) as NullableDuckChainInnerTarget;
                if (instance is null)
                {
                    return default;
                }

                return new NullableInnerDuckCopy { Number = instance.Number };
            }
        }

        private interface IValueWithTypeProxy
        {
            [Duck(Name = "Count")]
            ValueWithType<int> Count { get; set; }
        }

        private class ValueWithTypeTarget
        {
            public int Count { get; set; } = 12;
        }

        private class ValueWithTypeAotProxy : IValueWithTypeProxy
        {
            private readonly ValueWithTypeTarget _target;

            public ValueWithTypeAotProxy(ValueWithTypeTarget target)
            {
                _target = target;
            }

            public ValueWithType<int> Count
            {
                get => ValueWithType<int>.Create(_target.Count, typeof(int));
                set => _target.Count = value.Value;
            }
        }

        private interface IEnumConversionProxy
        {
            EnumConversionValue Echo(EnumConversionValue value);
        }

        private enum EnumConversionValue
        {
            Zero = 0,
            One = 1,
            Two = 2,
        }

        private class EnumConversionTarget
        {
            public int Echo(int value)
            {
                return value;
            }
        }

        private class EnumConversionAotProxy : IEnumConversionProxy
        {
            private readonly EnumConversionTarget _target;

            public EnumConversionAotProxy(EnumConversionTarget target)
            {
                _target = target;
            }

            public EnumConversionValue Echo(EnumConversionValue value)
            {
                return (EnumConversionValue)_target.Echo((int)value);
            }
        }

        private interface IFg1GetterProxy
        {
            string Name { get; }
        }

        private class Fg1GetterTarget
        {
            public Fg1GetterTarget(string name)
            {
                Name = name;
            }

            public string Name { get; }
        }

        private class Fg1GetterAotProxy : IFg1GetterProxy
        {
            private readonly Fg1GetterTarget _target;

            public Fg1GetterAotProxy(Fg1GetterTarget target)
            {
                _target = target;
            }

            public string Name => _target.Name;
        }

        private interface IFs1SetterProxy
        {
            int Count { get; set; }
        }

        private class Fs1SetterTarget
        {
            public int Count { get; set; }
        }

        private class Fs1SetterAotProxy : IFs1SetterProxy
        {
            private readonly Fs1SetterTarget _target;

            public Fs1SetterAotProxy(Fs1SetterTarget target)
            {
                _target = target;
            }

            public int Count
            {
                get => _target.Count;
                set => _target.Count = value;
            }
        }

        private interface IFf1FieldProxy
        {
            [DuckField(Name = "_value")]
            int Value { get; set; }
        }

        private class Ff1FieldTarget
        {
            private int _value = 7;

            public int Peek()
            {
                return _value;
            }
        }

        private class Ff1FieldAotProxy : IFf1FieldProxy
        {
            private static readonly FieldInfo ValueField = GetValueField();
            private readonly Ff1FieldTarget _target;

            public Ff1FieldAotProxy(Ff1FieldTarget target)
            {
                _target = target;
            }

            public int Value
            {
                get
                {
                    if (ValueField.GetValue(_target) is int value)
                    {
                        return value;
                    }

                    return default;
                }

                set => ValueField.SetValue(_target, value);
            }

            private static FieldInfo GetValueField()
            {
                return typeof(Ff1FieldTarget).GetField("_value", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("Unable to resolve _value field.");
            }
        }

        private interface IFm1MethodProxy
        {
            string Join(string right);
        }

        private class Fm1MethodTarget
        {
            private readonly string _left;

            public Fm1MethodTarget(string left)
            {
                _left = left;
            }

            public string Join(string right)
            {
                return _left + ":" + right;
            }
        }

        private class Fm1MethodAotProxy : IFm1MethodProxy
        {
            private readonly Fm1MethodTarget _target;

            public Fm1MethodAotProxy(Fm1MethodTarget target)
            {
                _target = target;
            }

            public string Join(string right)
            {
                return _target.Join(right);
            }
        }

        private interface IRt1VoidProxy
        {
            void Touch(int delta);

            int Read();
        }

        private class Rt1VoidTarget
        {
            private int _value;

            public void Touch(int delta)
            {
                _value += delta;
            }

            public int Read()
            {
                return _value;
            }
        }

        private class Rt1VoidAotProxy : IRt1VoidProxy
        {
            private readonly Rt1VoidTarget _target;

            public Rt1VoidAotProxy(Rt1VoidTarget target)
            {
                _target = target;
            }

            public void Touch(int delta)
            {
                _target.Touch(delta);
            }

            public int Read()
            {
                return _target.Read();
            }
        }

        private interface IFg2GetterProxy
        {
            int Count { get; }
        }

        private class Fg2GetterTarget
        {
            public Fg2GetterTarget(int count)
            {
                Count = count;
            }

            public int Count { get; }
        }

        private class Fg2GetterAotProxy : IFg2GetterProxy
        {
            private readonly Fg2GetterTarget _target;

            public Fg2GetterAotProxy(Fg2GetterTarget target)
            {
                _target = target;
            }

            public int Count => _target.Count;
        }

        private interface IFs5StaticSetterProxy
        {
            [Duck(Name = "GlobalCount")]
            int Count { get; set; }
        }

        private class Fs5StaticSetterTarget
        {
            public static int GlobalCount { get; set; }
        }

        private class Fs5StaticSetterAotProxy : IFs5StaticSetterProxy
        {
            public Fs5StaticSetterAotProxy(Fs5StaticSetterTarget target)
            {
                _ = target;
            }

            public int Count
            {
                get => Fs5StaticSetterTarget.GlobalCount;
                set => Fs5StaticSetterTarget.GlobalCount = value;
            }
        }

        private interface IFf2StaticFieldProxy
        {
            [DuckField(Name = "GlobalValue")]
            int Value { get; set; }
        }

        private class Ff2StaticFieldTarget
        {
            public static int GlobalValue = 3;
        }

        private class Ff2StaticFieldAotProxy : IFf2StaticFieldProxy
        {
            public Ff2StaticFieldAotProxy(Ff2StaticFieldTarget target)
            {
                _ = target;
            }

            public int Value
            {
                get => Ff2StaticFieldTarget.GlobalValue;
                set => Ff2StaticFieldTarget.GlobalValue = value;
            }
        }

        private interface IFm8OptionalProxy
        {
            int AddWithDefault(int left, int right = 7);
        }

        private class Fm8OptionalTarget
        {
            public int AddWithDefault(int left, int right = 7)
            {
                return left + right;
            }
        }

        private class Fm8OptionalAotProxy : IFm8OptionalProxy
        {
            private readonly Fm8OptionalTarget _target;

            public Fm8OptionalAotProxy(Fm8OptionalTarget target)
            {
                _target = target;
            }

            public int AddWithDefault(int left, int right = 7)
            {
                return _target.AddWithDefault(left, right);
            }
        }

        private interface IRt3ReturnConversionProxy
        {
            object GetCount();
        }

        private class Rt3ReturnConversionTarget
        {
            public int GetCount()
            {
                return 31;
            }
        }

        private class Rt3ReturnConversionAotProxy : IRt3ReturnConversionProxy
        {
            private readonly Rt3ReturnConversionTarget _target;

            public Rt3ReturnConversionAotProxy(Rt3ReturnConversionTarget target)
            {
                _target = target;
            }

            public object GetCount()
            {
                return _target.GetCount();
            }
        }

        private interface IFg3NonPublicGetterProxy
        {
            [Duck(Name = "Secret")]
            int Secret { get; }
        }

        private class Fg3NonPublicGetterTarget
        {
            private readonly int _secret;

            public Fg3NonPublicGetterTarget(int secret)
            {
                _secret = secret;
            }

            private int Secret => _secret;
        }

        private class Fg3NonPublicGetterAotProxy : IFg3NonPublicGetterProxy
        {
            private static readonly PropertyInfo SecretProperty = GetSecretProperty();
            private readonly Fg3NonPublicGetterTarget _target;

            public Fg3NonPublicGetterAotProxy(Fg3NonPublicGetterTarget target)
            {
                _target = target;
            }

            public int Secret
            {
                get
                {
                    if (SecretProperty.GetValue(_target) is int value)
                    {
                        return value;
                    }

                    return default;
                }
            }

            private static PropertyInfo GetSecretProperty()
            {
                return typeof(Fg3NonPublicGetterTarget).GetProperty("Secret", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("Unable to resolve non-public property Secret.");
            }
        }

        private interface IFg4StaticGetterProxy
        {
            [Duck(Name = "Global")]
            int Global { get; }
        }

        private class Fg4StaticGetterTarget
        {
            public static int Global { get; set; }
        }

        private class Fg4StaticGetterAotProxy : IFg4StaticGetterProxy
        {
            public Fg4StaticGetterAotProxy(Fg4StaticGetterTarget target)
            {
                _ = target;
            }

            public int Global => Fg4StaticGetterTarget.Global;
        }

        private interface IFg5ValueWithTypeGetterProxy
        {
            [Duck(Name = "Count")]
            ValueWithType<int> Count { get; }
        }

        private class Fg5ValueWithTypeGetterTarget
        {
            public Fg5ValueWithTypeGetterTarget(int count)
            {
                Count = count;
            }

            public int Count { get; }
        }

        private class Fg5ValueWithTypeGetterAotProxy : IFg5ValueWithTypeGetterProxy
        {
            private readonly Fg5ValueWithTypeGetterTarget _target;

            public Fg5ValueWithTypeGetterAotProxy(Fg5ValueWithTypeGetterTarget target)
            {
                _target = target;
            }

            public ValueWithType<int> Count => ValueWithType<int>.Create(_target.Count, typeof(int));
        }

        private interface IFg6ChainInnerProxy
        {
            string Name { get; }
        }

        private class Fg6ChainInnerTarget
        {
            public Fg6ChainInnerTarget(string name)
            {
                Name = name;
            }

            public string Name { get; }
        }

        private class Fg6ChainInnerAotProxy : IFg6ChainInnerProxy
        {
            private readonly Fg6ChainInnerTarget _target;

            public Fg6ChainInnerAotProxy(Fg6ChainInnerTarget target)
            {
                _target = target;
            }

            public string Name => _target.Name;
        }

        private interface IFg6ChainOuterProxy
        {
            [Duck(Name = "Inner")]
            IFg6ChainInnerProxy Inner { get; }
        }

        private class Fg6ChainOuterTarget
        {
            public Fg6ChainOuterTarget(Fg6ChainInnerTarget inner)
            {
                Inner = inner;
            }

            public Fg6ChainInnerTarget Inner { get; }
        }

        private class Fg6ChainOuterAotProxy : IFg6ChainOuterProxy
        {
            private readonly Fg6ChainOuterTarget _target;

            public Fg6ChainOuterAotProxy(Fg6ChainOuterTarget target)
            {
                _target = target;
            }

            public IFg6ChainInnerProxy Inner => new Fg6ChainInnerAotProxy(_target.Inner);
        }

        private interface IFg9FallbackGetterProxy
        {
            [Duck(Name = "Hidden")]
            int Hidden { get; }
        }

        private class Fg9FallbackGetterTarget
        {
            private readonly int _hidden;

            public Fg9FallbackGetterTarget(int hidden)
            {
                _hidden = hidden;
            }

            private int Hidden => _hidden;
        }

        private class Fg9FallbackGetterAotProxy : IFg9FallbackGetterProxy
        {
            private static readonly PropertyInfo HiddenProperty = GetHiddenProperty();
            private readonly Fg9FallbackGetterTarget _target;

            public Fg9FallbackGetterAotProxy(Fg9FallbackGetterTarget target)
            {
                _target = target;
            }

            public int Hidden
            {
                get
                {
                    if (HiddenProperty.GetValue(_target) is int value)
                    {
                        return value;
                    }

                    return default;
                }
            }

            private static PropertyInfo GetHiddenProperty()
            {
                return typeof(Fg9FallbackGetterTarget).GetProperty("Hidden", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("Unable to resolve fallback property Hidden.");
            }
        }

        private interface IFs2NonPublicSetterProxy
        {
            [Duck(Name = "Hidden")]
            int Hidden { set; }

            [Duck(Name = "Read")]
            int Read();
        }

        private class Fs2NonPublicSetterTarget
        {
            private int Hidden { get; set; }

            public int Read()
            {
                return Hidden;
            }
        }

        private class Fs2NonPublicSetterAotProxy : IFs2NonPublicSetterProxy
        {
            private static readonly PropertyInfo HiddenProperty = GetHiddenProperty();
            private readonly Fs2NonPublicSetterTarget _target;

            public Fs2NonPublicSetterAotProxy(Fs2NonPublicSetterTarget target)
            {
                _target = target;
            }

            public int Hidden
            {
                set => HiddenProperty.SetValue(_target, value);
            }

            public int Read()
            {
                return _target.Read();
            }

            private static PropertyInfo GetHiddenProperty()
            {
                return typeof(Fs2NonPublicSetterTarget).GetProperty("Hidden", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("Unable to resolve non-public setter property Hidden.");
            }
        }

        private interface IFs6FallbackSetterProxy
        {
            [Duck(Name = "Hidden")]
            int Hidden { set; }

            [Duck(Name = "Read")]
            int Read();
        }

        private class Fs6FallbackSetterTarget
        {
            private int Hidden { get; set; }

            public int Read()
            {
                return Hidden;
            }
        }

        private class Fs6FallbackSetterAotProxy : IFs6FallbackSetterProxy
        {
            private static readonly PropertyInfo HiddenProperty = GetHiddenProperty();
            private readonly Fs6FallbackSetterTarget _target;

            public Fs6FallbackSetterAotProxy(Fs6FallbackSetterTarget target)
            {
                _target = target;
            }

            public int Hidden
            {
                set => HiddenProperty.SetValue(_target, value);
            }

            public int Read()
            {
                return _target.Read();
            }

            private static PropertyInfo GetHiddenProperty()
            {
                return typeof(Fs6FallbackSetterTarget).GetProperty("Hidden", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("Unable to resolve fallback setter property Hidden.");
            }
        }

        private interface IFf3InstanceFieldSetProxy
        {
            [DuckField(Name = "_value")]
            int Value { set; }
        }

        private class Ff3InstanceFieldSetTarget
        {
            private int _value = 0;

            public int Read()
            {
                return _value;
            }
        }

        private class Ff3InstanceFieldSetAotProxy : IFf3InstanceFieldSetProxy
        {
            private static readonly FieldInfo ValueField = GetValueField();
            private readonly Ff3InstanceFieldSetTarget _target;

            public Ff3InstanceFieldSetAotProxy(Ff3InstanceFieldSetTarget target)
            {
                _target = target;
            }

            public int Value
            {
                set => ValueField.SetValue(_target, value);
            }

            private static FieldInfo GetValueField()
            {
                return typeof(Ff3InstanceFieldSetTarget).GetField("_value", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("Unable to resolve field _value.");
            }
        }

        private interface IFf4StaticFieldSetProxy
        {
            [DuckField(Name = "_global")]
            int Value { set; }
        }

        private class Ff4StaticFieldSetTarget
        {
            private static int _global;

            public static int Read()
            {
                return _global;
            }

            public static void Reset()
            {
                _global = 0;
            }
        }

        private class Ff4StaticFieldSetAotProxy : IFf4StaticFieldSetProxy
        {
            private static readonly FieldInfo GlobalField = GetGlobalField();

            public Ff4StaticFieldSetAotProxy(Ff4StaticFieldSetTarget target)
            {
                _ = target;
            }

            public int Value
            {
                set => GlobalField.SetValue(null, value);
            }

            private static FieldInfo GetGlobalField()
            {
                return typeof(Ff4StaticFieldSetTarget).GetField("_global", BindingFlags.Static | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("Unable to resolve static field _global.");
            }
        }

        private interface IFf5FallbackFieldProxy
        {
            [DuckField(Name = "_hidden")]
            int Hidden { get; }
        }

        private class Ff5FallbackFieldTarget
        {
            private readonly int _hidden;

            public Ff5FallbackFieldTarget(int hidden)
            {
                _hidden = hidden;
            }
        }

        private class Ff5FallbackFieldAotProxy : IFf5FallbackFieldProxy
        {
            private static readonly FieldInfo HiddenField = GetHiddenField();
            private readonly Ff5FallbackFieldTarget _target;

            public Ff5FallbackFieldAotProxy(Ff5FallbackFieldTarget target)
            {
                _target = target;
            }

            public int Hidden
            {
                get
                {
                    if (HiddenField.GetValue(_target) is int value)
                    {
                        return value;
                    }

                    return default;
                }
            }

            private static FieldInfo GetHiddenField()
            {
                return typeof(Ff5FallbackFieldTarget).GetField("_hidden", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("Unable to resolve fallback field _hidden.");
            }
        }

        private interface IRt2VoidMismatchProxy
        {
            void Ping();
        }

        private class Rt2VoidMismatchTarget
        {
            public int Ping()
            {
                return 1;
            }
        }

        private interface IFg7ReverseGetterProxy
        {
            string Name { get; }
        }

        private class Fg7ReverseGetterDelegation
        {
            private readonly string _name;

            public Fg7ReverseGetterDelegation(string name)
            {
                _name = name;
            }

            [DuckReverseMethod]
            public string Name => _name;
        }

        private class Fg7ReverseGetterAotProxy : IFg7ReverseGetterProxy
        {
            private readonly Fg7ReverseGetterDelegation _delegation;

            public Fg7ReverseGetterAotProxy(Fg7ReverseGetterDelegation delegation)
            {
                _delegation = delegation;
            }

            public string Name => _delegation.Name;
        }

        private interface IFg8IndexerInnerProxy
        {
            int Number { get; }
        }

        private class Fg8IndexerInnerTarget
        {
            public Fg8IndexerInnerTarget(int number)
            {
                Number = number;
            }

            public int Number { get; }
        }

        private class Fg8IndexerInnerAotProxy : IFg8IndexerInnerProxy
        {
            private readonly Fg8IndexerInnerTarget _target;

            public Fg8IndexerInnerAotProxy(Fg8IndexerInnerTarget target)
            {
                _target = target;
            }

            public int Number => _target.Number;
        }

        private interface IFg8IndexerProxy
        {
            IFg8IndexerInnerProxy this[int index] { get; }
        }

        private class Fg8IndexerTarget
        {
            public Fg8IndexerInnerTarget this[int index] => new Fg8IndexerInnerTarget(index + 100);
        }

        private class Fg8IndexerAotProxy : IFg8IndexerProxy
        {
            private readonly Fg8IndexerTarget _target;

            public Fg8IndexerAotProxy(Fg8IndexerTarget target)
            {
                _target = target;
            }

            public IFg8IndexerInnerProxy this[int index]
            {
                get => new Fg8IndexerInnerAotProxy(_target[index]);
            }
        }

        private interface IFs3SetterInnerProxy
        {
            int Number { get; }
        }

        private class Fs3SetterInnerTarget
        {
            public Fs3SetterInnerTarget(int number)
            {
                Number = number;
            }

            public int Number { get; }
        }

        private class Fs3SetterInnerAotProxy : IFs3SetterInnerProxy
        {
            private readonly Fs3SetterInnerTarget _target;

            public Fs3SetterInnerAotProxy(Fs3SetterInnerTarget target)
            {
                _target = target;
            }

            public int Number => _target.Number;

            internal Fs3SetterInnerTarget Target => _target;
        }

        private interface IFs3SetterDuckExtractProxy
        {
            [Duck(Name = "Inner")]
            IFs3SetterInnerProxy Inner { set; }

            [Duck(Name = "Read")]
            int Read();
        }

        private class Fs3SetterDuckExtractTarget
        {
            public Fs3SetterInnerTarget Inner { private get; set; } = new Fs3SetterInnerTarget(0);

            public int Read()
            {
                return Inner.Number;
            }
        }

        private class Fs3SetterDuckExtractAotProxy : IFs3SetterDuckExtractProxy
        {
            private readonly Fs3SetterDuckExtractTarget _target;

            public Fs3SetterDuckExtractAotProxy(Fs3SetterDuckExtractTarget target)
            {
                _target = target;
            }

            public IFs3SetterInnerProxy Inner
            {
                set
                {
                    if (value is Fs3SetterInnerAotProxy aotProxy)
                    {
                        _target.Inner = aotProxy.Target;
                        return;
                    }

                    _target.Inner = new Fs3SetterInnerTarget(value.Number);
                }
            }

            public int Read()
            {
                return _target.Read();
            }
        }

        private interface IFs4SetterDuckCreateProxy
        {
            [Duck(Name = "Inner")]
            object Inner { set; }

            [Duck(Name = "Read")]
            int Read();
        }

        private interface IFs4SetterContract
        {
            int Number { get; }
        }

        private class Fs4SetterConcrete
        {
            public Fs4SetterConcrete(int number)
            {
                Number = number;
            }

            public int Number { get; }
        }

        private sealed class Fs4SetterConcreteAdapter : IFs4SetterContract
        {
            private readonly Fs4SetterConcrete _instance;

            public Fs4SetterConcreteAdapter(Fs4SetterConcrete instance)
            {
                _instance = instance;
            }

            public int Number => _instance.Number;
        }

        private class Fs4SetterDuckCreateTarget
        {
            public IFs4SetterContract Inner { private get; set; } = new Fs4SetterConcreteAdapter(new Fs4SetterConcrete(0));

            public int Read()
            {
                return Inner.Number;
            }
        }

        private class Fs4SetterDuckCreateAotProxy : IFs4SetterDuckCreateProxy
        {
            private readonly Fs4SetterDuckCreateTarget _target;

            public Fs4SetterDuckCreateAotProxy(Fs4SetterDuckCreateTarget target)
            {
                _target = target;
            }

            public object Inner
            {
                set
                {
                    if (value is IFs4SetterContract contract)
                    {
                        _target.Inner = contract;
                        return;
                    }

                    throw new InvalidCastException("Unable to cast setter input to IFs4SetterContract.");
                }
            }

            public int Read()
            {
                return _target.Read();
            }
        }

        private interface IFm2ValueReceiverProxy
        {
            int Increment(int value);
        }

        private struct Fm2ValueReceiverTarget
        {
            public int Offset;

            public int Increment(int value)
            {
                return value + Offset;
            }
        }

        private class Fm2ValueReceiverAotProxy : IFm2ValueReceiverProxy
        {
            private readonly Fm2ValueReceiverTarget _target;

            public Fm2ValueReceiverAotProxy(Fm2ValueReceiverTarget target)
            {
                _target = target;
            }

            public int Increment(int value)
            {
                return _target.Increment(value);
            }
        }

        private interface IFm3StaticMethodProxy
        {
            [Duck(Name = "Multiply")]
            int Multiply(int left, int right);
        }

        private class Fm3StaticMethodTarget
        {
            public static int Multiply(int left, int right)
            {
                return left * right;
            }
        }

        private class Fm3StaticMethodAotProxy : IFm3StaticMethodProxy
        {
            public Fm3StaticMethodAotProxy(Fm3StaticMethodTarget target)
            {
                _ = target;
            }

            public int Multiply(int left, int right)
            {
                return Fm3StaticMethodTarget.Multiply(left, right);
            }
        }

        private interface IFm4NonPublicMethodProxy
        {
            [Duck(Name = "Add")]
            int Add(int left, int right);
        }

        private class Fm4NonPublicMethodTarget
        {
            private int Add(int left, int right)
            {
                return left + right;
            }
        }

        private class Fm4NonPublicMethodAotProxy : IFm4NonPublicMethodProxy
        {
            private static readonly MethodInfo AddMethod = GetAddMethod();
            private readonly Fm4NonPublicMethodTarget _target;

            public Fm4NonPublicMethodAotProxy(Fm4NonPublicMethodTarget target)
            {
                _target = target;
            }

            public int Add(int left, int right)
            {
                return (int)AddMethod.Invoke(_target, new object[] { left, right })!;
            }

            private static MethodInfo GetAddMethod()
            {
                return typeof(Fm4NonPublicMethodTarget).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("Unable to resolve non-public method Add.");
            }
        }

        private interface IFm5GenericMethodProxy
        {
            T Echo<T>(T value);
        }

        private class Fm5GenericMethodTarget
        {
            public T Echo<T>(T value)
            {
                return value;
            }
        }

        private class Fm5GenericMethodAotProxy : IFm5GenericMethodProxy
        {
            private readonly Fm5GenericMethodTarget _target;

            public Fm5GenericMethodAotProxy(Fm5GenericMethodTarget target)
            {
                _target = target;
            }

            public T Echo<T>(T value)
            {
                return _target.Echo(value);
            }
        }

        private interface IFm6FallbackMethodProxy
        {
            [Duck(Name = "Compute")]
            int Compute(int value);
        }

        private class Fm6FallbackMethodTarget
        {
            internal int Compute(int value)
            {
                return value + 9;
            }
        }

        private class Fm6FallbackMethodAotProxy : IFm6FallbackMethodProxy
        {
            private static readonly MethodInfo ComputeMethod = GetComputeMethod();
            private readonly Fm6FallbackMethodTarget _target;

            public Fm6FallbackMethodAotProxy(Fm6FallbackMethodTarget target)
            {
                _target = target;
            }

            public int Compute(int value)
            {
                return (int)ComputeMethod.Invoke(_target, new object[] { value })!;
            }

            private static MethodInfo GetComputeMethod()
            {
                return typeof(Fm6FallbackMethodTarget).GetMethod("Compute", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    ?? throw new InvalidOperationException("Unable to resolve fallback method Compute.");
            }
        }

        private interface IFm7RefOutMismatchProxy
        {
            void Normalize(ref object value, out object doubled);
        }

        private class Fm7RefOutMismatchTarget
        {
            public void Normalize(ref int value, out int doubled)
            {
                value = Math.Abs(value);
                doubled = value * 2;
            }
        }

        private class Fm7RefOutMismatchAotProxy : IFm7RefOutMismatchProxy
        {
            private readonly Fm7RefOutMismatchTarget _target;

            public Fm7RefOutMismatchAotProxy(Fm7RefOutMismatchTarget target)
            {
                _target = target;
            }

            public void Normalize(ref object value, out object doubled)
            {
                var local = Convert.ToInt32(value);
                _target.Normalize(ref local, out var localOut);
                value = local;
                doubled = localOut;
            }
        }

        private interface IRt4DuckChainInnerProxy
        {
            int Number { get; }
        }

        private class Rt4DuckChainInnerTarget
        {
            public Rt4DuckChainInnerTarget(int number)
            {
                Number = number;
            }

            public int Number { get; }
        }

        private class Rt4DuckChainInnerAotProxy : IRt4DuckChainInnerProxy
        {
            private readonly Rt4DuckChainInnerTarget _target;

            public Rt4DuckChainInnerAotProxy(Rt4DuckChainInnerTarget target)
            {
                _target = target;
            }

            public int Number => _target.Number;
        }

        private interface IRt4DuckChainReturnProxy
        {
            IRt4DuckChainInnerProxy GetInner();
        }

        private class Rt4DuckChainReturnTarget
        {
            private readonly Rt4DuckChainInnerTarget _inner;

            public Rt4DuckChainReturnTarget(Rt4DuckChainInnerTarget inner)
            {
                _inner = inner;
            }

            public Rt4DuckChainInnerTarget GetInner()
            {
                return _inner;
            }
        }

        private class Rt4DuckChainReturnAotProxy : IRt4DuckChainReturnProxy
        {
            private readonly Rt4DuckChainReturnTarget _target;

            public Rt4DuckChainReturnAotProxy(Rt4DuckChainReturnTarget target)
            {
                _target = target;
            }

            public IRt4DuckChainInnerProxy GetInner()
            {
                return new Rt4DuckChainInnerAotProxy(_target.GetInner());
            }
        }

        private interface IRt5ValueWithTypeReturnProxy
        {
            [Duck(Name = "GetCount")]
            ValueWithType<int> GetCount();
        }

        private class Rt5ValueWithTypeReturnTarget
        {
            public int GetCount()
            {
                return 44;
            }
        }

        private class Rt5ValueWithTypeReturnAotProxy : IRt5ValueWithTypeReturnProxy
        {
            private readonly Rt5ValueWithTypeReturnTarget _target;

            public Rt5ValueWithTypeReturnAotProxy(Rt5ValueWithTypeReturnTarget target)
            {
                _target = target;
            }

            public ValueWithType<int> GetCount()
            {
                return ValueWithType<int>.Create(_target.GetCount(), typeof(int));
            }
        }

        private interface IReverseGreetingProxy
        {
            string Greet();
        }

        private class ReverseGreetingDelegation
        {
            private readonly string _name;

            public ReverseGreetingDelegation(string name)
            {
                _name = name;
            }

            [DuckReverseMethod]
            public string Greet()
            {
                return "hello " + _name;
            }
        }

        private class ReverseGreetingAotProxy : IReverseGreetingProxy
        {
            private readonly ReverseGreetingDelegation _delegation;

            public ReverseGreetingAotProxy(ReverseGreetingDelegation delegation)
            {
                _delegation = delegation;
            }

            public string Greet()
            {
                return _delegation.Greet();
            }
        }

        private interface IReverseStateProxy
        {
            int State { get; set; }
        }

        private class ReverseStateDelegation
        {
            [DuckReverseMethod]
            public int State { get; set; }
        }

        private class ReverseStateAotProxy : IReverseStateProxy
        {
            private readonly ReverseStateDelegation _delegation;

            public ReverseStateAotProxy(ReverseStateDelegation delegation)
            {
                _delegation = delegation;
            }

            public int State
            {
                get => _delegation.State;
                set => _delegation.State = value;
            }
        }

        private interface IReverseMathProxy
        {
            int Multiply(int left, int right);
        }

        private class ReverseMathDelegation
        {
            [DuckReverseMethod]
            public int Multiply(int left, int right)
            {
                return left * right;
            }
        }

        private class ReverseMathAotProxy : IReverseMathProxy
        {
            private readonly ReverseMathDelegation _delegation;

            public ReverseMathAotProxy(ReverseMathDelegation delegation)
            {
                _delegation = delegation;
            }

            public int Multiply(int left, int right)
            {
                return _delegation.Multiply(left, right);
            }
        }

        private interface IReverseB27Proxy
        {
            int Increment(int value);
        }

        private class ReverseB27Delegation
        {
            [DuckReverseMethod]
            public int Increment(int value)
            {
                return value + 1;
            }
        }

        private class ReverseB27AotProxy : IReverseB27Proxy
        {
            private readonly ReverseB27Delegation _delegation;

            public ReverseB27AotProxy(ReverseB27Delegation delegation)
            {
                _delegation = delegation;
            }

            public int Increment(int value)
            {
                return _delegation.Increment(value);
            }
        }

        private interface IReverseRefOutProxy
        {
            void Normalize(ref int value, out int doubled);
        }

        private class ReverseRefOutDelegation
        {
            [DuckReverseMethod]
            public void Normalize(ref int value, out int doubled)
            {
                value = Math.Abs(value);
                doubled = value * 2;
            }
        }

        private class ReverseRefOutAotProxy : IReverseRefOutProxy
        {
            private readonly ReverseRefOutDelegation _delegation;

            public ReverseRefOutAotProxy(ReverseRefOutDelegation delegation)
            {
                _delegation = delegation;
            }

            public void Normalize(ref int value, out int doubled)
            {
                _delegation.Normalize(ref value, out doubled);
            }
        }

        private abstract class ReverseAbstractBase
        {
            public abstract int Compute(int left, int right);
        }

        private class ReverseAbstractDelegation
        {
            [DuckReverseMethod]
            public int Compute(int left, int right)
            {
                return (left * 2) + right;
            }
        }

        private class ReverseAbstractAotProxy : ReverseAbstractBase
        {
            private readonly ReverseAbstractDelegation _delegation;

            public ReverseAbstractAotProxy(ReverseAbstractDelegation delegation)
            {
                _delegation = delegation;
            }

            public override int Compute(int left, int right)
            {
                return _delegation.Compute(left, right);
            }
        }

        private abstract class ReverseRequiredMethodBase
        {
            public abstract int Required(int value);
        }

        private class ReverseRequiredMethodDelegation
        {
        }

        private abstract class ReverseGenericContractBase
        {
            public abstract T Echo<T>(T value);
        }

        private class ReverseGenericMismatchDelegation
        {
            [DuckReverseMethod]
            public int Echo(int value)
            {
                return value;
            }
        }

        [AttributeUsage(AttributeTargets.Class)]
        private sealed class ReverseMarkerAttribute : Attribute
        {
            public ReverseMarkerAttribute(string marker)
            {
                Marker = marker;
            }

            public string Marker { get; }
        }

        private interface IReverseAttributeCopyProxy
        {
            int Bump(int value);
        }

        [ReverseMarker("copied-marker")]
        private class ReverseAttributeCopyDelegation
        {
            [DuckReverseMethod]
            public int Bump(int value)
            {
                return value + 4;
            }
        }

        [ReverseMarker("copied-marker")]
        private class ReverseAttributeCopyAotProxy : IReverseAttributeCopyProxy
        {
            private readonly ReverseAttributeCopyDelegation _delegation;

            public ReverseAttributeCopyAotProxy(ReverseAttributeCopyDelegation delegation)
            {
                _delegation = delegation;
            }

            public int Bump(int value)
            {
                return _delegation.Bump(value);
            }
        }

        private struct ReverseStructBase
        {
            public int Value { get; set; }
        }

        private class ReverseStructDelegation
        {
        }
    }
}
