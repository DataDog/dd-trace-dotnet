// <copyright file="SymbolVisibility.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

namespace Datadog.Trace.Tools.Analyzers.Helpers
{
#pragma warning disable CA1027 // Mark enums with FlagsAttribute
    internal enum SymbolVisibility
#pragma warning restore CA1027 // Mark enums with FlagsAttribute
    {
        Public = 0,
        Internal = 1,
        Private = 2,
        Friend = Internal,
    }
}
