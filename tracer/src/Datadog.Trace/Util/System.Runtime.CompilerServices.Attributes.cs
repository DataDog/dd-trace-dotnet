// <copyright file="System.Runtime.CompilerServices.Attributes.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// This file contains attributes from the System.Runtime.CompilerServices namespace
// used by the compiler for null-state static analysis.
// This is a C# feature, but requires these attributes to be defined,
// so we define them here for older .NET runtimes.

#pragma warning disable SA1649 // file name should match first type name
#pragma warning disable SA1402 // file may only contain a single type

// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices;

#if !NET5_0_OR_GREATER

internal static class IsExternalInit
{
}

/// <summary>
/// Used to indicate to the compiler that the <c>.locals init</c>
/// flag should not be set in method headers.
/// </summary>
/// <remarks>
/// This attribute is unsafe because it may reveal uninitialized memory to
/// the application in certain instances (e.g., reading from uninitialized
/// stackalloc'd memory). If applied to a method directly, the attribute
/// applies to that method and all nested functions (lambdas, local
/// functions) below it. If applied to a type or module, it applies to all
/// methods nested inside. This attribute is intentionally not permitted on
/// assemblies. Use at the module level instead to apply to multiple type
/// declarations.
/// </remarks>
[AttributeUsage(
    AttributeTargets.Module
  | AttributeTargets.Class
  | AttributeTargets.Struct
  | AttributeTargets.Interface
  | AttributeTargets.Constructor
  | AttributeTargets.Method
  | AttributeTargets.Property
  | AttributeTargets.Event,
    Inherited = false)]
internal sealed class SkipLocalsInitAttribute : Attribute
{
}

#endif

#if !NET7_0_OR_GREATER
/// <summary>
/// Specifies that a type has required members or that a member is required.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property, Inherited = false)]
internal sealed class RequiredMemberAttribute : Attribute
{
}

/// <summary>
/// Indicates that compiler support for a particular feature is required for the location where this attribute is applied.
/// </summary>
[AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
internal sealed class CompilerFeatureRequiredAttribute : Attribute
{
    public const string RefStructs = nameof(RefStructs);
    public const string RequiredMembers = nameof(RequiredMembers);

    public CompilerFeatureRequiredAttribute(string featureName)
    {
        FeatureName = featureName;
    }

    public string FeatureName { get; }

    public bool IsOptional { get; init; }
}
#endif

#if !NET9_0_OR_GREATER

/// <summary>
/// Specifies the priority of a member in overload resolution. When unspecified, the default priority is 0.
/// </summary>
[AttributeUsage(
    AttributeTargets.Method |
    AttributeTargets.Constructor |
    AttributeTargets.Property,
    Inherited = false)]
internal sealed class OverloadResolutionPriorityAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OverloadResolutionPriorityAttribute"/> class.
    /// </summary>
    public OverloadResolutionPriorityAttribute(int priority) => Priority = priority;

    /// <summary>
    /// Gets the priority of the member.
    /// </summary>
    public int Priority { get; }
}
#endif
