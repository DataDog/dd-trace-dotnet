# Creating MassTransit 7 Instrumentation in dd-trace-dotnet

This guide shows how to create native Datadog instrumentation for MassTransit 7 using the CallTarget pattern in dd-trace-dotnet.

## Table of Contents
1. [Overview](#overview)
2. [Directory Structure](#directory-structure)
3. [Duck Typing Interfaces](#duck-typing-interfaces)
4. [Instrumentation Classes](#instrumentation-classes)
5. [Helper Classes](#helper-classes)
6. [Integration Registration](#integration-registration)
7. [Complete Examples](#complete-examples)

---

## Overview

### CallTarget Pattern

The CallTarget instrumentation pattern in dd-trace-dotnet works by:
1. **IL Rewriting**: The CLR Profiler rewrites method IL at runtime
2. **Method Wrapping**: Adds calls to `OnMethodBegin` and `OnMethodEnd`/`OnAsyncMethodEnd`
3. **Duck Typing**: Accesses third-party types without direct references
4. **Scope Management**: Creates and manages Datadog spans

### Key Components

```
AutoInstrumentation/MassTransit/
├── Duck-typed interfaces (IPublishContext, IConsumeContext, etc.)
├── Integration classes (PublishIntegration, ConsumeIntegration, etc.)
├── Helper classes (MassTransitIntegration, MassTransitConstants)
└── Tags (MassTransitTags)
```

---

## Directory Structure

Create the following structure:

```
tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/MassTransit/
├── MassTransitConstants.cs
├── MassTransitIntegration.cs
├── MassTransitTags.cs
├── ContextPropagation.cs
├── Duck Types/
│   ├── IBus.cs
│   ├── IPublishContext.cs
│   ├── ISendContext.cs
│   ├── IConsumeContext.cs
│   ├── IPublishEndpoint.cs
│   ├── ISendEndpoint.cs
│   └── IHeaders.cs
└── Integrations/
    ├── BusPublishIntegration.cs
    ├── BusPublishAsyncIntegration.cs
    ├── SendEndpointSendIntegration.cs
    ├── ConsumeIntegration.cs
    └── RequestClientGetResponseIntegration.cs
```

---

## 1. Duck Typing Interfaces

Duck typing allows accessing MassTransit types without direct assembly references.

### MassTransit/IPublishContext.cs

```csharp
// <copyright file="IPublishContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit
{
    /// <summary>
    /// Duck-typing interface for MassTransit.PublishContext
    /// </summary>
    internal interface IPublishContext
    {
        /// <summary>
        /// Gets the message ID
        /// </summary>
        Guid? MessageId { get; }

        /// <summary>
        /// Gets the conversation ID
        /// </summary>
        Guid? ConversationId { get; }

        /// <summary>
        /// Gets the correlation ID
        /// </summary>
        Guid? CorrelationId { get; }

        /// <summary>
        /// Gets the initiator ID (for sagas)
        /// </summary>
        Guid? InitiatorId { get; }

        /// <summary>
        /// Gets the source address
        /// </summary>
        Uri? SourceAddress { get; }

        /// <summary>
        /// Gets the destination address
        /// </summary>
        Uri? DestinationAddress { get; }

        /// <summary>
        /// Gets the message headers
        /// </summary>
        IHeaders? Headers { get; }
    }
}
```

### MassTransit/IConsumeContext.cs

```csharp
// <copyright file="IConsumeContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit
{
    /// <summary>
    /// Duck-typing interface for MassTransit.ConsumeContext
    /// </summary>
    internal interface IConsumeContext
    {
        /// <summary>
        /// Gets the message ID
        /// </summary>
        Guid? MessageId { get; }

        /// <summary>
        /// Gets the conversation ID
        /// </summary>
        Guid? ConversationId { get; }

        /// <summary>
        /// Gets the correlation ID
        /// </summary>
        Guid? CorrelationId { get; }

        /// <summary>
        /// Gets the initiator ID
        /// </summary>
        Guid? InitiatorId { get; }

        /// <summary>
        /// Gets the request ID (for request/response)
        /// </summary>
        Guid? RequestId { get; }

        /// <summary>
        /// Gets the source address
        /// </summary>
        Uri? SourceAddress { get; }

        /// <summary>
        /// Gets the destination address
        /// </summary>
        Uri? DestinationAddress { get; }

        /// <summary>
        /// Gets the response address
        /// </summary>
        Uri? ResponseAddress { get; }

        /// <summary>
        /// Gets the fault address
        /// </summary>
        Uri? FaultAddress { get; }

        /// <summary>
        /// Gets the message headers
        /// </summary>
        IHeaders? Headers { get; }
    }
}
```

### MassTransit/IHeaders.cs

```csharp
// <copyright file="IHeaders.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit
{
    /// <summary>
    /// Duck-typing interface for MassTransit.Headers
    /// </summary>
    internal interface IHeaders : IEnumerable<KeyValuePair<string, object>>
    {
        /// <summary>
        /// Gets or sets a header value
        /// </summary>
        object? this[string key] { get; set; }

        /// <summary>
        /// Tries to get a header value
        /// </summary>
        bool TryGetHeader(string key, out object? value);
    }
}
```

---

## 2. Constants and Tags

### MassTransit/MassTransitConstants.cs

```csharp
// <copyright file="MassTransitConstants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit
{
    internal static class MassTransitConstants
    {
        internal const string IntegrationName = nameof(IntegrationId.MassTransit);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.MassTransit;
        
        internal const string MessagingType = "masstransit";
        internal const string MessagingSystem = "in-memory"; // or "rabbitmq", "azureservicebus", etc.
        
        internal const string OperationPublish = "publish";
        internal const string OperationSend = "send";
        internal const string OperationReceive = "receive";
        internal const string OperationProcess = "process";
        
        // Assembly and type names
        internal const string MassTransitAssembly = "MassTransit";
        internal const string IBusTypeName = "MassTransit.IBus";
        internal const string IPublishEndpointTypeName = "MassTransit.IPublishEndpoint";
        internal const string ISendEndpointTypeName = "MassTransit.ISendEndpoint";
        internal const string IConsumeContextTypeName = "MassTransit.ConsumeContext";
        internal const string IConsumerTypeName = "MassTransit.IConsumer`1";
    }
}
```

### MassTransit/MassTransitTags.cs

```csharp
// <copyright file="MassTransitTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit
{
    internal partial class MassTransitTags : InstrumentationTags
    {
        [Tag(Trace.Tags.SpanKind)]
        public override string? SpanKind { get; set; }

        [Tag(Trace.Tags.InstrumentationName)]
        public string? InstrumentationName { get; set; }

        [Tag("messaging.operation")]
        public string? MessagingOperation { get; set; }

        [Tag("messaging.system")]
        public string? MessagingSystem { get; set; }

        [Tag("messaging.destination.name")]
        public string? DestinationName { get; set; }

        [Tag("messaging.masstransit.message_id")]
        public string? MessageId { get; set; }

        [Tag("messaging.message.conversation_id")]
        public string? ConversationId { get; set; }

        [Tag("messaging.masstransit.source_address")]
        public string? SourceAddress { get; set; }

        [Tag("messaging.masstransit.destination_address")]
        public string? DestinationAddress { get; set; }

        [Tag("messaging.masstransit.message_types")]
        public string? MessageTypes { get; set; }

        [Tag("messaging.message.body.size")]
        public string? MessageSize { get; set; }

        [Tag("messaging.masstransit.initiator_id")]
        public string? InitiatorId { get; set; }

        [Tag("messaging.masstransit.request_id")]
        public string? RequestId { get; set; }

        [Tag("messaging.masstransit.response_address")]
        public string? ResponseAddress { get; set; }

        [Tag("messaging.masstransit.fault_address")]
        public string? FaultAddress { get; set; }
    }
}
```

---

## 3. Helper Class

### MassTransit/MassTransitIntegration.cs

```csharp
// <copyright file="MassTransitIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit
{
    internal static class MassTransitIntegration
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MassTransitIntegration));

        internal static Scope? CreateProducerScope(
            Tracer tracer,
            string operation,
            string? messageType,
            string? destinationName = null,
            DateTimeOffset? startTime = null)
        {
            var perTraceSettings = tracer.CurrentTraceSettings;
            if (!perTraceSettings.Settings.IsIntegrationEnabled(MassTransitConstants.IntegrationId))
            {
                return null;
            }

            Scope? scope = null;

            try
            {
                var tags = new MassTransitTags
                {
                    SpanKind = SpanKinds.Producer,
                    InstrumentationName = MassTransitConstants.IntegrationName,
                    MessagingOperation = operation,
                    MessagingSystem = MassTransitConstants.MessagingSystem,
                };

                var serviceName = perTraceSettings.Schema.Messaging.GetServiceName(MassTransitConstants.MessagingType);
                var operationName = $"masstransit.{operation}";

                scope = tracer.StartActiveInternal(
                    operationName,
                    tags: tags,
                    serviceName: serviceName,
                    startTime: startTime);

                var span = scope.Span;
                span.Type = SpanTypes.Queue;
                
                // Set resource name
                if (messageType != null)
                {
                    span.ResourceName = $"{operation} {messageType}";
                    tags.MessageTypes = $"urn:message:{messageType}";
                }
                else
                {
                    span.ResourceName = operation;
                }

                if (destinationName != null)
                {
                    tags.DestinationName = destinationName;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating MassTransit producer scope");
            }

            return scope;
        }

        internal static Scope? CreateConsumerScope(
            Tracer tracer,
            string operation,
            string? messageType,
            PropagationContext context = default,
            DateTimeOffset? startTime = null)
        {
            var perTraceSettings = tracer.CurrentTraceSettings;
            if (!perTraceSettings.Settings.IsIntegrationEnabled(MassTransitConstants.IntegrationId))
            {
                return null;
            }

            Scope? scope = null;

            try
            {
                var tags = new MassTransitTags
                {
                    SpanKind = SpanKinds.Consumer,
                    InstrumentationName = MassTransitConstants.IntegrationName,
                    MessagingOperation = operation,
                    MessagingSystem = MassTransitConstants.MessagingSystem,
                };

                var serviceName = perTraceSettings.Schema.Messaging.GetServiceName(MassTransitConstants.MessagingType);
                var operationName = $"masstransit.{operation}";

                scope = tracer.StartActiveInternal(
                    operationName,
                    parent: context.SpanContext,
                    tags: tags,
                    serviceName: serviceName,
                    startTime: startTime);

                var span = scope.Span;
                span.Type = SpanTypes.Queue;
                
                // Set resource name
                if (messageType != null)
                {
                    span.ResourceName = $"{operation} {messageType}";
                    tags.MessageTypes = $"urn:message:{messageType}";
                }
                else
                {
                    span.ResourceName = operation;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating MassTransit consumer scope");
            }

            return scope;
        }

        internal static void SetPublishContextTags(MassTransitTags tags, IPublishContext context)
        {
            tags.MessageId = context.MessageId?.ToString();
            tags.ConversationId = context.ConversationId?.ToString();
            tags.SourceAddress = context.SourceAddress?.ToString();
            tags.DestinationAddress = context.DestinationAddress?.ToString();
            tags.InitiatorId = context.InitiatorId?.ToString();
        }

        internal static void SetConsumeContextTags(MassTransitTags tags, IConsumeContext context)
        {
            tags.MessageId = context.MessageId?.ToString();
            tags.ConversationId = context.ConversationId?.ToString();
            tags.SourceAddress = context.SourceAddress?.ToString();
            tags.DestinationAddress = context.DestinationAddress?.ToString();
            tags.InitiatorId = context.InitiatorId?.ToString();
            tags.RequestId = context.RequestId?.ToString();
            tags.ResponseAddress = context.ResponseAddress?.ToString();
            tags.FaultAddress = context.FaultAddress?.ToString();
        }
    }
}
```

---

## 4. Context Propagation

### MassTransit/ContextPropagation.cs

```csharp
// <copyright file="ContextPropagation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Headers;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit
{
    internal readonly struct ContextPropagation : IHeadersCollection
    {
        private readonly IHeaders _headers;

        public ContextPropagation(IHeaders headers)
        {
            _headers = headers;
        }

        public IEnumerable<string> GetValues(string name)
        {
            if (_headers.TryGetHeader(name, out var value) && value != null)
            {
                yield return value.ToString() ?? string.Empty;
            }
        }

        public void Set(string name, string value)
        {
            _headers[name] = value;
        }

        public void Add(string name, string value)
        {
            _headers[name] = value;
        }

        public void Remove(string name)
        {
            // MassTransit headers don't support removal
        }
    }
}
```

---

## 5. Integration Classes

### MassTransit/BusPublishIntegration.cs

```csharp
// <copyright file="BusPublishIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit
{
    /// <summary>
    /// MassTransit IBus.Publish calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = MassTransitConstants.MassTransitAssembly,
        TypeName = MassTransitConstants.IBusTypeName,
        MethodName = "Publish",
        ReturnTypeName = ClrNames.Task,
        ParameterTypeNames = new[] { ClrNames.GenericParameterAttribute, ClrNames.CancellationToken },
        MinimumVersion = "7.0.0",
        MaximumVersion = "7.*.*",
        IntegrationName = MassTransitConstants.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class BusPublishIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TMessage">Type of the message</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="message">The message being published.</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TMessage>(TTarget instance, TMessage message, System.Threading.CancellationToken cancellationToken)
        {
            var messageType = typeof(TMessage).Name;
            var scope = MassTransitIntegration.CreateProducerScope(
                Tracer.Instance,
                MassTransitConstants.OperationPublish,
                messageType,
                destinationName: $"urn:message:{typeof(TMessage).FullName}");

            if (scope?.Span?.Tags is MassTransitTags tags)
            {
                // Context will be propagated in the actual Publish pipeline
                // For now, we just create the span
            }

            return new CallTargetState(scope);
        }

        /// <summary>
        /// OnAsyncMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TReturn">Type of the return value</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">Return value</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value</returns>
        internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            state.Scope.DisposeWithException(exception);
            return returnValue;
        }
    }
}
```

### MassTransit/ConsumeIntegration.cs

```csharp
// <copyright file="ConsumeIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit
{
    /// <summary>
    /// MassTransit IConsumer.Consume calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = MassTransitConstants.MassTransitAssembly,
        TypeName = MassTransitConstants.IConsumerTypeName,
        MethodName = "Consume",
        ReturnTypeName = ClrNames.Task,
        ParameterTypeNames = new[] { MassTransitConstants.IConsumeContextTypeName },
        MinimumVersion = "7.0.0",
        MaximumVersion = "7.*.*",
        IntegrationName = MassTransitConstants.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ConsumeIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TContext">Type of the context</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="context">The consume context.</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TContext>(TTarget instance, TContext context)
            where TContext : IConsumeContext, IDuckType
        {
            // Extract trace context from headers
            var propagationContext = default(PropagationContext);
            if (context.Headers != null)
            {
                var headersAdapter = new ContextPropagation(context.Headers);
                propagationContext = Tracer.Instance.TracerManager.SpanContextPropagator.Extract(headersAdapter);
            }

            var messageType = instance?.GetType().GetGenericArguments()[0].Name ?? "Unknown";
            var scope = MassTransitIntegration.CreateConsumerScope(
                Tracer.Instance,
                MassTransitConstants.OperationProcess,
                messageType,
                context: propagationContext);

            if (scope?.Span?.Tags is MassTransitTags tags)
            {
                MassTransitIntegration.SetConsumeContextTags(tags, context);
            }

            return new CallTargetState(scope);
        }

        /// <summary>
        /// OnAsyncMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TReturn">Type of the return value</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">Return value</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value</returns>
        internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            state.Scope.DisposeWithException(exception);
            return returnValue;
        }
    }
}
```

---

## 6. Registration

The integrations are automatically discovered by the build process through the `[InstrumentMethod]` attributes. No manual registration needed!

---

## 7. Testing

Create integration tests in:
```
tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/MassTransitTests.cs
```

And sample applications in:
```
tracer/test/test-applications/integrations/Samples.MassTransit7/
```

---

## Summary

To create MassTransit 7 instrumentation:

1. ✅ Create duck-typed interfaces for MassTransit types
2. ✅ Create constants and tags classes
3. ✅ Create helper class for scope creation
4. ✅ Create context propagation adapter
5. ✅ Create integration classes with `[InstrumentMethod]` attributes
6. ✅ Add integration tests
7. ✅ Build and test

The `[InstrumentMethod]` attribute handles:
- Assembly/type/method targeting
- Version range support
- Automatic registration with the profiler
- IL rewriting at runtime

No external dependencies needed - it's all native Datadog instrumentation!
