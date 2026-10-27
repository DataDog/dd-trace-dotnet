# Kafka DSM throughput workload

By default, five workers each produce and consume 1,000 messages on one topic.
The benchmarking-platform runner measures this workload with DSM disabled and
with DSM enabled.

The additional high-cardinality workload uses the same five workers, each sending
10,000 messages round-robin across 400 topics: 2,000 topics and 50,000 messages
in total. This exceeds the 1,000-entry DSM tag cache capacity and revisits topics
after the cache fills. It has its own DSM-off baseline and DSM-on result, so its
broker and client overhead is not compared with the simpler workload.

| Environment variable | Default | Meaning |
| --- | --- | --- |
| `KAFKA_TOPIC` | `benchmark-topic` | Prefix for worker topic names |
| `NUM_THREADS` | `5` | Number of producer/consumer workers |
| `TOPICS_PER_THREAD` | `1` | Distinct topics per worker |
| `NUM_MESSAGES` | `1000` | Total messages per worker, distributed across its topics |

All numeric values must be positive, and `NUM_MESSAGES` must be at least
`TOPICS_PER_THREAD`. Kafka must be available at `localhost:9092`.

Build the sample, then run `Samples.KafkaBenchmark --setup` with the same environment
as the measured run. Setup creates single-partition topics in batches and must
finish before timing starts. Run the executable without arguments to measure it.
The multi-topic consumer starts at the offsets produced by the current run and
verifies the message count for every topic, so repeated measurements exercise
the whole working set instead of reading old data from only a few topics.

The runner is configured in the `dd-trace-dotnet/data-streams-monitoring` branch of
[benchmarking-platform](https://github.com/DataDog/benchmarking-platform/tree/dd-trace-dotnet/data-streams-monitoring).
Duration and committed-memory comparisons measure DSM overhead within a revision.
To quantify an improvement from a tracer change, run the same workload against
both tracer revisions; a DSM-on/off comparison alone does not establish it.
