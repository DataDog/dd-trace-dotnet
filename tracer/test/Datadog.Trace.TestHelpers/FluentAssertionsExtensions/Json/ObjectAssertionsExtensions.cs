// <copyright file="ObjectAssertionsExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using FluentAssertions.Equivalency;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;

namespace Datadog.Trace.TestHelpers.FluentAssertionsExtensions.Json;

/// <summary>
///     Contains extension methods for JSON serialization assertion methods
/// </summary>
internal static class ObjectAssertionsExtensions
{
    /// <summary>
    /// Asserts that an object can be serialized and deserialized using the JSON serializer and that it still retains
    /// the values of all members.
    /// </summary>
    /// <param name="assertions">Assertions</param>
    /// <param name="because">
    /// A formatted phrase as is supported by <see cref="string.Format(string,object[])" /> explaining why the assertion
    /// is needed. If the phrase does not start with the word <i>because</i>, it is prepended automatically.
    /// </param>
    /// <param name="becauseArgs">
    /// Zero or more objects to format using the placeholders in <see cref="because" />.
    /// </param>
    [CustomAssertion]
    public static AndConstraint<ObjectAssertions> BeJsonSerializable(this ObjectAssertions assertions, string because = "", params object[] becauseArgs)
    {
        return BeJsonSerializable<object>(assertions, options => options, because, becauseArgs);
    }

    /// <summary>
    /// Asserts that an object can be serialized and deserialized using the JSON serializer and that it stills retains
    /// the values of all members.
    /// </summary>
    /// <param name="assertions">Assertions</param>
    /// <param name="because">
    /// A formatted phrase as is supported by <see cref="string.Format(string,object[])" /> explaining why the assertion
    /// is needed. If the phrase does not start with the word <i>because</i>, it is prepended automatically.
    /// </param>
    /// <param name="becauseArgs">
    /// Zero or more objects to format using the placeholders in <see cref="because" />.
    /// </param>
    [CustomAssertion]
    public static AndConstraint<ObjectAssertions> BeJsonSerializable<T>(this ObjectAssertions assertions, string because = "", params object[] becauseArgs)
    {
        return BeJsonSerializable<T>(assertions, options => options, because, becauseArgs);
    }

    /// <summary>
    /// Asserts that an object can be serialized and deserialized using the JSON serializer and that it stills retains
    /// the values of all members.
    /// </summary>
    /// <param name="assertions">Assertions</param>
    /// <param name="options">Options</param>
    /// <param name="because">
    /// A formatted phrase as is supported by <see cref="string.Format(string,object[])" /> explaining why the assertion
    /// is needed. If the phrase does not start with the word <i>because</i>, it is prepended automatically.
    /// </param>
    /// <param name="becauseArgs">
    /// Zero or more objects to format using the placeholders in <see cref="because" />.
    /// </param>
    [CustomAssertion]
    public static AndConstraint<ObjectAssertions> BeJsonSerializable<T>(this ObjectAssertions assertions, Func<EquivalencyAssertionOptions<T>, EquivalencyAssertionOptions<T>> options, string because = "", params object[] becauseArgs)
    {
        Execute.Assertion.ForCondition(assertions.Subject != null)
            .BecauseOf(because, becauseArgs)
            .FailWith("Expected {context:object} to be JSON serializable{reason}, but the value is null.  Please provide a value for the assertion.");

        Execute.Assertion.ForCondition(assertions.Subject is T)
            .BecauseOf(because, becauseArgs)
            .FailWith("Expected {context:object} to be JSON serializable{reason}, but {context:object} is not assignable to {0}", typeof(T));

        try
        {
            var deserializedObject = CreateCloneUsingJsonSerializer(assertions.Subject);

            var defaultOptions = AssertionOptions.CloneDefaults<T>()
                .RespectingRuntimeTypes()
                .IncludingFields()
                .IncludingProperties();

            var typedSubject = (T)assertions.Subject;
            ((T)deserializedObject).Should().BeEquivalentTo(typedSubject, _ => options(defaultOptions));
        }
#pragma warning disable CA1031 // Ignore catching general exception
        catch (Exception exc)
#pragma warning restore CA1031 // Ignore catching general exception
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .FailWith("Expected {context:object} to be JSON serializable{reason}, but serializing {0} failed with {1}", assertions.Subject, exc);
        }

        return new AndConstraint<ObjectAssertions>(assertions);
    }

    private static object CreateCloneUsingJsonSerializer(object subject)
    {
        var serializedObject = JsonConvert.SerializeObject(subject);
        var cloneUsingJsonSerializer = JsonConvert.DeserializeObject(serializedObject, subject.GetType());
        return cloneUsingJsonSerializer;
    }
}
