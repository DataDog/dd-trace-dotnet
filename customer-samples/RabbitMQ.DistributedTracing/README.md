# RabbitMQ Instrumentation
Currently, the .NET Tracer does not have out-of-the-box automatic instrumentation for the RabbitMQ .NET SDK. This means if your .NET application publishes/consumes a RabbitMQ message and you would like to propagate/consume the Datadog trace context, you must do so manually by adding/removing the Datadog headers. This sample follows the ["Hello World" C# tutorial](https://www.rabbitmq.com/tutorials/tutorial-one-dotnet.html) provided by RabbitMQ and modifies it in the following ways:

1. The sender begins a Datadog trace before publishing a message
1. The sender injects the trace context into the published message before sending
1. The receiver extracts the trace context from the consumed message
1. The receiver starts a new Datadog trace that is now properly connected to the original trace

## Setup
### Application setup
This sample contains two .NET Core applications: `Send` and `Receive`. Open two terminals. First, run the consumer:

```
cd Receive
dotnet run
```

Then run the producer:
```
cd Send
dotnet run
```

For further tutorial instructions, see the official RabbitMQ docs at https://www.rabbitmq.com/tutorials/tutorial-one-dotnet.html

### RabbitMQ setup
Ensure RabbitMQ is running on localhost. If you have docker installed, you may get it up and running quickly on your workstation via the community Docker image:

```
docker run -it --rm --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management
```

For further setup instructions, see the official RabbitMQ docs at https://www.rabbitmq.com/download.html

## Results
![Datadog UI with one RabbitMQ producer span and one RabbitMQ consumer span](https://user-images.githubusercontent.com/13769665/94503633-be690d80-01bb-11eb-8a4c-ccb4a5ee5b82.png)