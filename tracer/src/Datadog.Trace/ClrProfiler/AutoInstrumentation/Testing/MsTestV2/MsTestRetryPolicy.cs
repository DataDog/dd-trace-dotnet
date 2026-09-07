// <copyright file="MsTestRetryPolicy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Threading.Tasks;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

internal static class MsTestRetryPolicy
{
    private interface IRetryPolicy
    {
        object ExecuteAsync(object context);
    }

    public static object Wrap(object policy, Type retryBaseType)
    {
        var resultType = retryBaseType.Assembly.GetType("Microsoft.VisualStudio.TestTools.UnitTesting.RetryResult", throwOnError: true)!;
        var wrapper = Activator.CreateInstance(typeof(RetryPolicy<>).MakeGenericType(resultType), policy)!;
        return DuckType.CreateReverse(retryBaseType, wrapper);
    }

    private sealed class RetryPolicy<TResult>(object policy)
    {
        private readonly IRetryPolicy _policy = policy.DuckCast<IRetryPolicy>();

        [DuckReverseMethod(Name = "ExecuteAsync", ParameterTypeNames = ["Microsoft.VisualStudio.TestTools.UnitTesting.RetryContext"])]
        public Task<TResult> Execute(object context)
        {
            var execution = MsTestExecution.Current;
            var task = (Task<TResult>)_policy.ExecuteAsync(context);
            return execution is null ? task : CompleteAsync(task, execution);
        }

        private static async Task<TResult> CompleteAsync(Task<TResult> task, MsTestExecution execution)
        {
            // Await the original policy outside our error handler. Its exceptions and cancellation
            // belong to MSTest; errors in our instrumentation must not replace its result.
            var result = await task.ConfigureAwait(false);
            try
            {
                if (result.DuckCast<IRetryResult>()?.TryGetLast() is { Count: > 0 } results)
                {
                    await execution.ApplyDatadogRetriesAsync(results).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Common.Log.Error(ex, "MSTest: Error applying Datadog retries after the native retry policy.");
            }

            return result;
        }
    }
}
