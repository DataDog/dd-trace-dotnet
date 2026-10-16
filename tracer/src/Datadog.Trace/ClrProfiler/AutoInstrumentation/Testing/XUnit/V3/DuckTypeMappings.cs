// <copyright file="DuckTypeMappings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

// The task the test method runners' RunTestCase returns.
[assembly: DuckTypeMapping("Datadog.Trace.DuckTyping.IDuckTypeAwaiter`1[[System.Object, System.Private.CoreLib]]", "Datadog.Trace", "System.Runtime.CompilerServices.ValueTaskAwaiter`1[[Xunit.v3.RunSummary, xunit.v3.core]]", "System.Private.CoreLib")]
[assembly: DuckTypeMapping("Datadog.Trace.DuckTyping.IDuckTypeTask`1[[System.Object, System.Private.CoreLib]]", "Datadog.Trace", "System.Threading.Tasks.ValueTask`1[[Xunit.v3.RunSummary, xunit.v3.core]]", "System.Private.CoreLib")]
