// <copyright file="GenericMethodSignatureValidationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Reflection;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.DuckTyping.Tests.Errors.Methods.Generics
{
    public class GenericMethodSignatureValidationTests
    {
        private interface IConcreteReferenceDefaultReturnProxy
        {
            string ReturnDefault<T>();
        }

        private interface IConcreteReferenceNonDefaultReturnProxy
        {
            string ReturnNonDefault<T>();
        }

        private interface IReorderedArgumentProxy
        {
            TSecond EchoArgument<TFirst, TSecond>(TFirst value);
        }

        private interface IReorderedReturnProxy
        {
            TFirst EchoReturn<TFirst, TSecond>(TSecond value);
        }

        private interface IReorderedRefParameterProxy
        {
            void Clear<TFirst, TSecond>(ref TFirst value);
        }

        private interface IReorderedOutParameterProxy
        {
            void SetDefault<TFirst, TSecond>(out TFirst value);
        }

        private interface INestedGenericReturnProxy
        {
            Tuple<TFirst, TFirst> Wrap<TFirst, TSecond>(TFirst first, TSecond second);
        }

        private interface IReorderedArrayReturnProxy
        {
            TFirst[] ReturnArray<TFirst, TSecond>();
        }

        private interface IValueWithTypeReturnProxy
        {
            ValueWithType<T> ReturnWithType<T>(T value);
        }

        private interface ICovariantReferenceReturnProxy
        {
            IEnumerable<object> ReturnCovariant<T>(T value);
        }

        private interface IReverseValueWithTypeContract
        {
            ValueWithType<T> ReturnWithType<T>(T value);
        }

        private interface IReverseGenericContract
        {
            TSecond Echo<TFirst, TSecond>(TSecond value);
        }

        [Fact]
        public void RejectsOpenGenericReturnTypeMappedToConcreteReferenceType()
        {
            // ReturnDefault<int>() used to appear to return null because the zero bits were interpreted as a null
            // object reference. Proxy creation must reject the open T-to-string conversion, so the misleading
            // result can never be observed by a caller.
            AssertInvalidProxy<IConcreteReferenceDefaultReturnProxy>();
        }

        [Fact]
        public void RejectsNonZeroOpenGenericReturnTypeMappedToConcreteReferenceType()
        {
            // ReturnNonDefault<int>() leaves 0x11223344 on the evaluation stack before the old implementation emits
            // castclass string. On affected runtimes, treating those integer bits as an object reference terminates
            // the process with an AccessViolationException. The regression test stops at proxy creation because
            // executing the malformed method inside the test runner would make the entire suite unstable.
            AssertInvalidProxy<IConcreteReferenceNonDefaultReturnProxy>();
        }

        [Fact]
        public void RejectsDifferentGenericParameterPositionsInArgumentType()
        {
            // The names of generic parameters are not significant, but their positions are. TFirst and TSecond
            // can be instantiated with unrelated representations such as Int32/Int64 or string/WideValue. Treating
            // an argument as the other position can truncate its value or make the generated call invalid.
            AssertInvalidProxy<IReorderedArgumentProxy>();
        }

        [Fact]
        public void RejectsDifferentGenericParameterPositionsInReturnType()
        {
            // Keep the argument types equivalent so this test reaches the return-specific validation. Returning
            // TSecond as TFirst without a conversion can leave a differently sized value or a value/reference
            // mismatch on the evaluation stack.
            AssertInvalidProxy<IReorderedReturnProxy>();
        }

        [Fact]
        public void RejectsDifferentGenericParameterPositionsInRefParameter()
        {
            // A managed reference carries the storage layout of its element type. Passing ref TFirst to a method
            // expecting ref TSecond is unsafe even when both parameters happen to have the same size in one
            // instantiation, because another valid instantiation can use differently sized value types.
            AssertInvalidProxy<IReorderedRefParameterProxy>();
        }

        [Fact]
        public void RejectsDifferentGenericParameterPositionsInOutParameter()
        {
            // The target writes a TSecond value through this reference. The proxy must not expose the same storage
            // as TFirst, otherwise the generated copy-back IL can overwrite adjacent memory when their sizes differ.
            AssertInvalidProxy<IReorderedOutParameterProxy>();
        }

        [Fact]
        public void RejectsDifferentGenericParameterPositionsNestedInsideGenericType()
        {
            // Generic identity must be compared recursively. Tuple<TFirst, TSecond> and
            // Tuple<TFirst, TFirst> are different closed types whenever the caller chooses different arguments,
            // even though their outer generic type definition is the same.
            AssertInvalidProxy<INestedGenericReturnProxy>();
        }

        [Fact]
        public void RejectsDifferentGenericParameterPositionsNestedInsideArray()
        {
            // Arrays are reference types, but their element types remain part of their runtime identity. A proxy
            // returning TFirst[] cannot implement a target returning TSecond[] because callers may choose unrelated
            // element types. Detect the mismatch while the signature is open instead of emitting a runtime cast.
            AssertInvalidProxy<IReorderedArrayReturnProxy>();
        }

        [Fact]
        public void AllowsOpenGenericReturnWrappedWithRuntimeType()
        {
            // ValueWithType<T> is an explicit DuckTyping return contract. AddReturnIl extracts the T value from the
            // target and wraps it together with its runtime Type, so signature validation must compare the wrapper's
            // element type with the target placeholder instead of rejecting the supported wrapper itself.
            var proxy = new GenericMethodSignatureTarget().DuckCast<IValueWithTypeReturnProxy>();

            ValueWithType<int> result = proxy.ReturnWithType(42);
            result.Value.Should().Be(42);
            result.Type.Should().Be(typeof(int));
        }

        [Fact]
        public void AllowsOpenGenericReturnWrappedWithRuntimeTypeInReverseProxy()
        {
            // Reverse proxies use the same return helper with the contract as the outer method. Preserve the wrapper
            // there as well while still requiring its T to match the implementation's generic parameter position.
            var implementation = new ReverseValueWithTypeImplementation();
            var proxy = (IReverseValueWithTypeContract)implementation.DuckImplement(typeof(IReverseValueWithTypeContract));

            ValueWithType<string> result = proxy.ReturnWithType("expected");
            result.Value.Should().Be("expected");
            result.Type.Should().Be(typeof(string));
        }

        [Fact]
        public void AllowsCastSafeConversionInsideConstructedReferenceReturnType()
        {
            // A concrete argument nested inside a reference-type return can be checked by castclass at invocation
            // time. Covariance makes IEnumerable<string> compatible with IEnumerable<object>; an incompatible value
            // type instantiation still fails with a managed InvalidCastException instead of producing unsafe IL.
            var proxy = new GenericMethodSignatureTarget().DuckCast<ICovariantReferenceReturnProxy>();

            proxy.ReturnCovariant("expected").Should().Equal("expected");

            Action returnValueType = () => proxy.ReturnCovariant(42);
            returnValueType.Should().Throw<InvalidCastException>();
        }

        [Fact]
        public void RejectsDifferentGenericParameterPositionsInReverseProxy()
        {
            // Keep the argument positions equivalent so this test reaches the return-specific validation for
            // reverse proxies. They emit the same kind of direct generic call, with the contract and implementation
            // roles exchanged, and must reject a different return placeholder before defining the override.
            var implementation = new ReorderedReverseImplementation();

            Action create = () => implementation.DuckImplement(typeof(IReverseGenericContract));
            create.Should().Throw<TargetInvocationException>();
        }

        private static void AssertInvalidProxy<TProxy>()
        {
            var target = new GenericMethodSignatureTarget();

            using var scope = new AssertionScope();
            target.DuckIs(typeof(TProxy)).Should().BeFalse();

            Action cast = () => target.DuckCast(typeof(TProxy));
            cast.Should().Throw<TargetInvocationException>();
        }

        private class GenericMethodSignatureTarget
        {
            public T ReturnDefault<T>() => default;

            public T ReturnNonDefault<T>() => (T)(object)0x11223344;

            public TSecond EchoArgument<TFirst, TSecond>(TSecond value) => value;

            public TSecond EchoReturn<TFirst, TSecond>(TSecond value) => value;

            public void Clear<TFirst, TSecond>(ref TSecond value) => value = default;

            public void SetDefault<TFirst, TSecond>(out TSecond value) => value = default;

            public Tuple<TFirst, TSecond> Wrap<TFirst, TSecond>(TFirst first, TSecond second) => Tuple.Create(first, second);

            public TSecond[] ReturnArray<TFirst, TSecond>() => Array.Empty<TSecond>();

            public T ReturnWithType<T>(T value) => value;

            public IEnumerable<T> ReturnCovariant<T>(T value)
            {
                yield return value;
            }
        }

        private class ReverseValueWithTypeImplementation
        {
            [DuckReverseMethod]
            public T ReturnWithType<T>(T value) => value;
        }

        private class ReorderedReverseImplementation
        {
            [DuckReverseMethod]
            public TFirst Echo<TFirst, TSecond>(TSecond value) => default;
        }
    }
}
