namespace Datadog.Trace.TestHelpers.FSharp

module TracingIntegrationRules =
    open ValidationTypes
    open Datadog.Trace.TestHelpers
    open SpanModelHelpers

    let isAspNetCore : MockSpan -> Result<MockSpan, string> =
        matches name "aspnet_core.request"
        &&& matches ``type`` "web"
        &&& tagIsPresent "http.method"
        &&& tagIsPresent "http.status_code"
        &&& tagIsPresent "http.url"
        &&& tagMatches "component" "aspnet_core"
        &&& tagMatches "span.kind" "server"

    let isAspNetCoreMvc : MockSpan -> Result<MockSpan, string> =
        matches name "aspnet_core_mvc.request"
        &&& matches ``type`` "web"
        &&& tagIsPresent "aspnet_core.action"
        &&& tagIsPresent "aspnet_core.controller"
        &&& tagIsPresent "aspnet_core.route"
        &&& tagMatches "component" "aspnet_core"
        &&& tagMatches "span.kind" "server"

    let isElasticsearch : MockSpan -> Result<MockSpan, string> =
        matches name "elasticsearch.query"
        &&& matches ``type`` "elasticsearch"
        &&& tagIsPresent "elasticsearch.action"
        &&& tagIsPresent "elasticsearch.method"
        &&& tagIsPresent "elasticsearch.url"
        &&& tagMatches "component" "elasticsearch-net"
        &&& tagMatches "span.kind" "client"

    let ``isGraphQL Server`` : MockSpan -> Result<MockSpan, string> =
        matches name "http.request"
        &&& matches ``type`` "graphql"
        &&& tagIsPresent "graphql.operation.name"
        &&& tagIsPresent "graphql.operation.type"
        &&& tagIsPresent "graphql.source"
        &&& tagMatches "component" "graphql"
        &&& tagMatches "span.kind" "server"

    let isHttpMessageHandler : MockSpan -> Result<MockSpan, string> =
        matches name "http.request"
        &&& matches ``type`` "http"
        &&& tagIsPresent "component"
        &&& tagIsPresent "http-client-handler-type"
        &&& tagIsPresent "http.method"
        &&& tagIsPresent "http.status_code"
        &&& tagIsPresent "http.url"
        &&& tagMatches "span.kind" "client"

    let isMongoDB : MockSpan -> Result<MockSpan, string> =
        matches name "mongodb.query"
        &&& matches ``type`` "mongodb"
        &&& tagIsPresent "db.name"
        &&& tagIsPresent "mongodb.collection"
        &&& tagIsPresent "mongodb.query"
        &&& tagIsPresent "out.host"
        &&& tagIsPresent "out.port"
        &&& tagMatches "component" "mongodb"
        &&& tagMatches "span.kind" "client"

    let isPostgreSQL : MockSpan -> Result<MockSpan, string> =
        matches name "postgres.query"
        &&& matches ``type`` "sql"
        &&& tagIsPresent "db.name"
        &&& tagMatches "db.type" "postgres"
        &&& tagMatches "component" "mongodb"
        &&& tagMatches "span.kind" "client"

    let isRabbitMQ : MockSpan -> Result<MockSpan, string> =
        matches name "amqp.command"
        &&& matches ``type`` "queue"
        &&& tagIsPresent "amqp.command"
        &&& tagIsPresent "amqp.delivery_mode"
        &&& tagIsPresent "amqp.exchange"
        &&& tagIsPresent "amqp.routing_key"
        &&& tagIsPresent "amqp.queue"
        &&& tagIsPresent "message.size"
        &&& tagMatches "component" "rabbitmq"
        &&& tagMatches "span.kind" "client"

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
        &&& tagMatches "component" "stackexchangeredis"
        &&& tagMatches "span.kind" "client"

    let isStackExchangeRedis : MockSpan -> Result<MockSpan, string> =
        matches name "redis.command"
        &&& matches ``type`` "redis"
        &&& tagIsPresent "redis.raw_command"
        &&& tagIsPresent "out.host"
        &&& tagIsPresent "out.port"
        &&& tagMatches "component" "stackexchangeredis"
        &&& tagMatches "span.kind" "client"

    let ``isWcf (server)`` : MockSpan -> Result<MockSpan, string> =
        matches name "wcf.request"
        &&& matches ``type`` "web"
        &&& tagIsPresent "http.url"
        &&& tagMatches "span.kind" "server"

    let isWebRequest : MockSpan -> Result<MockSpan, string> =
        matches name "http.request"
        &&& matches ``type`` "http"
        &&& tagIsPresent "component"
        &&& tagIsPresent "http-client-handler-type"
        &&& tagIsPresent "http.method"
        &&& tagIsPresent "http.status_code"
        &&& tagIsPresent "http.url"
        &&& tagMatches "span.kind" "client"