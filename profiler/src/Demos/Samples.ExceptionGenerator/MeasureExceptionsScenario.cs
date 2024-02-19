// <copyright file="MeasureExceptionsScenario.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

namespace Samples.ExceptionGenerator
{
    internal class MeasureExceptionsScenario
    {
        private const int ExceptionsToThrowCount = 100_000;

        public void Run()
        {
            // throw random exceptions
            List<ExceptionStats> exceptions = Initialize();
            var maxExceptionIndex = exceptions.Count;
            Random r = new Random(DateTime.Now.Millisecond);

            for (int i = 0; i < ExceptionsToThrowCount; i++)
            {
                int index = r.Next(maxExceptionIndex);
                exceptions[index].Count++;
                exceptions[index].Thrower();
            }

            DumpExceptions(exceptions);
        }

        private List<ExceptionStats> Initialize()
        {
            List<ExceptionStats> exceptions = new List<ExceptionStats>()
            {
                new ExceptionStats()
                {
                    Count = 0,
                    Name = nameof(ArgumentException),
                    Thrower = ThrowArgument
                },
                new ExceptionStats()
                {
                    Count = 0,
                    Name = nameof(SystemException),
                    Thrower = ThrowSystem
                },
                new ExceptionStats()
                {
                    Count = 0,
                    Name = nameof(InvalidOperationException),
                    Thrower = ThrowInvalidOperation
                },
                new ExceptionStats()
                {
                    Count = 0,
                    Name = nameof(InvalidCastException),
                    Thrower = ThrowInvalidCast
                },
                new ExceptionStats()
                {
                    Count = 0,
                    Name = nameof(TimeoutException),
                    Thrower = ThrowTimeout
                },
                new ExceptionStats()
                {
                    Count = 0,
                    Name = nameof(BadImageFormatException),
                    Thrower = ThrowBadImageFormat
                },
                new ExceptionStats()
                {
                    Count = 0,
                    Name = nameof(NotImplementedException),
                    Thrower = ThrowNotImplemented
                },
                new ExceptionStats()
                {
                    Count = 0,
                    Name = nameof(ArithmeticException),
                    Thrower = ThrowArithmetic
                },
                new ExceptionStats()
                {
                    Count = 0,
                    Name = nameof(IndexOutOfRangeException),
                    Thrower = ThrowIndexOutOfRange
                },
                new ExceptionStats()
                {
                    Count = 0,
                    Name = nameof(NotSupportedException),
                    Thrower = ThrowNotSupported
                },
                new ExceptionStats()
                {
                    Count = 0,
                    Name = nameof(RankException),
                    Thrower = ThrowRank
                },
                new ExceptionStats()
                {
                    Count = 0,
                    Name = nameof(UnauthorizedAccessException),
                    Thrower = ThrowUnauthorizedAccess
                },
            };

            return exceptions;
        }

        private void ThrowUnauthorizedAccess()
        {
            try { throw new UnauthorizedAccessException(); } catch { }
        }

        private void ThrowRank()
        {
            try { throw new RankException(); } catch { }
        }

        private void ThrowNotSupported()
        {
            try { throw new NotSupportedException("Too heavy"); } catch { }
        }

        private void ThrowIndexOutOfRange()
        {
            try { throw new IndexOutOfRangeException("Not in range"); } catch { }
        }

        private void ThrowArithmetic()
        {
            try { throw new ArithmeticException("don't know how to count"); } catch { }
        }

        private void ThrowNotImplemented()
        {
            try { throw new NotImplementedException(); } catch { }
        }

        private void ThrowBadImageFormat()
        {
            try { throw new BadImageFormatException("Baaad format", "foo.dll"); } catch { }
        }

        private void ThrowArgument()
        {
            try { throw new ArgumentException(); } catch { }
        }

        private void ThrowSystem()
        {
            try { throw new SystemException(); } catch { }
        }

        private void ThrowInvalidOperation()
        {
            try { throw new InvalidOperationException(); } catch { }
        }

        private void ThrowInvalidCast()
        {
            try { throw new InvalidCastException(); } catch { }
        }

        private void ThrowTimeout()
        {
            try { throw new TimeoutException(); } catch { }
        }

        private void DumpExceptions(IEnumerable<ExceptionStats> exceptions)
        {
            Console.WriteLine("Exceptions start");
            foreach (var exception in exceptions)
            {
                Console.WriteLine($"{exception.Name}={exception.Count}");
            }

            Console.WriteLine("Exceptions end");
            Console.WriteLine();
        }

#pragma warning disable SA1401 // Fields should be private
        internal class ExceptionStats
        {
            public string Name;

            public int Count;

            public Action Thrower;
        }
#pragma warning restore SA1401 // Fields should be private
    }
}
