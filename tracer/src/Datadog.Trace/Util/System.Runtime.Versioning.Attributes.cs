// <copyright file="System.Runtime.Versioning.Attributes.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// https://github.com/dotnet/runtime/blob/bffa7cf52b3982597adc5447c25e7aaa3b063c1c/src/libraries/System.Private.CoreLib/src/System/Diagnostics/CodeAnalysis/NullableAttributes.cs

// This file contains attributes from the System.Runtime.Versioning namespace
// used by the compiler for compiler-y stuff.

#nullable enable

#pragma warning disable SA1649 // file name should match first type name
#pragma warning disable SA1402 // file may only contain a single type

// ReSharper disable once CheckNamespace
namespace System.Runtime.Versioning;

// This attribute is used by some vendored Microsoft code
// https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/botr/readytorun-overview.md#performance-impact-1
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Constructor | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
internal sealed class NonVersionableAttribute : Attribute
{
}
