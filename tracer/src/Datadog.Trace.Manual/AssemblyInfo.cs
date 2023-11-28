// <copyright file="AssemblyInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

[assembly: Datadog.Trace.Ci.Coverage.Attributes.AvoidCoverage]

// Sanity check for _OR_GREATER compiler directives
#if !NETCOREAPP3_1_OR_GREATER && !NET461_OR_GREATER && !NETSTANDARD2_0
#error Unsupported TFM or _OR_GREATER compiler directives are not supported
#endif
