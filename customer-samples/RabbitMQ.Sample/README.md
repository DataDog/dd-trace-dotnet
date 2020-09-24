### Setup
Ensure RabbitMQ is running on localhost. If you have docker installed, you may get it up and running quickly on your workstation via the community Docker image:

```
docker run -it --rm --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management
```

For further setup instructions, see the official RabbitMQ docs at https://www.rabbitmq.com/download.html