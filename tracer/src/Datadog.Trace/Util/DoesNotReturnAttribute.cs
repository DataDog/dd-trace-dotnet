// <copyright file="DoesNotReturnAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#pragma warning disable SA1402 // file may only contain a single type
#if !NETCOREAPP3_0_OR_GREATER

// This file contains attributes from the System.Diagnostics.CodeAnalysis namespace
// used by the compiler for null-state static analysis.
// This is a C# feature, but requires these attributes to be defined,
// so we define them here for older .NET runtimes.
// https: //docs.microsoft.com/en-us/dotnet/csharp/language-reference/attributes/nullable-analysis

using System.ComponentModel;

// ReSharper disable once CheckNamespace
namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>
    /// Applied to a method that will never return under any circumstance.
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class DoesNotReturnAttribute : Attribute
    {
    }

    /// <summary>
    /// Specifies that when a method returns <see cref="ReturnValue"/>, the parameter will not be null even if the corresponding type allows it.
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class NotNullWhenAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NotNullWhenAttribute"/> class with the specified return value condition.
        /// </summary>
        /// <param name="returnValue">
        /// The return value condition. If the method returns this value, the associated parameter will not be null.
        /// </param>
        public NotNullWhenAttribute(bool returnValue) => ReturnValue = returnValue;

        /// <summary>Gets a value indicating whether the associated parameter will not be null..</summary>
        public bool ReturnValue { get; }
    }
}
#endif
#pragma warning restore SA1402
