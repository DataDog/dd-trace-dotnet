## Instrumentation behaviors
Before reading on, make sure you understand the current instrumentation behavior to make sense of the generated traces from `Azure.Messaging.ServiceBus`:

### ServiceBusSender.SendMessageAsync and ServiceBusSender.SendMessagesAsync
When this API is called, the following diagnostic activity occurs:
1. A `Message` Activity is started, whose TraceId/SpanId is injected into the Azure Service Bus message's properties `Diagnostic-Id` and `traceparent` (and `tracestate` property if applicable)
2. A `ServiceBusSender.Send` Activity is started that represents the sending operation.
3. The `ServiceBusSender.Send` Activity is decorated with a Span Link for each `Message` Activity

### ServiceBusProcessor and ServiceBusSessionProcessor
When `ServiceBusProcessor.StartProcessingAsync()` is called, it will continuously poll the desired queue for messages and handle them in a callback, which allows distributed tracing to work with a parent-child relationship. **However**, the polling generates a `ServiceBusReceiver.Receive` Activity outside of the trace context that is likely to be orphaned into its own trace.

## Test application behavior
The test application consists of 5 separate test methods, which will be explained individually.

### TestServiceBusProcessorAsync
Source: [`RequestHelper.Processor.cs`](./RequestHelper.Processor.cs)
API tested: ServiceBusProcessor

The test method initializes a ServiceBusProcessor, opens a root span, sends one message

### TestServiceBusSessionReceiverAsync
> Source: [`RequestHelper.Session.cs`](./RequestHelper.Session.cs)

### TestSenderSchedulingAsync
> Source: [`RequestHelper.cs`](./RequestHelper.cs)

### TestServiceBusReceiverIndividualMessageAsync
> Source: [`RequestHelper.cs`](./RequestHelper.cs)

### TestServiceBusReceiverBatchMessagesAsync
> Source: [`RequestHelper.cs`](./RequestHelper.cs)


## Resulting Traces
### TestServiceBusProcessorAsync traces
Trace #1

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

```plaintext
ServiceBusReceiver.Receive
```

### TestServiceBusSessionReceiverAsync

Trace #1

```plaintext
FirstSessionId_Producer
------------------------------------------------
|          |                        |           |
Message    ServiceBusSender.Send    Message     ServiceBusSender.Send
```

Trace #2

```plaintext
SecondSessionId_Producer
-----------
|          |
Message    ServiceBusSender.Send
```

Trace #3

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

Traces #5-13

- `ServiceBusReceiver.Receive`
- 2x `ServiceBusReceiver.Complete`
- 1x `ServiceBusSessionReceiver.RenewSessionLock`
- 1x `ServiceBusSessionReceiver.SetSessionState`
- 1x `ServiceBusSessionReceiver.GetSessionState`
- `ServiceBusReceiver.Receive` (receive from the SessionProcessor)
- `ServiceBusReceiver.Complete` (complete from the SessionProcessor)
- `ServiceBusReceiver.Receive` (Extra span from processing loop???)

### TestSenderSchedulingAsync

Trace #1

```plaintext
TestSenderSchedulingAsync
---------------------------------------------------------------------------------------------------------------------------------
|          |                            |                          |          |          |          |                            |
Message    ServiceBusSender.Schedule    ServiceBusSender.Cancel    Message    Message    Message    ServiceBusSender.Schedule    ServiceBusSender.Cancel
```

### TestServiceBusReceiverIndividualMessageAsync


Trace #1

```plaintext
SendIndividualMessageAsync
-----------
|          |
Message    ServiceBusSender.Send
```

Traces #2-10

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

Trace #1

```plaintext
SendBatchMessagesAsync_IEnumerable_ServiceBusMessage
---------------------------------
|          |          |          |
Message    Message    Message    ServiceBusSender.Send
```

Trace #2

```plaintext
SendBatchMessagesAsync_ServiceBusMessageBatch
---------------------------------
|          |          |          |
Message    Message    Message    ServiceBusSender.Send
```

Traces #3-21

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

Trace #1

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

Traces #2-4

- 3x `ServiceBusReceiver.Receive`
