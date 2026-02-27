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
