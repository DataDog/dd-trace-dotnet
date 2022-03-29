// <copyright file="Computer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Demos.Computer01
{
    public class Computer<TClass1, TClass2> : IDisposable
        where TClass1 : new()
        where TClass2 : new()
    {
        public const bool ThrowExceptionFromInnerCompute = false;

        public static readonly TimeSpan StatsPeriodDuration = TimeSpan.FromSeconds(5);

        public static readonly TimeSpan InvokeHotLoopDuration = TimeSpan.FromMilliseconds(500);

        private AppDomain _computationAppDomain;
        private NestedComputer _nestedComputer;
        private ManualResetEventSlim _stopedSignal = null;

        public Computer()
        {
#if NETFRAMEWORK
            _computationAppDomain = AppDomain.CreateDomain("Computation App Domain");
#else
            _computationAppDomain = AppDomain.CurrentDomain;
#endif
            Type nestedComputerType = typeof(NestedComputer);
            _nestedComputer = (NestedComputer)_computationAppDomain.CreateInstanceAndUnwrap(nestedComputerType.Assembly.FullName, nestedComputerType.FullName);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
        }

        public void Stop()
        {
            ManualResetEventSlim stopedSignal = _stopedSignal;

            if (stopedSignal == null)
            {
                var newSignal = new ManualResetEventSlim(initialState: false);
                ManualResetEventSlim existingSignal = Interlocked.CompareExchange(ref _stopedSignal, newSignal, null);

                if (existingSignal == null)
                {
                    stopedSignal = newSignal;
                }
                else
                {
                    stopedSignal = existingSignal;
                    newSignal.Dispose();
                }
            }

            try
            {
                stopedSignal.Wait();
            }
            catch { }

            stopedSignal.Dispose();
        }

        public void Start<TFunc1, TFunc2>()
            where TFunc1 : new()
            where TFunc2 : new()
        {
            const string ThreadName = "Computer Thread";
            Console.WriteLine($"Current thread: ManagedThreadId={Thread.CurrentThread.ManagedThreadId}, Name=\"{Thread.CurrentThread.Name}\".");
            Console.WriteLine($"Renaming current thread...");
            Thread.CurrentThread.Name = ThreadName;

            int totalInvocations = 0;
            int statsPeriodInvocations = 0;
            double totalDurationMillisSum = 0.0;
            double statsPeriodDurationMillisSum = 0.0;
            ulong totalInnerIterations = 0;
            ulong statsPeriodInnerIterations = 0;

            DateTimeOffset statsPeriodStartTime, startTime;
            statsPeriodStartTime = startTime = DateTimeOffset.Now;

            TClass1 classParam1Instance = new TClass1();
            Console.WriteLine($"Type of classParam1-Instance: \"{classParam1Instance.GetType().FullName}\".");

            TClass2 classParam2Instance = new TClass2();
            Console.WriteLine($"Type of classParam2-Instance: \"{classParam2Instance.GetType().FullName}\".");

            TFunc1 funcParam1Instance = new TFunc1();
            Console.WriteLine($"Type of funcParam1-Instance: \"{funcParam1Instance.GetType().FullName}\".");

            TFunc2 funcParam2Instance = new TFunc2();
            Console.WriteLine($"Type of funcParam2-Instance: \"{funcParam2Instance.GetType().FullName}\".");

            ManualResetEventSlim stopedSignal = _stopedSignal;
            while (stopedSignal == null)
            {
                DoInvocations(ref totalInvocations, ref statsPeriodInvocations, ref totalDurationMillisSum, ref statsPeriodDurationMillisSum, ref totalInnerIterations, ref statsPeriodInnerIterations, ref statsPeriodStartTime, startTime);

                Thread.Yield();

                stopedSignal = _stopedSignal;
            }

            stopedSignal.Set();
        }

        public void Run<TFunc1, TFunc2>()
            where TFunc1 : new()
            where TFunc2 : new()
        {
            int totalInvocations = 0;
            int statsPeriodInvocations = 0;
            double totalDurationMillisSum = 0.0;
            double statsPeriodDurationMillisSum = 0.0;
            ulong totalInnerIterations = 0;
            ulong statsPeriodInnerIterations = 0;

            DateTimeOffset statsPeriodStartTime, startTime;
            statsPeriodStartTime = startTime = DateTimeOffset.Now;

            TClass1 classParam1Instance = new TClass1();
            Console.WriteLine($"Type of classParam1-Instance: \"{classParam1Instance.GetType().FullName}\".");

            TClass2 classParam2Instance = new TClass2();
            Console.WriteLine($"Type of classParam2-Instance: \"{classParam2Instance.GetType().FullName}\".");

            TFunc1 funcParam1Instance = new TFunc1();
            Console.WriteLine($"Type of funcParam1-Instance: \"{funcParam1Instance.GetType().FullName}\".");

            TFunc2 funcParam2Instance = new TFunc2();
            Console.WriteLine($"Type of funcParam2-Instance: \"{funcParam2Instance.GetType().FullName}\".");

            DoInvocations(ref totalInvocations, ref statsPeriodInvocations, ref totalDurationMillisSum, ref statsPeriodDurationMillisSum, ref totalInnerIterations, ref statsPeriodInnerIterations, ref statsPeriodStartTime, startTime);
        }

        protected virtual void Dispose(bool disposing)
        {
            AppDomain computationAppDomain = Interlocked.Exchange(ref _computationAppDomain, null);
            if (computationAppDomain != null)
            {
#if NETFRAMEWORK
                AppDomain.Unload(computationAppDomain);
#endif
            }
        }

        private void DoInvocations(ref int totalInvocations, ref int statsPeriodInvocations, ref double totalDurationMillisSum, ref double statsPeriodDurationMillisSum, ref ulong totalInnerIterations, ref ulong statsPeriodInnerIterations, ref DateTimeOffset statsPeriodStartTime, DateTimeOffset startTime)
        {
            DateTimeOffset invokeStart = DateTimeOffset.Now;
            ulong innerIterations = _nestedComputer.Compute(ThrowExceptionFromInnerCompute);
            DateTimeOffset invokeEnd = DateTimeOffset.Now;

            totalInvocations++;
            statsPeriodInvocations++;
            totalInnerIterations += innerIterations;
            statsPeriodInnerIterations += innerIterations;

            double durationMillis = (invokeEnd - invokeStart).TotalMilliseconds;
            totalDurationMillisSum += durationMillis;
            statsPeriodDurationMillisSum += durationMillis;

            TimeSpan statsPeriodRuntime = invokeEnd - statsPeriodStartTime;
            if (statsPeriodRuntime >= StatsPeriodDuration)
            {
                Console.WriteLine();
                Console.WriteLine("Latest stats period:");
                Console.WriteLine($"  Invocations:            {statsPeriodInvocations}.");
                Console.WriteLine($"  Time:                   {statsPeriodRuntime}.");
                Console.WriteLine($"  Mean invocatons/sec:    {statsPeriodInvocations / (statsPeriodRuntime).TotalSeconds}.");
                Console.WriteLine($"  Mean lattency:          {statsPeriodDurationMillisSum / statsPeriodInvocations} msecs.");
                Console.WriteLine($"  Mean inner iterations:  {statsPeriodInnerIterations / (double)statsPeriodInvocations}.");

                TimeSpan totalRuntime = invokeEnd - startTime;
                Console.WriteLine("Total:");
                Console.WriteLine($"  Invocations:            {totalInvocations}.");
                Console.WriteLine($"  Time:                   {totalRuntime}.");
                Console.WriteLine($"  Mean invocatons/sec:    {totalInvocations / (totalRuntime).TotalSeconds}.");
                Console.WriteLine($"  Mean lattency:          {totalDurationMillisSum / totalInvocations} msecs.");
                Console.WriteLine($"  Mean inner iterations:  {totalInnerIterations / (double)totalInvocations}.");
                Console.WriteLine();

                statsPeriodInvocations = 0;
                statsPeriodDurationMillisSum = 0.0;
                statsPeriodStartTime = invokeEnd;
            }
        }

        private class NestedComputer : MarshalByRefObject
        {
            private readonly Random _rnd = new Random();
            private readonly DoublyNestedComputer<TClass1, KeyValuePair<char, CancellationToken>> _computeInvoker = new DoublyNestedComputer<TClass1, KeyValuePair<char, CancellationToken>>();

            internal ulong Compute(bool throwExceptionFromInnerCompute)
            {
                ulong a = ComputeAsync(throwExceptionFromInnerCompute: false).GetAwaiter().GetResult();
                ulong b = _computeInvoker.InvokeComputeAsync<string>(throwExceptionFromInnerCompute, this).GetAwaiter().GetResult();
                return (a + b);
            }

            internal Task<ulong> ComputeAsync(bool throwExceptionFromInnerCompute)
            {
                if (throwExceptionFromInnerCompute)
                {
                    return ComputeCoreAsync(throwExceptionFromInnerCompute);
                }
                else
                {
                    try
                    {
                        return ComputeCoreAsync(throwExceptionFromInnerCompute);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                        return Task.FromResult(0UL);
                    }
                }
            }

            private Task<ulong> ComputeCoreAsync(bool throwExceptionFromInnerCompute)
            {
                string text = string.Empty;
                uint number = 0;

                ulong iterations = 0;

                // Hot spin:
                DateTimeOffset start = DateTimeOffset.Now;
                do
                {
                    if (throwExceptionFromInnerCompute)
                    {
                        throw new Exception("Bang!!!");
                    }

                    ++iterations;

                    uint n = (uint)_rnd.Next();
                    number ^= n;

                    if (text.Length > 0)
                    {
                        text += ", ";
                    }

                    text += $"({number}/{n})";
                }
                while (DateTimeOffset.Now - start < InvokeHotLoopDuration);

                if (text.Length < 1)
                {
                    Console.WriteLine("This will never happen, but now 'text' won't be oplimized away.");
                }

                return Task.FromResult(iterations);
            }

            private class DoublyNestedComputer<TGenParam1, TGenParam2>
            {
                public Task<ulong> InvokeComputeAsync<TGenParam3>(bool throwExceptionFromInnerCompute, NestedComputer actualComputer)
                {
                    if (actualComputer == null)
                    {
                        return Task.FromResult(0UL);
                    }

                    return actualComputer.ComputeAsync(throwExceptionFromInnerCompute);
                }
            }
        }
    }
}
