// <copyright file="IUnknown.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Tools.dd_dotnet;

[NativeObject]
internal interface IUnknown : IDisposable
{
    int QueryInterface(in Guid guid, out IntPtr ptr);

    int AddRef();

    int Release();
}
