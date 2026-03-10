// <copyright file="JsonArrayPool.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Util.Json;

internal sealed class JsonArrayPool : IArrayPool<char>
{
    public static readonly JsonArrayPool Shared = new(ArrayPool<char>.Shared);

    private readonly ArrayPool<char> _pool;

    [TestingAndPrivateOnly]
    internal JsonArrayPool(ArrayPool<char> pool)
    {
        _pool = pool;
    }

    public char[] Rent(int minimumLength) => _pool.Rent(minimumLength);

    public void Return(char[]? array)
    {
        if (array is not null)
        {
            _pool.Return(array, clearArray: false);
        }
    }
}
