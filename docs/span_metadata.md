# Span Metadata
This file is intended for development purposes only. The markdown is generated from assertions authored [here](/tracer/test/Datadog.Trace.TestHelpers/SpanMetadataRules.cs) and the assertions are actively tested in the tracing integration tests.
## AdoNet
### Span properties
Name | Required |
---------|----------------|
Type | `sql`
### Tags
Name | Required |
---------|----------------|
component | `AdoNet`
db.name | No
db.type | Yes
span.kind | `client`

## Aerospike
### Span properties
Name | Required |
---------|----------------|
Name | `aerospike.command`
Type | `aerospike`
### Tags
Name | Required |
---------|----------------|
aerospike.key | No
aerospike.namespace | No
aerospike.setname | No
aerospike.userkey | No
component | `aerospike`
span.kind | `client`

## AspNet
### Span properties
Name | Required |
---------|----------------|
Name | `aspnet.request`
Type | `web`
### Tags
Name | Required |
---------|----------------|
http.method | Yes
http.request.headers.host | Yes
http.status_code | Yes
http.url | Yes
span.kind | `server`

## AspNetMvc
### Span properties
Name | Required |
---------|----------------|
Name | `aspnet-mvc.request`
Type | `web`
### Tags
Name | Required |
---------|----------------|
aspnet.action | Yes
aspnet.area | No
aspnet.controller | Yes
aspnet.route | Yes
http.method | Yes
http.request.headers.host | Yes
http.status_code | Yes
http.url | Yes
span.kind | `server`

## AspNetWebApi2
### Span properties
Name | Required |
---------|----------------|
Name | `aspnet-webapi.request`
Type | `web`
### Tags
Name | Required |
---------|----------------|
aspnet.action | No
aspnet.controller | No
aspnet.route | Yes
http.method | Yes
http.request.headers.host | Yes
http.url | Yes
span.kind | `server`

## AspNetCore
### Span properties
Name | Required |
---------|----------------|
Name | `aspnet_core.request`
Type | `web`
### Tags
Name | Required |
---------|----------------|
aspnet_core.endpoint | No
aspnet_core.route | No
component | `aspnet_core`
http.method | Yes
http.request.headers.host | Yes
http.status_code | Yes
http.url | Yes
span.kind | `server`

## AspNetCoreMvc
### Span properties
Name | Required |
---------|----------------|
Name | `aspnet_core_mvc.request`
Type | `web`
### Tags
Name | Required |
---------|----------------|
aspnet_core.action | Yes
aspnet_core.area | No
aspnet_core.controller | Yes
aspnet_core.page | No
component | `aspnet_core`
span.kind | `server`

## AwsSqs
### Span properties
Name | Required |
---------|----------------|
Name | `sqs.request`
Type | `http`
### Tags
Name | Required |
---------|----------------|
aws.agent | `dotnet-aws-sdk`
aws.operation | Yes
aws.queue.name | No
aws.queue.url | No
aws.region | No
aws.requestId | Yes
aws.service | `SQS`
component | `aws-sdk`
http.method | Yes
http.status_code | Yes
http.url | Yes
span.kind | `client`

## CosmosDb
### Span properties
Name | Required |
---------|----------------|
Name | `cosmosdb.query`
Type | `sql`
### Tags
Name | Required |
---------|----------------|
b.type | `cosmosdb`
component | `CosmosDb`
cosmosdb.container | No
db.name | No
out.host | Yes
span.kind | `client`

## Couchbase
### Span properties
Name | Required |
---------|----------------|
Name | `couchbase.query`
Type | `db`
### Tags
Name | Required |
---------|----------------|
component | `Couchbase`
couchbase.operation.bucket | No
couchbase.operation.code | Yes
couchbase.operation.key | Yes
out.host | No
out.port | No
span.kind | `client`

## ElasticsearchNet
### Span properties
Name | Required |
---------|----------------|
Name | `elasticsearch.query`
Type | `elasticsearch`
### Tags
Name | Required |
---------|----------------|
component | `elasticsearch-net`
elasticsearch.action | Yes
elasticsearch.method | Yes
elasticsearch.url | Yes
span.kind | `client`

## GraphQL
### Span properties
Name | Required |
---------|----------------|
Name | `graphql.execute`; `graphql.validate`
Type | `graphql`
### Tags
Name | Required |
---------|----------------|
component | `GraphQL`
graphql.operation.name | No
graphql.operation.type | No
graphql.source | Yes
span.kind | `server`

## Grpc
### Span properties
Name | Required |
---------|----------------|
Name | `grpc.request`
Type | `grpc`
### Tags
Name | Required |
---------|----------------|
component | `Grpc`
grpc.method.kind | Yes
grpc.method.name | Yes
grpc.method.package | Yes
grpc.method.path | Yes
grpc.method.service | Yes
grpc.status.code | Yes
span.kind | `client`; `server`

## HttpMessageHandler
### Span properties
Name | Required |
---------|----------------|
Name | `http.request`
Type | `http`
### Tags
Name | Required |
---------|----------------|
component | Yes
http-client-handler-type | Yes
http.method | Yes
http.status_code | Yes
http.url | Yes
span.kind | `client`

