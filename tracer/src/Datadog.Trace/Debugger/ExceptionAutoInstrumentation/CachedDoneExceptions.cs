// <copyright file="CachedDoneExceptions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal class CachedDoneExceptions
    {
        private static readonly HashSet<string> DoneExceptions = new();
        private static readonly ReaderWriterLockSlim DoneExceptionsLocker = new();

        internal static void Add(string exceptionToString)
        {
            DoneExceptionsLocker.EnterWriteLock();
            try
            {
                DoneExceptions.Add(exceptionToString);
            }
            finally
            {
                DoneExceptionsLocker.ExitWriteLock();
            }
        }

        internal static bool Remove(string exceptionToString)
        {
            DoneExceptionsLocker.EnterWriteLock();
            try
            {
                return DoneExceptions.Remove(exceptionToString);
            }
            finally
            {
                DoneExceptionsLocker.ExitWriteLock();
            }
        }

        internal static bool Contains(string exceptionToString)
        {
            DoneExceptionsLocker.EnterReadLock();
            try
            {
                return DoneExceptions.Contains(exceptionToString);
            }
            finally
            {
                DoneExceptionsLocker.ExitReadLock();
            }
        }
    }
}
