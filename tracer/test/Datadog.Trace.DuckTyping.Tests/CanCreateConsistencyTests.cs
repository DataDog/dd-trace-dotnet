// <copyright file="CanCreateConsistencyTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.DuckTyping.Tests;

public class CanCreateConsistencyTests
{
    public static IEnumerable<object[]> DuckTypingCases() =>
    [
        ..Errors.Fields.ReferenceType.ReferenceTypeFieldErrorTests.Valid(),
        ..Errors.Fields.ReferenceType.ReferenceTypeFieldErrorTests.WrongFieldNames(),
        ..Errors.Fields.ReferenceType.ReferenceTypeFieldErrorTests.WrongReturnTypes(),
        ..Errors.Fields.TypeChaining.TypeChainingFieldErrorTests.Valid(),
        ..Errors.Fields.TypeChaining.TypeChainingFieldErrorTests.WrongFieldNames(),
        ..Errors.Fields.TypeChaining.TypeChainingFieldErrorTests.WrongReturnTypes(),
        ..Errors.Fields.TypeChaining.TypeChainingFieldErrorTests.WrongChainedReturnTypes(),
        ..Errors.Fields.TypeChaining.TypeChainingFieldErrorTests.WrongChainedReturnTypesForInterfaces(),
        ..Errors.Fields.ValueType.ValueTypeFieldErrorTests.Valid(),
        ..Errors.Fields.ValueType.ValueTypeFieldErrorTests.WrongFieldNames(),
        ..Errors.Fields.ValueType.ValueTypeFieldErrorTests.WrongReturnTypes(),
        ..Errors.Properties.ReferenceType.ReferenceTypePropertyErrorTests.Valid(),
        ..Errors.Properties.ReferenceType.ReferenceTypePropertyErrorTests.WrongPropertyNames(),
        ..Errors.Properties.ReferenceType.ReferenceTypePropertyErrorTests.WrongReturnTypes(),
        ..Errors.Properties.TypeChaining.TypeChainingPropertyErrorTests.Valid(),
        ..Errors.Properties.TypeChaining.TypeChainingPropertyErrorTests.WrongPropertyNames(),
        ..Errors.Properties.TypeChaining.TypeChainingPropertyErrorTests.WrongReturnTypes(),
        ..Errors.Properties.TypeChaining.TypeChainingPropertyErrorTests.WrongChainedReturnTypes(),
        ..Errors.Properties.ValueType.ValueTypePropertyErrorTests.Valid(),
        ..Errors.Properties.ValueType.ValueTypePropertyErrorTests.WrongPropertyNames(),
        ..Errors.Properties.ValueType.ValueTypePropertyErrorTests.WrongReturnTypes(),
        ..Errors.Methods.Generics.GenericMethodErrorTests.Valid(),
        ..Errors.Methods.Generics.GenericMethodErrorTests.WrongMethodNames(),
        ..Errors.Methods.Generics.GenericMethodErrorTests.WrongReturnTypes(),
        ..Errors.Methods.Generics.GenericMethodErrorTests.WrongArgumentTypes(),
        ..Errors.Methods.Generics.GenericMethodErrorTests.WrongNumberOfArguments(),
        ..Errors.Methods.NonGenerics.NonGenericMethodErrorTests.Valid(),
        ..Errors.Methods.NonGenerics.NonGenericMethodErrorTests.WrongMethodNames(),
        ..Errors.Methods.NonGenerics.NonGenericMethodErrorTests.WrongReturnTypes(),
        ..Errors.Methods.NonGenerics.NonGenericMethodErrorTests.WrongArgumentTypes(),
        ..Errors.Methods.NonGenerics.NonGenericMethodErrorTests.WrongNumberOfArguments(),
        ..Errors.Methods.NonGenerics.NonGenericMethodErrorTests.WrongArgumentModifiers(),
    ];

    public static IEnumerable<object[]> ReverseDuckTypeCases() =>
    [
        ..ReverseProxyErrorTests.Valid(),
        ..ReverseProxyErrorTests.WrongMethodNames(),
        ..ReverseProxyErrorTests.WrongReturnTypes(),
        ..ReverseProxyErrorTests.WrongNumberOfArguments(),
        ..ReverseProxyErrorTests.WrongArgumentTypes(),
    ];

