// <copyright file="DisposableEnumerable.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

namespace Datadog.Trace.Debugger.Helpers
{
    internal readonly struct DisposableEnumerable<T> : IDisposable
        where T : IDisposable
    {
        private readonly List<T> _items;

        public DisposableEnumerable(List<T> items) => _items = items;

        public void Dispose()
        {
            if (_items == null)
            {
                return;
            }

            foreach (var item in this._items)
            {
                SafeDisposal.TryDispose(item);
            }
        }
    }
}
