// <copyright file="GlobalUsings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

global using Datadog.Trace.ExtensionMethods;
global using ThrowHelper = Datadog.Trace.Util.ThrowHelper;

#if NET5_0_OR_GREATER
global using Unsafe = System.Runtime.CompilerServices.Unsafe;
#else
// we use Unsafe.IsNullRef() that was added in .NET 5
global using Unsafe = Datadog.Trace.VendoredMicrosoftCode.System.Runtime.CompilerServices.Unsafe.Unsafe;
#endif

#if NETCOREAPP3_1_OR_GREATER
global using System.Buffers;
global using System.Buffers.Binary;
global using System.Buffers.Text;
#else
global using Datadog.Trace.VendoredMicrosoftCode.System;
global using Datadog.Trace.VendoredMicrosoftCode.System.Runtime.CompilerServices.Unsafe;
#endif
