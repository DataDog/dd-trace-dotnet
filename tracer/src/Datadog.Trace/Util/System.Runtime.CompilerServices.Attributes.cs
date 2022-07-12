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
