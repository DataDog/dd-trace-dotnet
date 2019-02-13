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
            const int times = 5;
            const int tasksToRun = 1000;

            var duplicates = 0;
            var runsThatProducedDuplicates = 0;

            // The ThreadLocal version will produce duplicates on every execution and in every inner-run of this test on my machine (i.e. 100 times in 100 attempts)
            // var randomProvider = ThreadLocalNewRandomProvider.Instance;

            // The AsyncLocalGuidSeedRandomProvider version never produces duplicates, at least on my test runs...
            var randomProvider = AsyncLocalGuidSeedRandomProvider.Instance;

            // NOTE: Test should be left at this to ensure we're testing the implementation we actually use
            // var randomProvider = SimpleDependencyFactory.RandomProvider();

            for (var i = 0; i < times; i++)
            {
                var tasks = new Task[tasksToRun];
                var actions = new Action[tasksToRun];

                var randomGenerations = new ConcurrentDictionary<int, int>();

                for (var t = 0; t < tasksToRun; t++)
                {
                    var task = new Task(
                                        () =>
                                        {
                                            var myRandom = randomProvider.GetRandom();

                                            var myRandomId = myRandom.Next();

                                            randomGenerations.AddOrUpdate(
                                                                          myRandomId,
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
                                    MaxDegreeOfParallelism = 10
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

        [Fact]
        public void VerifyIdGenerationDoesNotProduceDuplicatesInConcurrentExecutions()
        {
            const int times = 5;
            const int tasksToRun = 1000;

            var duplicates = 0;
            var runsThatProducedDuplicates = 0;

            // Using a non-locked ID provider that uses a thread-safe random provider that basically creates a new Random with a 32-bit int
            // based seed that increments on each random-object creation.  The problem with this approach is 2-fold:
            //   1) Once the consumer produces more than ~4.3 billion IDs, new random object creations will start re-using the
            //      same seed over again, which will guarantee duplicate ID values generated.  Naturally it'll likely start occurring in increasing
            //      frequency much sooner than the max ID values.
            //   2) Given #1, which is specific to a single machine, the problem becomes compounded when performed this way across multiple machines
            // var idProvider = new RandomIdProvider(AsyncLocalGuidSeedRandomProvider.Instance);

            // Using a locked ID provider that uses a non-thread-safe random provider (which is basically a single random object) that is
            // interlocked on each access to provide thread-safety during random-id generation.  The downside here generally speaking is likely
            // poorer performance, depending on how heavily contented the lock is
            var idProvider = LockedRandomIdProvider.Instance;

            for (var i = 0; i < times; i++)
            {
                var tasks = new Task[tasksToRun];
                var actions = new Action[tasksToRun];

                var randomGenerations = new ConcurrentDictionary<ulong, int>();

                for (var t = 0; t < tasksToRun; t++)
                {
                    var task = new Task(
                                        () =>
                                        {
                                            var myRandomId = idProvider.GetUInt63Id();

                                            randomGenerations.AddOrUpdate(
                                                                          myRandomId,
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
                                    MaxDegreeOfParallelism = 15
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
