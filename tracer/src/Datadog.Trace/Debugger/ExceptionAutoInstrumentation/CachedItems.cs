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
        private readonly HashSet<int> cache = new();
        private readonly ReaderWriterLockSlim cacheLocker = new();

        internal void Add(string item)
        {
            cacheLocker.EnterWriteLock();
            try
            {
                cache.Add(Hash(item));
            }
            finally
            {
                cacheLocker.ExitWriteLock();
            }
        }

        internal bool Remove(string item)
        {
            cacheLocker.EnterWriteLock();
            try
            {
                return cache.Remove(Hash(item));
            }
            finally
            {
                cacheLocker.ExitWriteLock();
            }
        }

        internal bool Contains(string item)
        {
            cacheLocker.EnterReadLock();
            try
            {
                return cache.Contains(Hash(item));
            }
            finally
            {
                cacheLocker.ExitReadLock();
            }
        }

        private int Hash(string item) => Fnv1aHash.GetFNVHashCode(StringEncoding.UTF8.GetBytes(item));
    }
}
