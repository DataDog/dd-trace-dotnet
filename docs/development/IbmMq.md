# IBM MQ integration

This integration doesn't include any unit tests or a test application and provided as is for licensing reasons. 
This documentation includes some tips on how to create / run a simple test application.

Note: you can also refer to the original IBM
.

To test the integration locally you'll need:
* Download, build and run the IBM MQ for Developers. You can do this by using the following set of commands:
```shell
  git clone https://github.com/ibm-messaging/mq-container.git
  cd mq-container
  make build-devserver
```
* Run the image which was created in the previous step.
```shell
docker run \
  --rm \
  --env LICENSE=accept \ # this is required in order to run a dev server
  --env MQ_QMGR_NAME=test_queue_manager \
  --volume ibm-mq-data:/mnt/mqm \
  --publish 1414:1414 \
  --publish 9443:9443 \
  --detach \
  --name IBMMQ %YOUR_IMAGE_NAME%
```
* By default you'll get the following things created:
  * Channel: `DEV.APP.SVRCONN`
  * Queue manager: `test_queue_manager` (can be change in docker command above)
  * 3 test queues: `DEV.QUEUE.1`, `DEV.QUEUE.2`, `DEV.QUEUE.3`. You can create more using IBM MQ management UI.

A test application can be created using the original [documentation](https://www.ibm.com/docs/en/ibm-mq/9.0?topic=programs-example-c-code-fragment-use-net). 

Installing the package:
```shell
dotnet add package IBMMQDotnetClient
```

How the sample application may look like:
```csharp
Hashtable connectionProperties = new Hashtable
{
    { MQC.TRANSPORT_PROPERTY, MQC.TRANSPORT_MQSERIES_MANAGED },
    { MQC.HOST_NAME_PROPERTY, Environment.GetEnvironmentVariable("IBMMQ_HOST") ?? "localhost" },
    { MQC.PORT_PROPERTY, Environment.GetEnvironmentVariable("IBMMQ_PORT") ?? "1414" },
    { MQC.CHANNEL_PROPERTY, Environment.GetEnvironmentVariable("IBMMQ_CHANNEL") ?? "DEV.APP.SVRCONN" },
};
var queue = 'DEV.QUEUE.1';
var manager = new MQQueueManager(Environment.GetEnvironmentVariable("IBMMQ_QUEUE_MANAGER") ?? "test_queue_manager", connectionProperties);
MQQueue queue = manager.AccessQueue(, MQC.MQOO_INPUT_AS_Q_DEF | MQC.MQOO_OUTPUT);

var msg = new MQMessage(queue);
msg.WriteUTF("Test message sent to " + queue);
queue.Put(msg);

var readMsg = new MQMessage();
queue.Get(readMsg);
```

Note that only simple *put/get* methods are instrumented.