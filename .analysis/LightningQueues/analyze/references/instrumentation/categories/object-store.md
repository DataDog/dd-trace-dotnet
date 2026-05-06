# Object Store Instrumentation

Object store integrations provide observability into object/blob storage operations for S3, GCS, Azure Blob, and similar systems.

## What to Trace

### Object Operations (Critical)
- Get: `getObject()`, `download()`
- Put: `putObject()`, `upload()`
- Delete: `deleteObject()`, `delete()`
- Copy: `copyObject()`, `copy()`
- Head: `headObject()` (metadata retrieval)

### Multipart Upload Operations
- Initiate: `createMultipartUpload()`
- Upload parts: `uploadPart()`
- Complete: `completeMultipartUpload()`
- Abort: `abortMultipartUpload()`

### List Operations
- List objects: `listObjects()`, `listObjectsV2()`
- List buckets: `listBuckets()`

### Query Operations
- Select: `selectObjectContent()` (S3 Select)

## What to Skip

### Bucket Administration
- Bucket creation/deletion (unless specifically needed)
- Bucket policy configuration
- Lifecycle rule configuration
- CORS configuration

### Pre-signed URL Generation
- `getSignedUrl()` - generates URL but doesn't make request
- The actual request using the URL would be traced separately

### Client Setup
- Client instantiation
- Credential configuration

## Context Propagation

Object store operations typically do NOT require context propagation - they are leaf spans that inherit context from the current trace.

The bucket and object key provide sufficient context for correlation.
