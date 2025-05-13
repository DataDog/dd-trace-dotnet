// <copyright file="GlobalUsings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

global using ThrowHelper = Datadog.Trace.Util.ThrowHelper;

#if NETFRAMEWORK || NETSTANDARD2_0
global using Datadog.Trace.VendoredMicrosoftCode.System;
#endif

global using Datadog.Trace.ExtensionMethods;
