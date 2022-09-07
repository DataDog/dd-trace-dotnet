using Datadog.Trace.Debugger.Instrumentation;

namespace Samples.Probes.SmokeTests
{
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;

    namespace Samples.Probes.SmokeTests
    {
        internal class AsyncCallChain
        {
            public Task<int> Async1(int chain)
            {
                AsyncCallChain.AAsync1Zd__2 Async1d__ = new AsyncCallChain.AAsync1Zd__2();
                Async1d__.SSt__builder = AsyncTaskMethodBuilder<int>.Create();
                Async1d__.SS4__this = this;
                Async1d__.chain = chain;
                Async1d__.SS1__state = -1;
                Async1d__.SSt__builder.Start<AsyncCallChain.AAsync1Zd__2>(ref Async1d__);
                return Async1d__.SSt__builder.Task;
            }

            public Task<int> Async2(int chain)
            {
                return Task.FromResult(1);
            }

            private int _chain;

            [CompilerGenerated]
            private sealed class AAsync1Zd__2 : IAsyncStateMachine
            {
                public AAsync1Zd__2()
                {
                }

                void IAsyncStateMachine.MoveNext()
                {
                    int defaultValue = MethodDebuggerInvoker.GetDefaultValue<int>();
                    DebuggerReturn<int> @default = DebuggerReturn<int>.GetDefault();
                    int num = this.SS1__state;
                    int result;
                    try
                    {
                        TaskAwaiter<int> awaiter;
                        if (num != 0)
                        {
                            int num2 = this.chain;
                            this.chain = num2 + 1;
                            LineDebuggerState lineDebuggerState = default;
                            
                            try
                            {
                                lineDebuggerState = LineDebuggerInvoker.BeginLine<AsyncCallChain.AAsync1Zd__2>("8286d046-9740-a3e4-95cf-ff46699c73c4", this, new RuntimeMethodHandle(), typeof(AsyncCallChain.AAsync1Zd__2).TypeHandle, 0, 23, "C:\\dev\\Datadog\\dd-trace-dotnet\\tracer\\test\\test-applications\\debugger\\Samples.Probes\\SmokeTests\\AsyncCallChain.cs");
                                LineDebuggerInvoker.LogLocal<int>(ref num, 0, ref lineDebuggerState);
                                LineDebuggerInvoker.LogLocal<int>(ref result, 1, ref lineDebuggerState);
                                LineDebuggerInvoker.LogLocal<int>(ref num2, 2, ref lineDebuggerState);
                                LineDebuggerInvoker.LogLocal<TaskAwaiter<int>>(ref awaiter, 3, ref lineDebuggerState);
                                AsyncCallChain.AAsync1Zd__2 d__;
                                LineDebuggerInvoker.LogLocal<AsyncCallChain.AAsync1Zd__2>(ref d__, 4, ref lineDebuggerState);
                                Exception exception;
                                LineDebuggerInvoker.LogLocal<Exception>(ref exception, 5, ref lineDebuggerState);
                                LineDebuggerInvoker.EndLine(ref lineDebuggerState);
                            }
                            catch (Exception exception2)
                            {
                                LineDebuggerInvoker.LogException<AsyncCallChain.AAsync1Zd__2>(exception2, lineDebuggerState);
                            }

                            awaiter = this.SS4__this.Async2(this.chain).GetAwaiter();
                            if (!awaiter.IsCompleted)
                            {
                                num = (this.SS1__state = 0);
                                this.SSu__1 = awaiter;
                                AsyncCallChain.AAsync1Zd__2 d__ = this;
                                this.SSt__builder.AwaitUnsafeOnCompleted<TaskAwaiter<int>, AsyncCallChain.AAsync1Zd__2>(ref awaiter, ref d__);
                                return;
                            }
                        }
                        else
                        {
                            awaiter = this.SSu__1;
                            this.SSu__1 = default(TaskAwaiter<int>);
                            num = (this.SS1__state = -1);
                        }
                        this.SSs__2 = awaiter.GetResult();
                        this.AresultZ5__1 = this.SSs__2;
                        result = this.AresultZ5__1;
                    }
                    catch (Exception exception)
                    {
                        this.SS1__state = -2;
                        // Exception exception;
                        this.SSt__builder.SetException(exception);
                        return;
                    }
                    this.SS1__state = -2;
                    this.SSt__builder.SetResult(result);
                }

                // Token: 0x0600007D RID: 125 RVA: 0x0000222F File Offset: 0x0000042F
                [DebuggerHidden]
                void IAsyncStateMachine.SetStateMachine(IAsyncStateMachine stateMachine)
                {
                }

                // Token: 0x04000061 RID: 97
                public int SS1__state;

                // Token: 0x04000062 RID: 98
                public AsyncTaskMethodBuilder<int> SSt__builder;

                // Token: 0x04000063 RID: 99
                public int chain;

                // Token: 0x04000064 RID: 100
                public AsyncCallChain SS4__this;

                // Token: 0x04000065 RID: 101
                private int AresultZ5__1;

                // Token: 0x04000066 RID: 102
                private int SSs__2;

                // Token: 0x04000067 RID: 103
                private TaskAwaiter<int> SSu__1;
            }

        }
    }
}
