# Messaging Instrumentation

Messaging integrations provide observability into message production and consumption for queues, topics, streaming platforms, and job queues.

## What to Trace

### Producer Operations (Critical)
Operations that send messages to a broker or queue:
- Send methods: `send()`, `publish()`, `produce()`
- Job creation: `add()`, `addJob()`, `enqueue()`
- Batch send: `sendBatch()`, `publishBatch()`, `addBulk()`

### Consumer Operations (Critical)
The **invocation** of message handlers - where each message is actually processed:
- The internal method that calls the user's handler per-message
- NOT the registration/subscription method

**Key distinction**: Trace WHERE the callback is invoked, not where it's registered.

```
WRONG:  consumer.subscribe({ topics: ['orders'] })  // Just stores subscription
WRONG:  worker.process(handler)                      // Just registers handler

RIGHT:  Internal processJob() that calls handler     // Per-message invocation
RIGHT:  Internal dispatch that runs eachMessage()    // Per-message invocation
```

### Settlement Operations
- Acknowledgments: `ack()`, `commit()`
- Rejections: `nack()`, `reject()`

## What to Skip

### Connection & Setup
- Connection establishment: `connect()`, `createConnection()`
- Client/producer/consumer creation

### Administration
- Topic/queue creation: `createTopic()`, `createQueue()`
- Consumer group management: `subscribe()`, `join()`
- Queue configuration

### Internal Mechanics
- Messages pre-fetched or cached by the library (until forwarded to caller)
- Offset management (metadata, not message processing)
- Internal heartbeats

## Context Propagation

**Critical**: Messaging requires bidirectional context propagation to link producer and consumer spans across service boundaries.

### Producer Side
Inject trace context into outgoing message headers/attributes before sending.

### Consumer Side
Extract trace context from incoming message to create child span linked to producer.

This enables end-to-end distributed tracing across async message flows.

## Job Queue Patterns

Job queues (Bull, BullMQ, Bee-Queue) follow messaging patterns:

**Producer**: `queue.add(jobData)` - creates job
**Consumer**: Internal method that invokes the job handler per-job

To find the consumer instrumentation point:
1. Find where handler is stored: `this.processFn = handler`
2. Search for invocation: `await this.processFn(job)`
3. That call site has per-job context and is the correct target
