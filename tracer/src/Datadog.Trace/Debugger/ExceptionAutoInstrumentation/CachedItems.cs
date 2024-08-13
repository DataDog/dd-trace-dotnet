// <copyright file="CachedItems.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Vendors.MessagePack;
using Fnv1aHash = Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Internal.Hash;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal class CachedItems
    {
        private readonly HashSet<int> _cache = new();
        private readonly ReaderWriterLockSlim _cacheLocker = new();

        internal void Add(int item)
        {
            _cacheLocker.EnterWriteLock();
            try
            {
                _cache.Add(item);
            }
            finally
            {
                _cacheLocker.ExitWriteLock();
            }
        }

        internal bool Remove(int item)
        {
            _cacheLocker.EnterWriteLock();
            try
            {
                return _cache.Remove(item);
            }
            finally
            {
                _cacheLocker.ExitWriteLock();
            }
        }

        internal bool Contains(int item)
        {
            _cacheLocker.EnterReadLock();
            try
            {
                return _cache.Contains(item);
            }
            finally
            {
                _cacheLocker.ExitReadLock();
            }
        }
    }
}
