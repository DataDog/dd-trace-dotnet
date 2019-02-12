using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Services;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class RandomProviderTests
    {
        [Fact]
        public void VerifyRandomProviderDoesNotProduceDuplicatesInConcurrentExecutions()
        {
            const int times = 100;
            const int threads = 5;

            var duplicates = 0;
            var runsThatProducedDuplicates = 0;

            // The ThreadLocal version will produce duplicates on every execution and in every inner-run of this test on my machine (i.e. 100 times in 100 attempts)
            // var randomProvider = ThreadLocalNewRandomProvider.Instance;

            // The AsyncLocalGuidSeedRandomProvider version never produces duplicates, at least on my test runs...
            var randomProvider = AsyncLocalGuidSeedRandomProvider.Instance;

            // NOTE: Test should be changed to this once we pick the method to use
            // var randomProvider = SimpleDependencyFactory.RandomProvider();

            for (var i = 0; i < times; i++)
            {
                var tasks = new Task[threads];
                var actions = new Action[threads];

                var randomGenerations = new ConcurrentDictionary<int, int>();

                for (var t = 0; t < threads; t++)
                {
                    var task = new Task(
                                        () =>
                                        {
                                            var myRandom = randomProvider.GetRandom().Next();

                                            randomGenerations.AddOrUpdate(
                                                                          myRandom,
                                                                          ri => 1,
                                                                          (k, ri) => ++ri);
                                        },
                                        TaskCreationOptions.LongRunning);

                    tasks[t] = task;
                    actions[t] = () => task.Start();
                }

                Parallel.Invoke(
                                new ParallelOptions
                                {
                                    MaxDegreeOfParallelism = threads * 2
                                },
                                actions);

                Task.WaitAll(tasks);

                var thisRunDuplicates = randomGenerations.Where(r => r.Value > 1)
                                                         .Sum(r => r.Value - 1);

                if (thisRunDuplicates <= 0)
                {
                    continue;
                }

                runsThatProducedDuplicates++;
                duplicates += thisRunDuplicates;
            }

            Assert.True(runsThatProducedDuplicates == 0, $"Random generation method produced [{duplicates}] total duplicates in [{runsThatProducedDuplicates}] distinct runs over a total of [{times}] runs/attempts.");
        }
    }
}
