# RabbitMQ Instrumentation
The .NET Tracer has out-of-the-box support for the `RabbitMQ.Client` library. This means if your .NET application publishes/consumes a RabbitMQ message, the .NET Tracer will automatically perform the following operations:
1. On message publish, a span will be created to represent the time spent by the SDK to publish the message.
2. While the RabbitMQ message is being constructed, the current trace context is added to the properties of the message.
3. On message consume, the properties of the message will be inspected for a trace context.
4. A span will be created to represent the time spent in consumer callbacks. If there was a trace context on the incoming message, the span will become a child of that span.

## Setup
### Application setup
Assuming these samples are being run on a machine with the .NET SDK installed, follow these steps to instrument the applications with the .NET Tracer and see the resulting traces in Datadog:
1. Ensure RabbitMQ is running on localhost. If you have docker installed, you may get it up and running quickly on your workstation via the community Docker image:
```
docker run -it --rm --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management
```
2. Configure the Datadog agent for APM [as described in our documentation](https://docs.datadoghq.com/tracing/setup_overview/setup/dotnet-core#configure-the-datadog-agent-for-apm).
3. Install the `dd-trace` [NuGet package](https://www.nuget.org/packages/dd-trace) as a .NET tool, which will simplify the setup of automatic instrumentation. To do this, run the following command:
```
dotnet tool install --global dd-trace
```
4. Run the `Receive` application with automatic instrumentation enabled. To do this, start a new terminal, navigate to this directory, and run the following commands:
```
cd Receive
dd-trace -- dotnet run
```
5. Run the `Send` application with automatic instrumentation enabled. To do this, start a new terminal, navigate to this directory, and run the following commands:
```
cd Send
dd-trace -- dotnet run
```

### Additional references
For further instructions on interacting with the `RabbitMQ.Client` library, see the official RabbitMQ docs at https://www.rabbitmq.com/tutorials/tutorial-one-dotnet.html

For further instructions on setting up a RabbitMQ server, see the official RabbitMQ docs at https://www.rabbitmq.com/download.html