using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.ExceptionReplay
{
    [ExceptionReplayTestData(expectedNumberOfSnapshotsDefault: 5, expectedNumberOfSnaphotsFull: 7)]
    internal class DeterministicComplexExceptionPropagationTest : IAsyncRun
    {
        public async Task RunAsync()
        {
            try
            {
                await Task.Yield(); // Ensure we're running on a thread pool thread
                await InitiateComplexExceptionChain();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                throw;
            }
        }

        private async Task InitiateComplexExceptionChain()
        {
            try
            {
                await SimulateComplexAsyncOperations(5);
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Top-level exception in InitiateComplexExceptionChain", ex);
            }
        }

        private async Task SimulateComplexAsyncOperations(int depth)
        {
            if (depth <= 0)
            {
                throw new InvalidOperationException("Reached maximum depth");
            }

            await Task.Yield(); // Force continuation on a different thread

            try
            {
                var tasks = new List<Task>
                {
                    SimulateAsyncOperation($"Operation-{depth}-A", depth % 2 == 0),
                    SimulateAsyncOperation($"Operation-{depth}-B", depth % 3 == 0),
                    SimulateAsyncOperation($"Operation-{depth}-C", depth % 5 == 0)
                };

                await Task.WhenAll(tasks);

                await SimulateComplexAsyncOperations(depth - 1);
            }
            catch (Exception ex)
            {
                throw new CustomAggregateException($"Multiple exceptions at depth {depth}", GenerateInnerExceptions(ex, 3));
            }
        }

        private async Task SimulateAsyncOperation(string operationName, bool shouldThrow)
        {
            await Task.Yield(); // Force continuation on a different thread

            // Simulate some work
            await Task.Delay(30);

            if (shouldThrow)
            {
                throw new TimeoutException($"Operation {operationName} timed out");
            }
        }

        private IEnumerable<Exception> GenerateInnerExceptions(Exception originalException, int count)
        {
            yield return originalException;

            for (int i = 0; i < count - 1; i++)
            {
                yield return new Exception($"Additional inner exception {i + 1}",
                    new InvalidOperationException($"Nested invalid operation {i + 1}"));
            }
        }
    }

    public class CustomAggregateException : AggregateException
    {
        public CustomAggregateException(string message, IEnumerable<Exception> innerExceptions)
            : base(message, innerExceptions) { }

        public override string ToString()
        {
            return $"{Message}\n{string.Join("\n", InnerExceptions.Select(ex => ex.ToString()))}";
        }
    }
}
