# Span Metadata
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
component | `CosmosDb`
cosmosdb.container | No
db.name | No
db.type | `cosmosdb`
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

## Elasticsearch
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

## GraphQL Server
### Span properties
Name | Required |
---------|----------------|
Name | `http.request`
Type | `graphql`
### Tags
Name | Required |
---------|----------------|
component | `graphql`
graphql.operation.name | Yes
graphql.operation.type | Yes
graphql.source | Yes
span.kind | `server`

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

## PostgreSQL
### Span properties
Name | Required |
---------|----------------|
Name | `postgres.query`
Type | `sql`
### Tags
Name | Required |
---------|----------------|
component | `mongodb`
db.name | Yes
db.type | `postgres`
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

## Service Fabric
### Span properties
Name | Required |
---------|----------------|
Name | `amqp.command`
Type | `redis`
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

## Service Remoting (client)
### Span properties
Name | Required |
---------|----------------|
Name | `service_remoting.client`
### Tags
Name | Required |
---------|----------------|
service-fabric.service-remoting.interface-id | No
service-fabric.service-remoting.invocation-id | No
service-fabric.service-remoting.method-id | No
service-fabric.service-remoting.method-name | Yes
service-fabric.service-remoting.uri | Yes
span.kind | `client`

## Service Remoting (server)
### Span properties
Name | Required |
---------|----------------|
Name | `service_remoting.server`
### Tags
Name | Required |
---------|----------------|
service-fabric.service-remoting.interface-id | No
service-fabric.service-remoting.invocation-id | No
service-fabric.service-remoting.method-id | No
service-fabric.service-remoting.method-name | Yes
service-fabric.service-remoting.uri | Yes
span.kind | `server`

## ServiceStackRedis
### Span properties
Name | Required |
---------|----------------|
Name | `redis.command`
Type | `redis`
### Tags
Name | Required |
---------|----------------|
component | `stackexchangeredis`
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

## Wcf (server)
### Span properties
Name | Required |
---------|----------------|
Name | `wcf.request`
Type | `web`
### Tags
Name | Required |
---------|----------------|
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
component | Yes
http.method | Yes
http.status_code | Yes
http.url | Yes
span.kind | `client`

