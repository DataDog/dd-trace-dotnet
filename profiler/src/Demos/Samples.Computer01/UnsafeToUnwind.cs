// <copyright file="UnsafeToUnwind.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Samples.Computer01
{
    internal class UnsafeToUnwind
    {
        private ManualResetEvent _stopEvent;
        private Thread _worker;
        private Thread _workerUnsafe;
        private Thread _workerException;
        private Thread _workerContention;

        public void Start()
        {
            if (_stopEvent != null)
            {
                throw new InvalidOperationException("Already running...");
            }

            _stopEvent = new ManualResetEvent(false);

            _worker = CreateAndStartThread(SafeToUnwind);
            _workerUnsafe = CreateAndStartThread(Wrap_UnSafeToUnwind);
            _workerException = CreateAndStartThread(StartException);
            _workerContention = CreateAndStartThread(StartContention);
        }

        public void Run()
        {
            Start();
            Thread.Sleep(TimeSpan.FromSeconds(10));
            Stop();
        }

        public void Stop()
        {
            if (_stopEvent == null)
            {
                throw new InvalidOperationException("Not running...");
            }

            _stopEvent.Set();

            _worker.Join();
            _workerUnsafe.Join();
            _workerException.Join();
            _workerContention.Join();
            _stopEvent.Dispose();
            _stopEvent = null;
        }

        private static Thread CreateAndStartThread(ThreadStart action)
        {
            var t = new Thread(action)
            {
                IsBackground = false // set to false to prevent the app from shutting down. The test will fail
            };
            t.Start();

            return t;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Wrap_RaiseExceptionUnsafeUnwind()
        {
            RaiseExceptionUnsafeUnwind();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RaiseExceptionUnsafeUnwind()
        {
            throw new Exception("Whoops");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RaiseExceptionSafeUnwind()
        {
            throw new Exception("Whoops but safe");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void SafeToUnwind()
        {
            _stopEvent.WaitOne();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Wrap_UnSafeToUnwind()
        {
            UnSafeToUnwind();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void UnSafeToUnwind()
        {
            _stopEvent.WaitOne();
        }

        private void StartException()
        {
            ulong i = 0;
            while (!_stopEvent.WaitOne(0))
            {
                try
                {
                    if (i++ % 2 == 0)
                    {
                        Wrap_RaiseExceptionUnsafeUnwind();
                    }
                    else
                    {
                        RaiseExceptionSafeUnwind();
                    }
                }
                catch { } // do not care

                Thread.Sleep(TimeSpan.FromMilliseconds(250));
            }
        }

        private void StartContention()
        {
            var lockObj = new object();
            var otherThread = CreateAndStartThread(() =>
            {
                while (!_stopEvent.WaitOne(0))
                {
                    Wrap_ContentionUnsafeToUnwind(lockObj);
                }
            });

            while (!_stopEvent.WaitOne(0))
            {
                ContentionSafeToUnwind(lockObj);
            }

            otherThread.Join();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Wrap_ContentionUnsafeToUnwind(object lockObj)
        {
            ContentionUnsafeToUnwind(lockObj);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ContentionUnsafeToUnwind(object lockObj)
        {
            LockAndSleep(lockObj);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ContentionSafeToUnwind(object lockObj)
        {
            LockAndSleep(lockObj);
        }

        private void LockAndSleep(object lockObj)
        {
            lock (lockObj)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(250));
            }
        }
    }
}
