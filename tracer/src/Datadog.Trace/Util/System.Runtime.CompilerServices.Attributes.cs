// <copyright file="System.Runtime.CompilerServices.Attributes.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// This file contains attributes from the System.Diagnostics.CompilerServices namespace
// used by the compiler. We define them here for older .NET runtimes.

#pragma warning disable SA1649 // file name should match first type name
#pragma warning disable SA1402 // file may only contain a single type

#if !NETCOREAPP3_0_OR_GREATER

// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Parameter)]
    internal sealed class CallerArgumentExpressionAttribute : Attribute
    {
        public CallerArgumentExpressionAttribute(string parameterName)
        {
            ParameterName = parameterName;
        }

        public string ParameterName { get; }
    }
}

#endif

#pragma warning restore SA1402
#pragma warning restore SA1649
