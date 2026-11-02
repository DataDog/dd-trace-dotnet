// <copyright file="PreventFailureSuppressionAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Ci;

/// <summary>
/// Prevents Datadog quarantine and disabling from suppressing this test.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class PreventFailureSuppressionAttribute : Attribute
{
}