    [Theory]
    [MemberData(nameof(DuckTypingCases))]
    public void CanCreateAgreesWithEveryOtherRoute(Type duckType, string obscureObjectName)
    {
        var target = ObscureObject.GetObject(obscureObjectName);
        using var scope = new AssertionScope();
        bool canCreate;

        // Deliberately first, so this call populates the cache and everything below reads it back.
        using (var counter = new FirstChanceExceptionCounter())
        {
            canCreate = DuckType.CanCreate(duckType, target);

            target.DuckIs(duckType).Should().Be(canCreate);

            // Deciding whether a proxy can be built must be exception-free, apart from one remaining case
            // (see KnownResidual). Allow-listed rather than ignored, so a *new* kind of first-chance
            // exception still fails here.
            counter.Exceptions.Should().NotContain(x => !KnownResidual(x));
        }

        DuckType.GetOrCreateProxyType(duckType, target.GetType())
                .CanCreate()
                .Should().Be(canCreate);

        // DuckCast reports an unusable proxy shape as a DuckTypeException, and must do so exactly when
        // CanCreate said no. Anything else it throws is not a shape failure and is out of scope here: a
        // [DuckCopy] proxy copies every field at cast time, so a reference-type field whose type doesn't
        // match the target emits a castclass that fails with InvalidCastException *after* the proxy type
        // was legitimately created.
        var failure = CaptureCreationFailure(target, duckType);

        if (canCreate)
        {
            (failure as DuckTypeException).Should().BeNull();
        }
        else
        {
            failure.Should().BeAssignableTo<DuckTypeException>();
            target.TryDuckCast(duckType, out var proxy).Should().BeFalse();
            proxy.Should().BeNull();

            target.DuckAs(duckType).Should().BeNull();
        }

        using (var counter = new FirstChanceExceptionCounter())
        {
            try
            {
                target.TryDuckCast(duckType, out _);
            }
            catch (Exception ex) when (ex is InvalidCastException || ex.InnerException is InvalidCastException)
            {
                // Arrives raw from the generic path, or wrapped in TargetInvocationException from the
                // non-generic one, because that goes through the activator's DynamicInvoke.
                // TryDuckCast also instantiates, and for a [DuckCopy] struct that means copying every field
                // at cast time - so a reference-type field whose type doesn't match the target raises this
                // even though the proxy type was built successfully. It escapes TryDuckCast, which therefore
                // does not honour its own non-throwing contract for that case.
            }

            // Whatever else happens, one of our own shape failures must never surface as a first-chance exception
            counter.Exceptions.Should().NotContain(x => x is DuckTypeException);
        }
    }

    [Theory]
    [MemberData(nameof(ReverseDuckTypeCases))]
    public void ReverseCanCreateAgreesWithEveryOtherRoute(Type reversedType)
    {
        var typeToImplement = reversedType.GetCustomAttribute<ReverseTypeToTestAttribute>()?.TypeToTest;
        typeToImplement.Should().NotBeNull($"Could not find referenced type for {reversedType}");

        var instance = Activator.CreateInstance(reversedType);
        using var scope = new AssertionScope();

        bool canCreate;
        object proxy;
        using (var counter = new FirstChanceExceptionCounter())
        {
            canCreate = DuckType.GetOrCreateReverseProxyType(typeToImplement, reversedType).CanCreate();

            instance.TryDuckImplement(typeToImplement, out proxy).Should().Be(canCreate);

            counter.Exceptions.Should().NotContain(x => !KnownResidual(x));
        }

        if (canCreate)
        {
            proxy.Should().NotBeNull();
            proxy.Should().BeAssignableTo(typeToImplement);
            instance.DuckImplement(typeToImplement).Should().NotBeNull();
        }
        else
        {
            proxy.Should().BeNull();

            Action implement = () => instance.DuckImplement(typeToImplement);
            implement.Should()
                     .Throw<TargetInvocationException>()
                     .WithInnerException<DuckTypeException>();
        }
    }

    /// <summary>
    /// The one first-chance exception source left inside proxy creation.
    /// <para>
    /// <c>Type.GetProperty(name, bindingFlags)</c> throws when a target declares several indexers.
    /// <c>DuckType.FindPropertyOrIndex</c> now resolves the proxy's exact signature up front, which avoids
    /// the throw whenever such an indexer exists - but when the proxy's indexer matches none of them (the
    /// <c>WrongReturnType</c> permutations declare <c>string[] this[string]</c> against a target with
    /// <c>string this[string]</c>), it still falls through to the ambiguous lookup.
    /// </para>
    /// </summary>
    private static bool KnownResidual(Exception exception) => exception is AmbiguousMatchException;

    /// <summary>
    /// Runs the throwing entry point and returns the exception it surfaced, unwrapping the
    /// <see cref="TargetInvocationException"/> that the failure activator's <c>DynamicInvoke</c> adds.
    /// </summary>
    private static Exception CaptureCreationFailure(object target, Type duckType)
    {
        try
        {
            target.DuckCast(duckType);
            return null;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            return ex.InnerException;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }
}
