// <copyright file="AsyncMethodDebuggerInvokerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Instrumentation;
using Xunit;

namespace Datadog.Trace.Tests.Debugger
{
    public class AsyncMethodDebuggerInvokerTests
    {
        public static List<string> Snapshots { get; } = new List<string>();

        [Fact]
        public void Test()
        {
            var t = new TestResource<string>();
            var task1 = t.Parent<string>("ss", 7, t); // parent call to another async method "child"
            var task2 = t.Recursive(3); // recursive async call
#if !NETFRAMEWORK
            var task3 = t.GetDataAsync<string>("ss", "vv"); // value task
            Task.WaitAll(task1, task2, task3.AsTask());
#else
            Task.WaitAll(task1, task2);
#endif
            Console.Read();
        }

        internal struct TestResource<T>
        {
            private readonly AsyncLocal<string> _asyncContext;

            public TestResource()
            {
                _asyncContext = new AsyncLocal<string>();
            }

            public Task Child()
            {
                GenChildEndd__5 stateMachine = new GenChildEndd__5();
                stateMachine.Gent__builder = AsyncTaskMethodBuilder.Create();
                stateMachine.Gen4__this = this;
                stateMachine.Gen1__state = -1;
                stateMachine.Gent__builder.Start(ref stateMachine);
                return stateMachine.Gent__builder.Task;

                /*
                 public async Task Child()
            {
                Console.WriteLine("Child " + "AsyncLocalValue = " + _asyncContext.Value);
                await Task.Delay(1000);
                _asyncContext.Value = "Child";
                await Task.Delay(1000);
                Console.WriteLine("Child " + "AsyncLocalValue = " + _asyncContext.Value);
            }
                 */
            }

            public Task Recursive(int j)
            {
                GenRecursiveEndd__6 stateMachine = new GenRecursiveEndd__6();
                stateMachine.Gent__builder = AsyncTaskMethodBuilder.Create();
                stateMachine.Gen4__this = this;
                stateMachine.j = j;
                stateMachine.Gen1__state = -1;
                stateMachine.Gent__builder.Start(ref stateMachine);
                return stateMachine.Gent__builder.Task;

                /*
                 public async Task Recursive(int j)
            {
                Console.WriteLine("Child " + "AsyncLocalValue = " + _asyncContext.Value);
                await Recursive(new Random().Next());
            }
                 */
            }

#if !NETFRAMEWORK
            public ValueTask<T> GetDataAsync<TV>(T ss, TV v)
            {
                GenGetDataAsyncEndd__4<TV> stateMachine = new GenGetDataAsyncEndd__4<TV>();
                stateMachine.Gent__builder = AsyncValueTaskMethodBuilder<T>.Create();
                stateMachine.Gen4__this = this;
                stateMachine.ss = ss;
                stateMachine.v = v;
                stateMachine.Gen1__state = -1;
                stateMachine.Gent__builder.Start(ref stateMachine);
                return stateMachine.Gent__builder.Task;

                /*
                 public async ValueTask<T> GetDataAsync<V>(T ss, V v)
                {
                var value = ss ?? (object)v;
                await Task.Delay(100);
                return (T)value;
                }
                */
            }
#endif

            public Task Parent<TV>(TV v, int i, TestResource<T> t)
            {
                GenParentEndd3<TV> stateMachine = new GenParentEndd3<TV>();
                stateMachine.Gent__builder = AsyncTaskMethodBuilder.Create();
                stateMachine.Gen4__this = this;
                stateMachine.v = v;
                stateMachine.i = i;
                stateMachine.t = t;
                stateMachine.Gen1__state = -1;
                stateMachine.Gent__builder.Start(ref stateMachine);
                return stateMachine.Gent__builder.Task;
                /* _asyncContext.Value = "Parent";
                Console.WriteLine("Parent " + "AsyncLocalValue = " + _asyncContext.Value);
                await Child();
                Console.WriteLine("Parent " + "AsyncLocalValue = " + _asyncContext.Value);
                */
            }

