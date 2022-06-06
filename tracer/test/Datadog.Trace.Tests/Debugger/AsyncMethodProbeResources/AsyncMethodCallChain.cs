// <copyright file="AsyncMethodCallChain.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Instrumentation;

namespace Datadog.Trace.Tests.Debugger.AsyncMethodProbeResources
{
    internal class AsyncMethodCallChain : IAsyncTestRun
    {
        private static readonly List<string> Snapshots = new();
        private int _chain = 0;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = ".")]
#pragma warning disable CS0414
#pragma warning disable CS0169
        private string _name;
#pragma warning restore CS0169
#pragma warning restore CS0414

        public List<string> Run()
        {
            Async1(this, _chain).GetAwaiter().GetResult();
            return Snapshots;
        }

        [AsyncStateMachine(typeof(GenAsync1Endd__3))]
        public Task<string> Async1(AsyncMethodCallChain chain, int i)
        {
            var state = AsyncMethodDebuggerInvoker.SetContext(this);
            GenAsync1Endd__3 stateMachine = default(GenAsync1Endd__3);
            stateMachine.GenEndt__builder = AsyncTaskMethodBuilder<string>.Create();
            stateMachine.GenEnd4__this = this;
            stateMachine.chain = chain;
            stateMachine.i = i;
            stateMachine.GenEnd1__state = -1;
            stateMachine.GenEndt__builder.Start(ref stateMachine);
            var task = stateMachine.GenEndt__builder.Task;
            var frames = new StackTrace(0, true).GetFrames() ?? Array.Empty<StackFrame>();
            task.ContinueWith(
                task1 =>
                {
                    AsyncMethodDebuggerInvoker.FinalizeSnapshot(ref state, task1, frames);
                    Snapshots.Add(state.SnapshotCreator.GetSnapshotJson());
                    AsyncMethodDebuggerInvoker.RestoreContext();
                },
                TaskContinuationOptions.ExecuteSynchronously);
            return task;
        }

        [AsyncStateMachine(typeof(GenAsync2Endd__4))]
        public Task<string> Async2(AsyncMethodCallChain chain)
        {
            var state = AsyncMethodDebuggerInvoker.SetContext(this);
            GenAsync2Endd__4 stateMachine = default(GenAsync2Endd__4);
            stateMachine.GenEndt__builder = AsyncTaskMethodBuilder<string>.Create();
            stateMachine.chain = chain;
            stateMachine.GenEnd1__state = -1;
            stateMachine.GenEndt__builder.Start(ref stateMachine);
            var task = stateMachine.GenEndt__builder.Task;
            var frames = new StackTrace(0, true).GetFrames() ?? Array.Empty<StackFrame>();
            task.ContinueWith(
                task1 =>
            {
                AsyncMethodDebuggerInvoker.FinalizeSnapshot(ref state, task1, frames);
                Snapshots.Add(state.SnapshotCreator.GetSnapshotJson());
                AsyncMethodDebuggerInvoker.RestoreContext();
            },
                TaskContinuationOptions.ExecuteSynchronously);
            return task;
        }

        /*
        public async Task<int> Async1(AsyncMethodCallChain chain, int i)
        {
            int counter1 = i + chain._chain;
            var result = await Async2(chain);
            return result + counter1;
        }

        public async Task<int> Async2(AsyncMethodCallChain chain)
        {
            var counter2 = chain._chain;
            return counter2;
        }
        */

        [StructLayout(LayoutKind.Auto)]
        [CompilerGenerated]
        private struct GenAsync1Endd__3 : IAsyncStateMachine
        {
            [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = ".")]
            public int GenEnd1__state;
            [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = ".")]
            public AsyncTaskMethodBuilder<string> GenEndt__builder;
            [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = ".")]
            public int i;
            [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = ".")]
            public AsyncMethodCallChain chain;
            [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = ".")]
            public AsyncMethodCallChain GenEnd4__this;
            [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1306:Field names should begin with lower-case letter", Justification = ".")]
            [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = ".")]
            private int Gencounter1End5__2;
            [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1306:Field names should begin with lower-case letter", Justification = ".")]
            [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = ".")]
            private TaskAwaiter<string> GenEndu__1;

            void IAsyncStateMachine.MoveNext()
            {
                this.MoveNext();
            }

            void IAsyncStateMachine.SetStateMachine(IAsyncStateMachine stateMachine)
            {
                this.SetStateMachine(stateMachine);
            }

            [DebuggerHidden]
            private void SetStateMachine(IAsyncStateMachine stateMachine)
            {
                GenEndt__builder.SetStateMachine(stateMachine);
            }

            private void MoveNext()
            {
                AsyncMethodDebuggerState state = new AsyncMethodDebuggerState();

                try
                {
                    // the generic type is GenParentEndd3<TV> but we are simulate the instrumentation behaviour of a generic struct method which the generic type is object
                    state = AsyncMethodDebuggerInvoker.BeginMethod(
                        "0",
                        (object)this,
                        MethodBase.GetCurrentMethod().MethodHandle,
                        GetType().TypeHandle,
                        0);
                }
                catch (Exception ex)
                {
                    AsyncMethodDebuggerInvoker.LogException(ex, ref state);
                    throw;
                }

                int num = GenEnd1__state;
                AsyncMethodCallChain asyncMethodCallChain = GenEnd4__this;
                string result = null;
                try
                {
                    TaskAwaiter<string> awaiter;
                    if (num != 0)
                    {
                        Gencounter1End5__2 = i + chain._chain;
                        awaiter = asyncMethodCallChain.Async2(chain).GetAwaiter();
                        if (!awaiter.IsCompleted)
                        {
                            num = (GenEnd1__state = 0);
                            GenEndu__1 = awaiter;
                            GenEndt__builder.AwaitUnsafeOnCompleted(ref awaiter, ref this);
                            return;
                        }
                    }
                    else
                    {
                        awaiter = GenEndu__1;
                        GenEndu__1 = default(TaskAwaiter<string>);
                        num = (GenEnd1__state = -1);
                    }

                    result = awaiter.GetResult();
                }
                catch (Exception exception)
                {
                    try
                    {
                        AsyncMethodDebuggerInvoker.EndMethod_StartMarker((object)this, exception, ref state);
                        AsyncMethodDebuggerInvoker.LogLocal(ref num, 0, ref state);
                        AsyncMethodDebuggerInvoker.LogLocal(ref asyncMethodCallChain, 1, ref state);
                        AsyncMethodDebuggerInvoker.LogLocal(ref result, 2, ref state);
                        // AsyncMethodDebuggerInvoker.LogLocal(ref awaiter, 3, ref state);
                        AsyncMethodDebuggerInvoker.LogLocal(ref exception, 4, ref state);
                        AsyncMethodDebuggerInvoker.EndMethod_EndMarker(ref state);
                        // AsyncMethodDebuggerInvoker<object>.RestoreContext();
                    }
                    catch (Exception ex)
                    {
                        AsyncMethodDebuggerInvoker.LogException(ex, ref state);
                        throw;
                    }

                    GenEnd1__state = -2;
                    GenEndt__builder.SetException(exception);
                    return;
                }

                try
                {
                    AsyncMethodDebuggerInvoker.EndMethod_StartMarker((object)this, result, null, ref state);
                    AsyncMethodDebuggerInvoker.LogLocal(ref num, 0, ref state);
                    AsyncMethodDebuggerInvoker.LogLocal(ref asyncMethodCallChain, 1, ref state);
                    AsyncMethodDebuggerInvoker.LogLocal(ref result, 2, ref state);
                    // AsyncMethodDebuggerInvoker.LogLocal(ref awaiter, 3, ref state);
                    // AsyncMethodDebuggerInvoker.LogLocal(ref exception, 4, ref state);
                    AsyncMethodDebuggerInvoker.EndMethod_EndMarker(ref state);
                    // AsyncMethodDebuggerInvoker<object>.RestoreContext();
                }
                catch (Exception ex)
                {
                    AsyncMethodDebuggerInvoker.LogException(ex, ref state);
                    throw;
                }

                GenEnd1__state = -2;
                GenEndt__builder.SetResult(result);
            }
        }

        [StructLayout(LayoutKind.Auto)]
        [CompilerGenerated]
        private struct GenAsync2Endd__4 : IAsyncStateMachine
        {
            [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = ".")]
            public int GenEnd1__state;
            [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = ".")]
            public AsyncTaskMethodBuilder<string> GenEndt__builder;
            [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = ".")]
            public AsyncMethodCallChain chain;

            void IAsyncStateMachine.MoveNext()
            {
                this.MoveNext();
            }

            void IAsyncStateMachine.SetStateMachine(IAsyncStateMachine stateMachine)
            {
                this.SetStateMachine(stateMachine);
            }

            [DebuggerHidden]
            private void SetStateMachine(IAsyncStateMachine stateMachine)
            {
                GenEndt__builder.SetStateMachine(stateMachine);
            }

            private void MoveNext()
            {
                AsyncMethodDebuggerState state = new AsyncMethodDebuggerState();

                try
                {
                    state = AsyncMethodDebuggerInvoker.BeginMethod(
                        "1",
                        this,
                        MethodBase.GetCurrentMethod().MethodHandle,
                        GetType().TypeHandle,
                        1);
                }
                catch (Exception ex)
                {
                    AsyncMethodDebuggerInvoker.LogException(ex, ref state);
                    throw;
                }

                int result = 0;
                try
                {
                    result = chain._chain;
                }
                catch (Exception exception)
                {
                    try
                    {
                        AsyncMethodDebuggerInvoker.EndMethod_StartMarker(this, exception, ref state);
                        AsyncMethodDebuggerInvoker.LogLocal(ref result, 0, ref state);
                        AsyncMethodDebuggerInvoker.LogLocal(ref exception, 1, ref state);
                        AsyncMethodDebuggerInvoker.EndMethod_EndMarker(ref state);
                        // AsyncMethodDebuggerInvoker<object>.RestoreContext();
                    }
                    catch (Exception ex)
                    {
                        AsyncMethodDebuggerInvoker.LogException(ex, ref state);
                        throw;
                    }

                    GenEnd1__state = -2;
                    GenEndt__builder.SetException(exception);
                    return;
                }

                try
                {
                    AsyncMethodDebuggerInvoker.EndMethod_StartMarker(this, null, ref state);
                    AsyncMethodDebuggerInvoker.LogLocal(ref result, 0, ref state);
                    // AsyncMethodDebuggerInvoker.LogLocal(ref exception, 1, ref state);
                    AsyncMethodDebuggerInvoker.EndMethod_EndMarker(ref state);
                    // AsyncMethodDebuggerInvoker<object>.RestoreContext();
                }
                catch (Exception ex)
                {
                    AsyncMethodDebuggerInvoker.LogException(ex, ref state);
                    throw;
                }

                GenEnd1__state = -2;
                GenEndt__builder.SetResult(result.ToString());
            }
        }
    }
}
