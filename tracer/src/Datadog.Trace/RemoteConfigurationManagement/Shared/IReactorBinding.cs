// <copyright file="IReactorBinding.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.RemoteConfigurationManagement.Shared;

internal interface IReactorBinding : IDisposable
{
    int GetError();

    Func<int, int> BindUnary(string export);

    Func<int, int, int> BindBinary(string export);

    Func<int, int, int, int> BindTernary(string export);

    Func<int, int, int, int, int> BindQuaternary(string export);

    ReadOnlySpan<byte> BorrowRead(int handle, int length);

    Span<byte> BorrowWrite(int handle, int length);
}
