// <copyright file="AssemblyIdentity.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

/// <summary>
/// The identity of an assembly, to reference it from another scope.
/// </summary>
internal readonly record struct AssemblyIdentity(string Name, Version Version, string Culture, byte[] PublicKeyOrToken, uint Flags);
#endif
