// <copyright file="DisposableEnumerable.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

namespace Datadog.Trace.Debugger.Helpers
{
    internal class DisposableEnumerable<T> : IDisposable
        where T : IDisposable
    {
        private IEnumerable<T> _items;

        public DisposableEnumerable(IEnumerable<T> items) => _items = items;

        public void Dispose()
        {
            foreach (var item in _items)
            {
                try
                {
                    item.Dispose();
                }
                catch
                {
                }
            }
        }
    }
}
