# Cloud Provider SDK Instrumentation

Cloud provider integrations provide observability into SDK operations for AWS, GCP, Azure, and similar platforms.

## What to Trace

### All SDK Client Calls (Critical)
Every SDK method that makes a remote API call to the cloud provider:
- Service operations: `dynamodb.getItem()`, `s3.putObject()`, `lambda.invoke()`
- The operation name and service are the key identifiers

### Service-Specific Operations

**Compute:**
- Lambda/Functions: `invoke()`, `invokeAsync()`
- Step Functions: `startExecution()`, `startSyncExecution()`

**Storage:**
- S3/GCS/Blob: See {{link:instrumentation/categories/object-store|Object Store}} category

**Database:**
- DynamoDB: `getItem()`, `putItem()`, `query()`, `scan()`, `batchWriteItem()`
- These follow database patterns with cloud-specific context

**Messaging:**
- SQS/SNS/Pub-Sub: See {{link:instrumentation/categories/messaging|Messaging}} category

**Other Services:**
- EventBridge: `putEvents()`
- CloudWatch: `putLogEvents()`, `putMetricData()`
- Secrets Manager: `getSecretValue()`

## What to Skip

### Client Construction
- SDK client instantiation
- Credential configuration
- Region configuration

### Internal SDK Operations
- Request signing
- Retry logic (trace each attempt, not internal retry mechanics)
- Response parsing

## Context Propagation

Cloud SDK operations may support context propagation depending on the service:
- **Messaging services** (SQS, SNS, EventBridge): Inject context into message attributes
- **Compute services** (Lambda, Step Functions): Inject context into invocation payload
- **Storage/Database services**: Typically no propagation needed (leaf operations)
