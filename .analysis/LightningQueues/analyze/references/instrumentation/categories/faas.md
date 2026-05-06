# FaaS (Serverless) Instrumentation

FaaS integrations provide observability into serverless function invocations for AWS Lambda, Azure Functions, Google Cloud Functions, and similar platforms.

## What to Trace

### Incoming Invocations (Critical)
The function execution lifecycle:
- Function handler invocation
- Cold start detection (first invocation in new instance)
- Invocation completion or error

### Outgoing Invocations
When a function invokes another FaaS function:
- `lambda.invoke()`
- Function-to-function calls

### Trigger-Specific Context

**HTTP Triggers**
- Follow HTTP server patterns
- Request/response handling

**Messaging Triggers**
- SQS, SNS, Pub/Sub triggered invocations
- Follow messaging consumer patterns
- Separate span per message in batch triggers

**Database Triggers**
- DynamoDB Streams, Firestore triggers
- Change event processing

**Timer/Scheduled Triggers**
- Cron-based invocations
- Scheduled execution

**Event Triggers**
- EventBridge, CloudEvents
- Custom event sources

## What to Skip

### Runtime Initialization
- Cold start initialization code (capture as attribute, not separate span)
- Module loading
- Connection pooling setup

### Platform Infrastructure
- Container lifecycle management
- Memory allocation
- Timeout management

## Context Propagation

**Incoming**: Extract trace context from trigger event:
- HTTP: Request headers
- Messaging: Message attributes
- Events: Event metadata

**Outgoing**: Inject trace context when invoking other functions or services.

## Cold Start Handling

Cold starts are captured as an attribute on the invocation span, not as a separate span. This indicates first execution in a new function instance.