            private struct GenParentEndd3<TV> : IAsyncStateMachine
            {
                #region
                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = ".")]
                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1124:Do not use regions", Justification = ".")]
                public int Gen1__state;

                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = ".")]
                public AsyncTaskMethodBuilder Gent__builder;

                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = ".")]
                public TV v;

                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = ".")]
                public int i;

                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = ".")]
                public TestResource<T> t;

                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = ".")]
                public TestResource<T> Gen4__this;

                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1306:Field names should begin with lower-case letter", Justification = ".")]
                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = ".")]
                private TaskAwaiter Genu__1;

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
                }

                #endregion

                private void MoveNext()
                {
                    AsyncMethodDebuggerState state = AsyncMethodDebuggerState.GetDefault();

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

                    int num = Gen1__state;
                    try
                    {
                        TaskAwaiter awaiter;
                        if (num != 0)
                        {
                            Gen4__this._asyncContext.Value = "Parent";
                            Console.WriteLine("Parent AsyncLocalValue = " + Gen4__this._asyncContext.Value);
                            awaiter = Gen4__this.Child().GetAwaiter();
                            if (!awaiter.IsCompleted)
                            {
                                num = (Gen1__state = 0);
                                Genu__1 = awaiter;
                                GenParentEndd3<TV> stateMachine = this;
                                Gent__builder.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);
                                return;
                            }
                        }
                        else
                        {
                            awaiter = Genu__1;
                            Genu__1 = default(TaskAwaiter);
                            num = (Gen1__state = -1);
                        }

                        awaiter.GetResult();
                        Console.WriteLine("Parent AsyncLocalValue = " + Gen4__this._asyncContext.Value);
                        Console.WriteLine("V-I-T " + v + i + t);
                    }
                    catch (Exception exception)
                    {
                        try
                        {
                            AsyncMethodDebuggerInvoker.EndMethod_StartMarker((object)this, exception, ref state);
                            AsyncMethodDebuggerInvoker.LogLocal(ref num, 0, ref state);
                            AsyncMethodDebuggerInvoker.EndMethod_EndMarker(ref state);
                        }
                        catch (Exception ex)
                        {
                            AsyncMethodDebuggerInvoker.LogException(ex, ref state);
                            throw;
                        }

                        Gen1__state = -2;
                        Gent__builder.SetException(exception);
                        return;
                    }

                    try
                    {
                        AsyncMethodDebuggerInvoker.EndMethod_StartMarker((object)this, null, ref state);
                        AsyncMethodDebuggerInvoker.LogLocal(ref num, 0, ref state);
                        AsyncMethodDebuggerInvoker.EndMethod_EndMarker(ref state);
                    }
                    catch (Exception ex)
                    {
                        AsyncMethodDebuggerInvoker.LogException(ex, ref state);
                        throw;
                    }

                    Gen1__state = -2;
                    Gent__builder.SetResult();
                }
            }

            private sealed class GenChildEndd__5 : IAsyncStateMachine
            {
                #region
                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = ".")]
                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = ".")]
                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1124:Do not use regions", Justification = ".")]
                public int Gen1__state;
                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = ".")]
                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = ".")]
                public AsyncTaskMethodBuilder Gent__builder;
                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = ".")]
                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = ".")]
                public TestResource<T> Gen4__this = default;
                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = ".")]
                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1306:Field names should begin with lower-case letter", Justification = ".")]
                private TaskAwaiter Genu__1;

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
                }
                #endregion

                private void MoveNext()
                {
                    AsyncMethodDebuggerState state = AsyncMethodDebuggerState.GetDefault();

                    try
                    {
                        state = AsyncMethodDebuggerInvoker.BeginMethod(
                            "0",
                            this,
                            MethodBase.GetCurrentMethod().MethodHandle,
                            GetType().TypeHandle,
                            0);
                    }
                    catch (Exception ex)
                    {
                        AsyncMethodDebuggerInvoker.LogException(ex, ref state);
                        throw;
                    }

                    int num = Gen1__state;
                    try
                    {
                        TaskAwaiter awaiter;
                        TaskAwaiter awaiter2;
                        if (num != 0)
                        {
                            if (num == 1)
                            {
                                awaiter = Genu__1;

                                Genu__1 = default(TaskAwaiter);
                                num = (Gen1__state = -1);
                                goto IL_010d;
                            }

                            Console.WriteLine("Child AsyncLocalValue = " + Gen4__this._asyncContext.Value);
                            awaiter2 = Task.Delay(1000).GetAwaiter();
                            if (!awaiter2.IsCompleted)
                            {
                                num = (Gen1__state = 0);
                                Genu__1 = awaiter2;
                                GenChildEndd__5 stateMachine = this;
                                Gent__builder.AwaitUnsafeOnCompleted(ref awaiter2, ref stateMachine);
                                return;
                            }
                        }
                        else
                        {
                            awaiter2 = Genu__1;

                            Genu__1 = default(TaskAwaiter);
                            num = (Gen1__state = -1);
                        }

                        awaiter2.GetResult();

                        Gen4__this._asyncContext.Value = "Child";
                        awaiter = Task.Delay(1000).GetAwaiter();
                        if (!awaiter.IsCompleted)
                        {
                            num = (Gen1__state = 1);
                            Genu__1 = awaiter;
                            GenChildEndd__5 stateMachine = this;
                            Gent__builder.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);
                            return;
                        }

                        goto IL_010d;
                    IL_010d:
                        awaiter.GetResult();
                        Console.WriteLine("Child AsyncLocalValue = " + Gen4__this._asyncContext.Value);
                    }
                    catch (Exception exception)
                    {
                        try
                        {
                            AsyncMethodDebuggerInvoker.EndMethod_StartMarker(this, exception, ref state);
                            AsyncMethodDebuggerInvoker.LogLocal(ref num, 0, ref state);
                            AsyncMethodDebuggerInvoker.EndMethod_EndMarker(ref state);
                        }
                        catch (Exception ex)
                        {
                            AsyncMethodDebuggerInvoker.LogException(ex, ref state);
                            throw;
                        }

                        Gen1__state = -2;
                        Gent__builder.SetException(exception);
                        return;
                    }

                    try
                    {
                        AsyncMethodDebuggerInvoker.EndMethod_StartMarker(this, null, ref state);
                        AsyncMethodDebuggerInvoker.LogLocal(ref num, 0, ref state);
                        AsyncMethodDebuggerInvoker.EndMethod_EndMarker(ref state);
                    }
                    catch (Exception ex)
                    {
                        AsyncMethodDebuggerInvoker.LogException(ex, ref state);
                        throw;
                    }

                    Gen1__state = -2;
                    Gent__builder.SetResult();
                }
            }

            private sealed class GenGetDataAsyncEndd__4<TV> : IAsyncStateMachine
            {
                #region
                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = ".")]
                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = ".")]
                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1124:Do not use regions", Justification = ".")]
                public int Gen1__state;

                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = ".")]
                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = ".")]
                public AsyncValueTaskMethodBuilder<T> Gent__builder;

                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = ".")]
                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = ".")]
                public T ss = default;

                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = ".")]
                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = ".")]
                public TV v = default;

                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = ".")]
                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = ".")]
                public TestResource<T> Gen4__this = default;

                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = ".")]
                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1306:Field names should begin with lower-case letter", Justification = ".")]
                private object GenvalueEnd5__1;

                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = ".")]
                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1306:Field names should begin with lower-case letter", Justification = ".")]
                private TaskAwaiter Genu__1;

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
                }
                #endregion

                private void MoveNext()
                {
                    AsyncMethodDebuggerState state = AsyncMethodDebuggerState.GetDefault();
                    try
                    {
                        state = AsyncMethodDebuggerInvoker.BeginMethod<object>(
                            "0",
                            this,
                            MethodBase.GetCurrentMethod().MethodHandle,
                            GetType().TypeHandle,
                            0);
                    }
                    catch (Exception ex)
                    {
                        AsyncMethodDebuggerInvoker.LogException(ex, ref state);
                        throw;
                    }

                    int num = Gen1__state;
                    T result;
                    try
                    {
                        TaskAwaiter awaiter;
                        if (num != 0)
                        {
                            T val = ss;

                            GenvalueEnd5__1 = ((val != null) ? ((object)val) : ((object)v));
                            awaiter = Task.Delay(100).GetAwaiter();
                            if (!awaiter.IsCompleted)
                            {
                                num = (Gen1__state = 0);
                                Genu__1 = awaiter;
                                GenGetDataAsyncEndd__4<TV> stateMachine = this;
                                Gent__builder.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);
                                return;
                            }
                        }
                        else
                        {
                            awaiter = Genu__1;
                            Genu__1 = default(TaskAwaiter);
                            num = (Gen1__state = -1);
                        }

                        awaiter.GetResult();
                        Console.WriteLine(ss.ToString() + v.ToString() + Gen4__this);
                        result = (T)GenvalueEnd5__1;
                    }
                    catch (Exception exception)
                    {
                        try
                        {
                            AsyncMethodDebuggerInvoker.EndMethod_StartMarker((object)this, exception, ref state);
                            AsyncMethodDebuggerInvoker.LogLocal(ref num, 0, ref state);
                            AsyncMethodDebuggerInvoker.EndMethod_EndMarker(ref state);
                        }
                        catch (Exception ex)
                        {
                            AsyncMethodDebuggerInvoker.LogException(ex, ref state);
                            throw;
                        }

                        Gen1__state = -2;
                        GenvalueEnd5__1 = null;
                        Gent__builder.SetException(exception);
                        return;
                    }

                    try
                    {
                        AsyncMethodDebuggerInvoker.EndMethod_StartMarker((object)this, result, null, ref state);
                        AsyncMethodDebuggerInvoker.LogLocal(ref num, 0, ref state);
                        AsyncMethodDebuggerInvoker.LogLocal(ref result, 1, ref state);
                        AsyncMethodDebuggerInvoker.EndMethod_EndMarker(ref state);
                    }
                    catch (Exception ex)
                    {
                        AsyncMethodDebuggerInvoker.LogException(ex, ref state);
                        throw;
                    }

                    Gen1__state = -2;
                    GenvalueEnd5__1 = null;
                    Gent__builder.SetResult(result);
                }
            }

            private sealed class GenRecursiveEndd__6 : IAsyncStateMachine
            {
                #region
                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1124:Do not use regions", Justification = ".")]
                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = ".")]
                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = ".")]
                public int Gen1__state;

                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = ".")]
                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = ".")]
                public AsyncTaskMethodBuilder Gent__builder;

                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = ".")]
                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = ".")]
                public int j = 0;

                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = ".")]
                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = ".")]
                public TestResource<T> Gen4__this;

                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = ".")]
                [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1306:Field names should begin with lower-case letter", Justification = ".")]
                private TaskAwaiter Genu__1;

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
                }
                #endregion

                private void MoveNext()
                {
                    AsyncMethodDebuggerState state = AsyncMethodDebuggerState.GetDefault();
                    try
                    {
                        state = AsyncMethodDebuggerInvoker.BeginMethod<object>(
                            "0",
                            this,
                            MethodBase.GetCurrentMethod().MethodHandle,
                            GetType().TypeHandle,
                            0);
                    }
                    catch (Exception ex)
                    {
                        AsyncMethodDebuggerInvoker.LogException(ex, ref state);
                        throw;
                    }

                    int num = Gen1__state;
                    try
                    {
                        TaskAwaiter awaiter;
                        if (num == 0)
                        {
                            awaiter = Genu__1;

                            Genu__1 = default(TaskAwaiter);
                            num = (Gen1__state = -1);
                            goto IL_0084;
                        }

                        Console.WriteLine("Child AsyncLocalValue = ");
                        if (j < 2)
                        {
                            awaiter = Gen4__this.Recursive(j).GetAwaiter();
                            if (!awaiter.IsCompleted)
                            {
                                num = (Gen1__state = 0);

                                Genu__1 = awaiter;
                                GenRecursiveEndd__6 stateMachine = this;
                                Gent__builder.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);
                                return;
                            }

                            goto IL_0084;
                        }

                        goto end_IL_0007;
                    IL_0084:
                        awaiter.GetResult();
#pragma warning disable SA1024 // Colons Should Be Spaced Correctly
                    end_IL_0007:;
#pragma warning restore SA1024 // Colons Should Be Spaced Correctly
                    }
                    catch (Exception exception)
                    {
                        try
                        {
                            AsyncMethodDebuggerInvoker.EndMethod_StartMarker((object)this, exception, ref state);
                            AsyncMethodDebuggerInvoker.LogLocal(ref num, 0, ref state);
                            AsyncMethodDebuggerInvoker.EndMethod_EndMarker(ref state);
                        }
                        catch (Exception ex)
                        {
                            AsyncMethodDebuggerInvoker.LogException(ex, ref state);
                            throw;
                        }

                        Gen1__state = -2;
                        Gent__builder.SetException(exception);
                        return;
                    }

                    try
                    {
                        AsyncMethodDebuggerInvoker.EndMethod_StartMarker((object)this, null, ref state);
                        AsyncMethodDebuggerInvoker.LogLocal(ref num, 0, ref state);
                        AsyncMethodDebuggerInvoker.EndMethod_EndMarker(ref state);
                        // AsyncMethodDebuggerInvokerTests.Snapshots.Add(state.SnapshotCreator.GetSnapshotJson());
                    }
                    catch (Exception ex)
                    {
                        AsyncMethodDebuggerInvoker.LogException(ex, ref state);
                        throw;
                    }

                    Gen1__state = -2;
                    Gent__builder.SetResult();
                }
            }
        }
    }
}
