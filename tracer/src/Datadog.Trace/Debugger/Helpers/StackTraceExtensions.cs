// <copyright file="StackTraceExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

#nullable enable
namespace Datadog.Trace.Debugger.Helpers
{
    /// <summary>
    /// Provides extension methods for <see cref="StackTrace"/>.
    /// </summary>
    internal static class StackTraceExtensions
    {
        /// <summary>
        /// Produces an async-friendly readable representation of the stack trace.
        /// </summary>
        /// <remarks>
        /// The async-friendly formatting is archived by:
        /// * Skipping all awaiter frames (all methods in types implementing <see cref="INotifyCompletion"/>).
        /// * Inferring the original method name from the async state machine class (<see cref="IAsyncStateMachine"/>)
        ///   and removing the "MoveNext" - currently only for C#.
        /// * Adding the "async" prefix after "at" on each line for async invocations.
        /// * Appending "(?)" to the method signature to indicate that parameter information is missing.
        /// * Removing the "End of stack trace from previous location..." text.
        /// </remarks>
        /// <param name="stackFrames">The stack frames.</param>
        /// <returns>An async-friendly readable representation of the stack trace.</returns>
        public static IEnumerable<StackFrame> GetAsyncFriendlyFrameMethods(this IEnumerable<StackFrame?> stackFrames)
        {
            if (stackFrames == null)
            {
                yield break;
            }

            foreach (var frame in stackFrames)
            {
                if (frame == null)
                {
                    continue;
                }

                var method = frame.GetMethod();

                if (method == null)
                {
                    continue;
                }

                var declaringType = method.DeclaringType?.GetTypeInfo();
                // skip awaiters
                if (declaringType != null &&
                    (typeof(INotifyCompletion).GetTypeInfo().IsAssignableFrom(declaringType) ||
                     method.DeclaringType! == typeof(ExceptionDispatchInfo)))
                {
                    continue;
                }

                yield return frame;
            }
        }
    }
}
