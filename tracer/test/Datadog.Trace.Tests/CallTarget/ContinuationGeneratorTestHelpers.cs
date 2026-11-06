// <copyright file="ContinuationGeneratorTestHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.Tests.CallTarget
{
    /// <summary>
    /// Helpers shared by the continuation generator tests. The integrations stay in each test class,
    /// because a continuation generator binds its integration's callback once per closed generic type.
    /// </summary>
    internal static class ContinuationGeneratorTestHelpers
    {
        public static void Complete<T>(TaskCompletionSource<T> tcs, bool faulted)
        {
            if (faulted)
            {
                tcs.TrySetException(new CustomException("Internal Test Exception"));
            }
            else
            {
                tcs.TrySetResult(default);
            }
        }

        internal class CustomException : Exception
        {
            public CustomException(string message)
                : base(message)
            {
            }
        }

        internal class CustomCancellationException : OperationCanceledException
        {
            public CustomCancellationException(CancellationToken token)
                : base(token)
            {
            }
        }

        internal class CustomSynchronizationContext : SynchronizationContext
        {
            private readonly TaskCompletionSource<bool> _tcs = new();

            public Task Task => _tcs.Task;

            public override void Post(SendOrPostCallback d, object state)
            {
                _tcs.TrySetResult(true);
            }

            public override void Send(SendOrPostCallback d, object state)
            {
                _tcs.TrySetResult(true);
            }
        }

        internal class BubbleUpException : CallTargetBubbleUpException
        {
        }
    }
}
