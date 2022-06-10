namespace Datadog.Trace.TestHelpers.FSharp

module TracingIntegrationRules =
    open ValidationTypes
    open Datadog.Trace.TestHelpers
    open SpanModelHelpers

    let isAdoNet : MockSpan -> Result<MockSpan, string> =
        matches ``type`` "sql"
        &&& tagIsOptional "db.name"
        &&& tagIsPresent "db.type"
        &&& tagMatches "component" "AdoNet"
        &&& tagMatches "span.kind" "client"

    let isAerospike : MockSpan -> Result<MockSpan, string> =
        matches name "aerospike.command"
        &&& matches ``type`` "aerospike"
        &&& tagIsOptional "aerospike.key"
        &&& tagIsOptional "aerospike.namespace"
        &&& tagIsOptional "aerospike.setname"
        &&& tagIsOptional "aerospike.userkey"
        &&& tagMatches "component" "aerospike"
        &&& tagMatches "span.kind" "client"

    let isAspNet : MockSpan -> Result<MockSpan, string> =
        matches name "aspnet.request"
        &&& matches ``type`` "web"
        &&& tagIsPresent "http.method"
        &&& tagIsPresent "http.request.headers.host"
        &&& tagIsPresent "http.status_code"
        &&& tagIsPresent "http.url"
        // BUG: component tag is not set
        // &&& tagMatches "component" "aspnet"
        &&& tagMatches "span.kind" "server"

    let isAspNetMvc : MockSpan -> Result<MockSpan, string> =
        matches name "aspnet-mvc.request"
        &&& matches ``type`` "web"
        &&& tagIsPresent "aspnet.action"
        &&& tagIsOptional "aspnet.area"
        &&& tagIsPresent "aspnet.controller"
        &&& tagIsPresent "aspnet.route"
        &&& tagIsPresent "http.method"
        &&& tagIsPresent "http.request.headers.host"
        &&& tagIsPresent "http.status_code"
        &&& tagIsPresent "http.url"
        // BUG: component tag is not set
        // &&& tagMatches "component" "aspnet"
        &&& tagMatches "span.kind" "server"

    let isAspNetWebApi2 : MockSpan -> Result<MockSpan, string> =
        matches name "aspnet-webapi.request"
        &&& matches ``type`` "web"
        &&& tagIsOptional "aspnet.action"
        &&& tagIsOptional "aspnet.controller"
        &&& tagIsPresent "aspnet.route"
        &&& tagIsPresent "http.method"
        &&& tagIsPresent "http.request.headers.host"
        // BUG: some test cases do not set http.status_code
        // &&& tagIsPresent "http.status_code"
        &&& tagIsPresent "http.url"
        // BUG: component tag is not set
        // &&& tagMatches "component" "aspnet"
        &&& tagMatches "span.kind" "server"

    let isAspNetCore : MockSpan -> Result<MockSpan, string> =
        matches name "aspnet_core.request"
        &&& matches ``type`` "web"
        &&& tagIsOptional "aspnet_core.endpoint"
        &&& tagIsOptional "aspnet_core.route"
        &&& tagIsPresent "http.method"
        &&& tagIsPresent "http.request.headers.host"
        &&& tagIsPresent "http.status_code"
        &&& tagIsPresent "http.url"
        &&& tagMatches "component" "aspnet_core"
        &&& tagMatches "span.kind" "server"

    let isAspNetCoreMvc : MockSpan -> Result<MockSpan, string> =
        matches name "aspnet_core_mvc.request"
        &&& matches ``type`` "web"
        &&& tagIsPresent "aspnet_core.action"
        &&& tagIsOptional "aspnet_core.area"
        &&& tagIsPresent "aspnet_core.controller"
        &&& tagIsOptional "aspnet_core.page"
        &&& tagMatches "component" "aspnet_core"
        &&& tagMatches "span.kind" "server"

    let isAwsSqs : MockSpan -> Result<MockSpan, string> =
        matches name "sqs.request"
        &&& matches ``type`` "http"
        &&& tagMatches "aws.agent" "dotnet-aws-sdk"
        &&& tagIsPresent "aws.operation"
        &&& tagIsOptional "aws.region"
        &&& tagIsPresent "aws.requestId"
        &&& tagMatches "aws.service" "SQS"
        &&& tagIsOptional "aws.queue.name"
        &&& tagIsOptional "aws.queue.url"
        &&& tagMatches "component" "aws-sdk"
        &&& tagIsPresent "http.method"
        &&& tagIsPresent "http.status_code"
        &&& tagIsPresent "http.url"
        &&& tagMatches "span.kind" "client"

    let isCosmosDb : MockSpan -> Result<MockSpan, string> =
        matches name "cosmosdb.query"
        &&& matches ``type`` "sql"
        &&& tagIsOptional "cosmosdb.container"
        &&& tagIsOptional "db.name"
        &&& tagMatches "db.type" "cosmosdb"
        &&& tagIsPresent "out.host"
        &&& tagMatches "component" "CosmosDb"
        &&& tagMatches "span.kind" "client"

    let isCouchbase : MockSpan -> Result<MockSpan, string> =
        matches name "couchbase.query"
        &&& matches ``type`` "db"
        &&& tagIsOptional "couchbase.operation.bucket"
        &&& tagIsPresent "couchbase.operation.code"
        &&& tagIsPresent "couchbase.operation.key"
        &&& tagIsOptional "out.port"
        &&& tagIsOptional "out.host"
        &&& tagMatches "component" "Couchbase"
        &&& tagMatches "span.kind" "client"

    let isElasticsearchNet : MockSpan -> Result<MockSpan, string> =
        matches name "elasticsearch.query"
        &&& matches ``type`` "elasticsearch"
        &&& tagIsPresent "elasticsearch.action"
        &&& tagIsPresent "elasticsearch.method"
        &&& tagIsPresent "elasticsearch.url"
        &&& tagMatches "component" "elasticsearch-net"
        &&& tagMatches "span.kind" "client"

    let isGraphQL : MockSpan -> Result<MockSpan, string> =
        matchesOneOf name [| "graphql.execute"; "graphql.validate" |]
        &&& matches ``type`` "graphql"
        &&& tagIsOptional "graphql.operation.name"
        &&& tagIsOptional "graphql.operation.type"
        &&& tagIsPresent "graphql.source"
        &&& tagMatches "component" "GraphQL"
        &&& tagMatches "span.kind" "server"

    let isGrpc : MockSpan -> Result<MockSpan, string> =
        matches name "grpc.request"
        &&& matches ``type`` "grpc"
        &&& tagIsPresent "grpc.method.kind"
        &&& tagIsPresent "grpc.method.name"
        &&& tagIsPresent "grpc.method.package"
        &&& tagIsPresent "grpc.method.path"
        &&& tagIsPresent "grpc.method.service"
        &&& tagIsPresent "grpc.status.code"
        &&& tagMatches "component" "Grpc"
        &&& tagMatchesOneOf "span.kind" [| "client"; "server" |]

    let isHttpMessageHandler : MockSpan -> Result<MockSpan, string> =
        matches name "http.request"
        &&& matches ``type`` "http"
        &&& tagIsPresent "component"
        &&& tagIsPresent "http-client-handler-type"
        &&& tagIsPresent "http.method"
        &&& tagIsPresent "http.status_code"
        &&& tagIsPresent "http.url"
        &&& tagMatches "span.kind" "client"

    let isKafka : MockSpan -> Result<MockSpan, string> =
        matchesOneOf name [|"kafka.consume"; "kafka.produce" |]
        &&& matches ``type`` "queue"
        &&& tagIsOptional "kafka.offset"
        &&& tagIsOptional "kafka.partition"
        &&& tagIsOptional "kafka.tombstone"
        &&& tagIsOptional "message.queue_time_ms"
        &&& tagMatches "component" "kafka"
        &&& tagIsPresent "span.kind"

    let isMongoDB : MockSpan -> Result<MockSpan, string> =
        matches name "mongodb.query"
        &&& matches ``type`` "mongodb"
        &&& tagIsOptional "db.name"
        &&& tagIsOptional "mongodb.collection"
        &&& tagIsOptional "mongodb.query"
        &&& tagIsPresent "out.host"
        &&& tagIsPresent "out.port"
        &&& tagMatches "component" "MongoDb"
        &&& tagMatches "span.kind" "client"

    let isMsmq : MockSpan -> Result<MockSpan, string> =
        matches name "msmq.command"
        &&& matches ``type`` "queue"
        &&& tagIsPresent "msmq.command"
        &&& tagIsOptional "msmq.message.transactional"
        &&& tagIsPresent "msmq.queue.path"
        &&& tagIsOptional "msmq.queue.transactional"
        &&& tagMatches "component" "msmq"
        &&& tagMatchesOneOf "span.kind" [| "client"; "consumer"; "producer" |]

    let isMySql : MockSpan -> Result<MockSpan, string> =
        matches name "mysql.query"
        &&& matches ``type`` "sql"
        &&& tagIsPresent "db.name"
        &&& tagMatches "db.type" "mysql"
        &&& tagMatches "component" "MySql"
        &&& tagMatches "span.kind" "client"

    let isNpgsql : MockSpan -> Result<MockSpan, string> =
        matches name "postgres.query"
        &&& matches ``type`` "sql"
        &&& tagIsPresent "db.name"
        &&& tagMatches "db.type" "postgres"
        &&& tagMatches "component" "Npgsql"
        &&& tagMatches "span.kind" "client"

    let isOracle : MockSpan -> Result<MockSpan, string> =
        matches name "oracle.query"
        &&& matches ``type`` "sql"
        &&& tagIsPresent "db.name"
        &&& tagMatches "db.type" "oracle"
        &&& tagMatches "component" "Oracle"
        &&& tagMatches "span.kind" "client"

    let isRabbitMQ : MockSpan -> Result<MockSpan, string> =
        matches name "amqp.command"
        &&& matches ``type`` "queue"
        &&& tagIsPresent "amqp.command"
        &&& tagIsOptional "amqp.delivery_mode"
        &&& tagIsOptional "amqp.exchange"
        &&& tagIsOptional "amqp.routing_key"
        &&& tagIsOptional "amqp.queue"
        &&& tagIsOptional "message.size"
        &&& tagMatches "component" "RabbitMQ"
        &&& tagIsPresent "span.kind"

    let ``isService Fabric`` : MockSpan -> Result<MockSpan, string> =
        matches name "amqp.command"
        &&& matches ``type`` "redis"
        &&& tagIsPresent "service-fabric.application-id"
        &&& tagIsPresent "service-fabric.application-name"
        &&& tagIsPresent "service-fabric.partition-id"
        &&& tagIsPresent "service-fabric.node-id"
        &&& tagIsPresent "service-fabric.node-name"
        &&& tagIsPresent "service-fabric.service-name"
        &&& tagIsPresent "service-fabric.service-remoting.uri"
        &&& tagIsPresent "service-fabric.service-remoting.method-name"
        &&& tagIsOptional "service-fabric.service-remoting.method-id"
        &&& tagIsOptional "service-fabric.service-remoting.interface-id"
        &&& tagIsOptional "service-fabric.service-remoting.invocation-id"

    let ``isService Remoting (client)`` : MockSpan -> Result<MockSpan, string> =
        matches name "service_remoting.client"
        &&& tagIsPresent "service-fabric.service-remoting.uri"
        &&& tagIsPresent "service-fabric.service-remoting.method-name"
        &&& tagIsOptional "service-fabric.service-remoting.method-id"
        &&& tagIsOptional "service-fabric.service-remoting.interface-id"
        &&& tagIsOptional "service-fabric.service-remoting.invocation-id"
        &&& tagMatches "span.kind" "client"

    let ``isService Remoting (server)`` : MockSpan -> Result<MockSpan, string> =
        matches name "service_remoting.server"
        &&& tagIsPresent "service-fabric.service-remoting.uri"
        &&& tagIsPresent "service-fabric.service-remoting.method-name"
        &&& tagIsOptional "service-fabric.service-remoting.method-id"
        &&& tagIsOptional "service-fabric.service-remoting.interface-id"
        &&& tagIsOptional "service-fabric.service-remoting.invocation-id"
        &&& tagMatches "span.kind" "server"

    let isServiceStackRedis : MockSpan -> Result<MockSpan, string> =
        matches name "redis.command"
        &&& matches ``type`` "redis"
        &&& tagIsPresent "redis.raw_command"
        &&& tagIsPresent "out.host"
        &&& tagIsPresent "out.port"
        &&& tagMatches "component" "ServiceStackRedis"
        &&& tagMatches "span.kind" "client"

    let isStackExchangeRedis : MockSpan -> Result<MockSpan, string> =
        matches name "redis.command"
        &&& matches ``type`` "redis"
        &&& tagIsPresent "redis.raw_command"
        &&& tagIsPresent "out.host"
        &&& tagIsPresent "out.port"
        &&& tagMatches "component" "StackExchangeRedis"
        &&& tagMatches "span.kind" "client"

    let isSqlite : MockSpan -> Result<MockSpan, string> =
        matches name "sqlite.query"
        &&& matches ``type`` "sql"
        &&& tagIsOptional "db.name"
        &&& tagMatches "db.type" "sqlite"
        &&& tagMatches "component" "Sqlite"
        &&& tagMatches "span.kind" "client"

    let isSqlClient : MockSpan -> Result<MockSpan, string> =
        matches name "sql-server.query"
        &&& matches ``type`` "sql"
        &&& tagIsOptional "db.name"
        &&& tagMatches "db.type" "sql-server"
        &&& tagMatches "component" "SqlClient"
        &&& tagMatches "span.kind" "client"

    let isWcf : MockSpan -> Result<MockSpan, string> =
        matches name "wcf.request"
        &&& matches ``type`` "web"
        &&& tagIsPresent "http.url"
        &&& tagMatches "component" "Wcf"
        &&& tagMatches "span.kind" "server"

    let isWebRequest : MockSpan -> Result<MockSpan, string> =
        matches name "http.request"
        &&& matches ``type`` "http"
        &&& tagIsPresent "component"
        &&& tagIsPresent "http.method"
        &&& tagIsPresent "http.status_code"
        &&& tagIsPresent "http.url"
        &&& tagMatches "span.kind" "client"