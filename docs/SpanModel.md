# Span Model
## Span Metadata
Property | Required Value |
---------|----------------|
Name | _non-null value_ |
Resource | _non-null value_ |
Service | _non-null value_ |
SpanId | _non-zero value_ |
TraceId | _non-zero value_ |
Type | _non-null value_ |
Tags["env"] | _non-null value_ |
Tags["version"] | _non-null value_ |

## RootSpan Metadata
Property | Required Value |
---------|----------------|
Tags["language"] | _non-null value_ |
Tags["runtime-id"] | _non-null value_ |
Metrics["_dd.top_level"] | `1.0` |

## AutomaticInstrumentationSpan Metadata
Property | Required Value |
---------|----------------|
Tags["component"] | _non-null value_ |

## WebSpan Metadata
Property | Required Value |
---------|----------------|
Type | `"web"` |
Tags["http.method"] | _non-null value_ |
Tags["http.status_code"] | _non-null value_ |
Tags["span.kind"] | `"server"` |

## AspNetCore Metadata
Property | Required Value |
---------|----------------|
Name | `"aspnet_core.request"` |
Type | `"web"` |
Tags["component"] | `"aspnet_core"` |
Tags["http.method"] | _non-null value_ |
Tags["http.status_code"] | _non-null value_ |
Tags["http.url"] | _non-null value_ |
Tags["span.kind"] | `"server"` |

## AspNetCoreMvc Metadata
Property | Required Value |
---------|----------------|
Name | `"aspnet_core_mvc.request"` |
Type | `"web"` |
Tags["aspnet_core_mvc.action"] | _non-null value_ |
Tags["aspnet_core_mvc.controller"] | _non-null value_ |
Tags["aspnet_core_mvc.route"] | _non-null value_ |
Tags["component"] | `"aspnet_core"` |
Tags["span.kind"] | `"server"` |

## Elasticsearch Metadata
Property | Required Value |
---------|----------------|
Name | `"elasticsearch.query"` |
Type | `"elasticsearch"` |
Tags["component"] | `"elasticsearch-net"` |
Tags["elasticsearch.action"] | _non-null value_ |
Tags["elasticsearch.method"] | _non-null value_ |
Tags["elasticsearch.url"] | _non-null value_ |
Tags["span.kind"] | `"client"` |

## GraphQL Server Metadata
Property | Required Value |
---------|----------------|
Name | `"http.request"` |
Type | `"graphql"` |
Tags["component"] | `"graphql"` |
Tags["graphql.operation.name"] | _non-null value_ |
Tags["graphql.operation.type"] | _non-null value_ |
Tags["graphql.source"] | _non-null value_ |
Tags["span.kind"] | `"server"` |

## HttpMessageHandler Metadata
Property | Required Value |
---------|----------------|
Name | `"http.request"` |
Type | `"http"` |
Tags["component"] | _non-null value_ |
Tags["http-client-handler-type"] | _non-null value_ |
Tags["http.method"] | _non-null value_ |
Tags["http.status_code"] | _non-null value_ |
Tags["http.url"] | _non-null value_ |
Tags["span.kind"] | `"client"` |

## MongoDB Metadata
Property | Required Value |
---------|----------------|
Name | `"mongodb.query"` |
Type | `"mongodb"` |
Tags["component"] | `"mongodb"` |
Tags["db.name"] | _non-null value_ |
Tags["mongodb.collection"] | _non-null value_ |
Tags["mongodb.query"] | _non-null value_ |
Tags["out.host"] | _non-null value_ |
Tags["out.port"] | _non-null value_ |
Tags["span.kind"] | `"client"` |

## Wcf Server Metadata
Property | Required Value |
---------|----------------|
Name | `"wcf.request"` |
Type | `"web"` |
Tags["http.url"] | _non-null value_ |
Tags["span.kind"] | `"server"` |

## WebRequest Metadata
Property | Required Value |
---------|----------------|
Name | `"http.request"` |
Type | `"http"` |
Tags["component"] | _non-null value_ |
Tags["http-client-handler-type"] | _non-null value_ |
Tags["http.method"] | _non-null value_ |
Tags["http.status_code"] | _non-null value_ |
Tags["http.url"] | _non-null value_ |
Tags["span.kind"] | `"client"` |

