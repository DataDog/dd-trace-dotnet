## Instrumentation behaviors
Before reading on, make sure you understand the current instrumentation behavior to make sense of the generated traces from `Azure.Messaging.ServiceBus`:

### ServiceBusSender.SendMessageAsync and ServiceBusSender.SendMessagesAsync
When this API is called, the following diagnostic activity occurs:
1. A `Message` Activity is started, whose TraceId/SpanId is injected into the Azure Service Bus message's properties `Diagnostic-Id` and `traceparent` (and `tracestate` property if applicable). The message activity is then stopped.
2. A `ServiceBusSender.Send` Activity is started that represents the sending operation.
3. The `ServiceBusSender.Send` Activity is decorated with a Span Link for each `Message` Activity

### ServiceBusProcessor and ServiceBusSessionProcessor
When `ServiceBusProcessor.StartProcessingAsync()` is called, it will continuously poll the desired queue for messages and handle them in a callback, which allows distributed tracing to work with a parent-child relationship. **However**, the polling generates a `ServiceBusReceiver.Receive` Activity outside of the trace context that is likely to be orphaned into its own trace.

## Test application behavior
The test application consists of 5 separate test methods, which will be explained individually.

### TestServiceBusProcessorAsync
Source: [`RequestHelper.Processor.cs`](./RequestHelper.Processor.cs)
API tested: ServiceBusProcessor

### TestServiceBusSessionReceiverAsync
> Source: [`RequestHelper.Session.cs`](./RequestHelper.Session.cs)
API tested: ServiceBusSessionReceiver

### TestSenderSchedulingAsync
> Source: [`RequestHelper.cs`](./RequestHelper.cs)
API tested: ServiceBusSender

### TestServiceBusReceiverIndividualMessageAsync
> Source: [`RequestHelper.cs`](./RequestHelper.cs)
API tested: ServiceBusSender, ServiceBusReceiver

### TestServiceBusReceiverBatchMessagesAsync
> Source: [`RequestHelper.cs`](./RequestHelper.cs)
API tested: Sending to Topics and Receiving from Subscriptions


## Resulting Traces
This is much easier to view by using the following tools:
- [Snapshot Viewer](https://andrewlock.github.io/DatadogSpanSnapshotViewer/)
- [Link to v0 snapshot](../../../snapshots/AzureServiceBusTests.SchemaV0.verified.txt)
- [Link to v1 snapshot](../../../snapshots/AzureServiceBusTests.SchemaV1.verified.txt)
### TestServiceBusProcessorAsync traces
Trace #1
Snapshot: TraceId_1 1

```plaintext
SendMessageToProcessorAsync
-----------
|          |
Message    ServiceBusSender.Send
|
ServiceBusProcessor.ProcessMessage
|
ServiceBusReceiver.Complete
```

Trace #2
Snapshot: TraceId_5 2

```plaintext
ServiceBusReceiver.Receive
```

### TestServiceBusSessionReceiverAsync
Trace #3
Snapshot: TraceId_9 3

```plaintext
FirstSessionId_Producer
------------------------------------------------
|          |                        |           |
Message    ServiceBusSender.Send    Message     ServiceBusSender.Send
```

Trace #4
Snapshot: TraceId_15 4

```plaintext
SecondSessionId_Producer
-----------
|          |
Message    ServiceBusSender.Send
```

Trace #5
Snapshot: TraceId_19 5

```plaintext
ProcessorSessionId_Producer
-----------
|          |
Message    ServiceBusSender.Send
|
ServiceBusSessionProcessor.ProcessSessionMessage
|
ServiceBusReceiver.Complete
```

Traces #6-14
Snapshot: TraceId_23-39 (odd numbers only)

- `ServiceBusReceiver.Receive`
- 2x `ServiceBusReceiver.Complete`
- 1x `ServiceBusSessionReceiver.RenewSessionLock`
- 1x `ServiceBusSessionReceiver.SetSessionState`
- 1x `ServiceBusSessionReceiver.GetSessionState`
- `ServiceBusReceiver.Receive` (receive from the SessionProcessor)
- `ServiceBusReceiver.Complete` (complete from the SessionProcessor)
- `ServiceBusReceiver.Receive` (Extra span from processing loop???)

### TestSenderSchedulingAsync

Trace #15
Snapshot: TraceId_43 15

```plaintext
TestSenderSchedulingAsync
---------------------------------------------------------------------------------------------------------------------------------
|          |                            |                          |          |          |          |                            |
Message    ServiceBusSender.Schedule    ServiceBusSender.Cancel    Message    Message    Message    ServiceBusSender.Schedule    ServiceBusSender.Cancel
```

### TestServiceBusReceiverIndividualMessageAsync


Trace #16
Snapshot: TraceId_53 16

```plaintext
SendIndividualMessageAsync
-----------
|          |
Message    ServiceBusSender.Send
```

Traces #17-26
Snapshot: TraceId_57-75 (odd numbers only)

- `ServiceBusReceiver.Peek`
- `ServiceBusReceiver.Receive`
- `ServiceBusReceiver.RenewMessageLock`
- `ServiceBusReceiver.Abandon`
- `ServiceBusReceiver.Receive`
- `ServiceBusReceiver.Defer`
- `ServiceBusReceiver.ReceiveDeferred`
- `ServiceBusReceiver.DeadLetter`
- `ServiceBusReceiver.Receive`
- `ServiceBusReceiver.Complete`

### TestServiceBusReceiverBatchMessagesAsync

Trace #27
Snapshot: TraceId_77 27

```plaintext
SendBatchMessagesAsync_IEnumerable_ServiceBusMessage
---------------------------------
|          |          |          |
Message    Message    Message    ServiceBusSender.Send
```

Trace #28
Snapshot: TraceId_83 28

```plaintext
SendBatchMessagesAsync_ServiceBusMessageBatch
---------------------------------
|          |          |          |
Message    Message    Message    ServiceBusSender.Send
```

Traces #29-47
Snapshot: TraceId_89-125 (odd numbers only)

- `ServiceBusReceiver.Peek`
- `ServiceBusReceiver.Receive` (receives entire batch in one operation)
- 3x `ServiceBusReceiver.Defer`
- `ServiceBusReceiver.ReceiveDeferred`
- 3x `ServiceBusReceiver.Complete`
- 3x `ServiceBusReceiver.Receive`
- 3x `ServiceBusReceiver.Defer`
- `ServiceBusReceiver.ReceiveDeferred`
- 3x `ServiceBusReceiver.Complete`

### TestServiceBusSubscriptionProcessorAsync

Trace #48
Snapshot: TraceId_127 48

```plaintext
SendMessageToTopicAsync
---------------------------------
|          |
Message    ServiceBusSender.Send
-------
|                                     |                                     |
ServiceBusProcessor.ProcessMessage    ServiceBusProcessor.ProcessMessage    ServiceBusProcessor.ProcessMessage
|                                     |                                     |
ServiceBusReceiver.Complete           ServiceBusReceiver.Complete           ServiceBusReceiver.Complete
```

Traces #49-51
Snapshot: TraceIds 131, 135, 139

- 3x `ServiceBusReceiver.Receive`