## Kafka
### Span properties
Name | Required |
---------|----------------|
Name | `kafka.consume`; `kafka.produce`
Type | `queue`
### Tags
Name | Required |
---------|----------------|
component | `kafka`
kafka.offset | No
kafka.partition | No
kafka.tombstone | No
message.queue_time_ms | No
span.kind | Yes

## MongoDB
### Span properties
Name | Required |
---------|----------------|
Name | `mongodb.query`
Type | `mongodb`
### Tags
Name | Required |
---------|----------------|
component | `MongoDb`
db.name | No
mongodb.collection | No
mongodb.query | No
out.host | Yes
out.port | Yes
span.kind | `client`

## Msmq
### Span properties
Name | Required |
---------|----------------|
Name | `msmq.command`
Type | `queue`
### Tags
Name | Required |
---------|----------------|
component | `msmq`
msmq.command | Yes
msmq.message.transactional | No
msmq.queue.path | Yes
msmq.queue.transactional | No
span.kind | `client`; `producer`; `consumer`

## MySql
### Span properties
Name | Required |
---------|----------------|
Name | `mysql.query`
Type | `sql`
### Tags
Name | Required |
---------|----------------|
component | `MySql`
db.name | Yes
db.type | `mysql`
span.kind | `client`

## Npgsql
### Span properties
Name | Required |
---------|----------------|
Name | `postgres.query`
Type | `sql`
### Tags
Name | Required |
---------|----------------|
component | `Npgsql`
db.name | Yes
db.type | `postgres`
span.kind | `client`

## Oracle
### Span properties
Name | Required |
---------|----------------|
Name | `oracle.query`
Type | `sql`
### Tags
Name | Required |
---------|----------------|
component | `Oracle`
db.name | Yes
db.type | `oracle`
span.kind | `client`

## RabbitMQ
### Span properties
Name | Required |
---------|----------------|
Name | `amqp.command`
Type | `queue`
### Tags
Name | Required |
---------|----------------|
amqp.command | Yes
amqp.delivery_mode | No
amqp.exchange | No
amqp.queue | No
amqp.routing_key | No
component | `RabbitMQ`
message.size | No
span.kind | Yes

## ServiceFabric
### Span properties
Name | Required |
---------|----------------|
Name | `service_remoting.client`; `service_remoting.server`
### Tags
Name | Required |
---------|----------------|
service-fabric.application-id | Yes
service-fabric.application-name | Yes
service-fabric.node-id | Yes
service-fabric.node-name | Yes
service-fabric.partition-id | Yes
service-fabric.service-name | Yes
service-fabric.service-remoting.interface-id | No
service-fabric.service-remoting.invocation-id | No
service-fabric.service-remoting.method-id | No
service-fabric.service-remoting.method-name | Yes
service-fabric.service-remoting.uri | Yes
span.kind | `client`; `server`

## ServiceRemoting
### Span properties
Name | Required |
---------|----------------|
Name | `service_remoting.client`; `service_remoting.server`
### Tags
Name | Required |
---------|----------------|
service-fabric.service-remoting.interface-id | No
service-fabric.service-remoting.invocation-id | No
service-fabric.service-remoting.method-id | No
service-fabric.service-remoting.method-name | Yes
service-fabric.service-remoting.uri | Yes
span.kind | `client`; `server`

## ServiceStackRedis
### Span properties
Name | Required |
---------|----------------|
Name | `redis.command`
Type | `redis`
### Tags
Name | Required |
---------|----------------|
component | `ServiceStackRedis`
out.host | Yes
out.port | Yes
redis.raw_command | Yes
span.kind | `client`

## StackExchangeRedis
### Span properties
Name | Required |
---------|----------------|
Name | `redis.command`
Type | `redis`
### Tags
Name | Required |
---------|----------------|
component | `StackExchangeRedis`
out.host | Yes
out.port | Yes
redis.raw_command | Yes
span.kind | `client`

## Sqlite
### Span properties
Name | Required |
---------|----------------|
Name | `sqlite.query`
Type | `sql`
### Tags
Name | Required |
---------|----------------|
component | `Sqlite`
db.name | No
db.type | `sqlite`
span.kind | `client`

## SqlClient
### Span properties
Name | Required |
---------|----------------|
Name | `sql-server.query`
Type | `sql`
### Tags
Name | Required |
---------|----------------|
component | `SqlClient`
db.name | No
db.type | `sql-server`
span.kind | `client`

## Wcf
### Span properties
Name | Required |
---------|----------------|
Name | `wcf.request`
Type | `web`
### Tags
Name | Required |
---------|----------------|
component | `Wcf`
http.url | Yes
span.kind | `server`

## WebRequest
### Span properties
Name | Required |
---------|----------------|
Name | `http.request`
Type | `http`
### Tags
Name | Required |
---------|----------------|
component | `HttpMessageHandler`; `WebRequest`
http.method | Yes
http.status_code | Yes
http.url | Yes
span.kind | `client`

