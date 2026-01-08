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
    [BenchmarkCategory(Constants.TracerCategory, Constants.RunOnPrs, Constants.RunOnMaster)]
    public class GraphQLBenchmark
    {
        private readonly static Task<ExecutionResult> _result = Task.FromResult(new ExecutionResult { Value = 42 });
        private ExecutionContext _context;
        private GraphQLClient _client;

        [GlobalSetup]
        public void GlobalSetup()
        {
            TracerHelper.SetGlobalTracer();

            _context = new ExecutionContext();
            _client = new GraphQLClient(_result);
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            TracerHelper.CleanupGlobalTracer();
        }

        [Benchmark]
        public unsafe int ExecuteAsync()
        {
            var task = CallTarget.Run<Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.Net.ExecuteAsyncIntegration, GraphQLClient, ExecutionContext, Task<ExecutionResult>>(
                _client,
                _context,
                &ExecuteAsyncImpl);

            return task.GetAwaiter().GetResult().Value;

            static Task<ExecutionResult> ExecuteAsyncImpl(ExecutionContext context) => _result;
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
