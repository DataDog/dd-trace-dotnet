# RabbitMQ Instrumentation
Currently, the .NET Tracer does have out-of-the-box automatic instrumentation for the RabbitMQ .NET SDK which means you should automatically get producer and consumer spans in your traces. That said, the consumer span is short lived. Indeed the `BasicGet` integration creates a scope but immediately disposes it. Indeed, depending how you consume the message locally, consumer children could be disconnected from the consumer span (this can happen for instance when you (or the library you use) consume the messages in a loop filling a local queue that your code would dequeue). That is why, we provide an API to manually extract the distributed context for you to pass to the child span.

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