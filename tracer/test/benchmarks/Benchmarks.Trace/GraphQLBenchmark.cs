using System.Collections.Generic;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.Configuration;
using GraphQL;
using GraphQL.Execution;

namespace Benchmarks.Trace
{
    [MemoryDiagnoser]
    [BenchmarkAgent3]
    [BenchmarkCategory(Constants.TracerCategory)]
    public class GraphQLBenchmark
    {
        private static readonly Task<ExecutionResult> Result = Task.FromResult(new ExecutionResult { Value = 42 });
        private static readonly ExecutionContext Context = new ExecutionContext();
        private static readonly GraphQLClient _client = new GraphQLClient(Result);

        [GlobalSetup]
        public void GlobalSetup()
        {
            var settings = TracerSettings.Create(new() { { ConfigurationKeys.StartupDiagnosticLogEnabled, false } });

            Tracer.UnsafeSetTracerInstance(new Tracer(settings, new DummyAgentWriter(), null, null, null));

            // Warmup
            ExecuteAsync();
        }

        [Benchmark]
        public unsafe int ExecuteAsync()
        {
            var task = CallTarget.Run<Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.Net.ExecuteAsyncIntegration, GraphQLClient, ExecutionContext, Task<ExecutionResult>>(
                _client,
                Context,
                &ExecuteAsyncImpl);

            return task.GetAwaiter().GetResult().Value;

            static Task<ExecutionResult> ExecuteAsyncImpl(ExecutionContext context) => Result;
        }

        private class GraphQLClient : IExecutionStrategy
        {
            private readonly Task<ExecutionResult> _result;

            public GraphQLClient(Task<ExecutionResult> result)
            {
                _result = result;
            }

            public Task<ExecutionResult> ExecuteAsync(ExecutionContext context)
            {
                return _result;
            }
        }
    }
}

namespace GraphQL
{
    internal class ExecutionResult
    {
        public int Value { get; set; }
    }

    namespace Execution
    {
        internal interface IExecutionStrategy
        {
            Task<ExecutionResult> ExecuteAsync(ExecutionContext context);
        }

        internal class ExecutionContext
        {
            public DocumentImpl Document { get; } = new DocumentImpl();
            public OperationImpl Operation { get; } = new OperationImpl();
            public ErrorsImpl Errors { get; } = new ErrorsImpl();
        }

        internal class OperationImpl
        {
            public string Name { get; } = "OperationName";
            public OperationTypes OperationType { get; } = OperationTypes.Query;
        }

        internal class DocumentImpl
        {
            public string OriginalQuery { get; } = "Query";
        }

        internal class ErrorsImpl
        {
            int Count { get; } = 0;

            ExecutionErrorImpl this[int index] { 
                get {
                    return null;
                } 
            }
        }

        internal class ExecutionErrorImpl
        {
            string Code { get; }
            IEnumerable<object> Locations { get; }
            string Message { get; }
            IEnumerable<string> Path { get; }
        }

        public enum OperationTypes
        {
            Query,
            Mutation,
            Subscription
        }
    }
}
