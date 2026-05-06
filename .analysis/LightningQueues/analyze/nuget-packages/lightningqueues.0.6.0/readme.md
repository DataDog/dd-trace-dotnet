# LightningQueues - Fast Persistent Queues for .NET

---

[![.NET Tests](https://github.com/LightningQueues/LightningQueues/workflows/.NET%20Tests/badge.svg)](https://github.com/LightningQueues/LightningQueues/actions)
[![NuGet version](https://img.shields.io/nuget/v/LightningQueues.svg)](https://www.nuget.org/packages/LightningQueues/)

LightningQueues is a high-performance, lightweight, **store-and-forward message queue** for .NET applications. Powered
by **LightningDB** (LMDB), it ensures fast and durable persistence for sending and receiving messages, making it an
excellent choice for lightweight and cross-platform message queuing needs.

---

## Why LightningQueues?

- **Simple API**: Easily interact with the message queue through an intuitive API.
- **No Administration**: Unlike MSMQ or other Server / Brokers, it requires zero administrative setup.
- **XCopy Deployable**: No complex installation; just copy and run.
- **Cross-Platform**: Works on Windows, macOS, and Linux.
- **Durable Storage**: Leverages LMDB for high-performance reliable message storage.
- **TLS Encryption**: Optionally secure your transport layer. You have full control.

---

## Installation

To use LightningQueues, add it to your .NET project via NuGet:

```bash
dotnet add package LightningQueues
```

---

## Getting Started

Here’s how to use LightningQueues to set up a message queue and send a message:

### 1. Creating a Queue

```csharp
using LightningQueues;

// Define queue location and create the queue
var queue = new QueueConfiguration()
         .WithDefaults("C:\\path_to_your_queue_folder")
         .BuildAndStart("queue-name");
```

### 2. Sending Messages

```csharp
// Send a message to the queue
var message = new Message
{
     Data = "hello"u8.ToArray(),
     Id = MessageId.GenerateRandom(), //source identifier (for the server instance) + message identifier
     Queue = "queue-name",
     Destination = new Uri("lq.tcp://localhost:port")
     //Note the uri pattern, can be DNS, loopback, etc.
};
queue.Send(message);
```

### 3. Receiving Messages

```csharp
// Start receiving messages asynchronously with IAsyncEnumerable<MessageContext>
var messages = queue.Receive("queue-name", token);
await foreach (var msg in messages)
{
    //process the message and respond with one or more of the following
    msg.QueueContext.ReceiveLater(TimeSpan.FromSeconds(1));
    msg.QueueContext.SuccessfullyReceived(); //nothing more to do, done processing
    msg.QueueContext.Enqueue(msg.Message); //ideally a new message enqueued to the queue name on the msg
    msg.QueueContext.Send(msg.Message); //send a response or send a message to another uri;
    msg.QueueContext.MoveTo("different-queue"); //moves the currently received message to a different queue
    msg.QueueContext.CommitChanges(); // Everything previous is gathered in memory and committed in one transaction with LightningDB
}
```

---

## Running Tests

To ensure everything is running smoothly, clone the repository and run:

```bash
dotnet test
```

---

## Transport Security (TLS Encryption)

LightningQueues supports **TLS encryption** to secure communication. The library provides hooks to enable custom
certificate validation and encryption settings. For example:

```csharp
var certificate = LoadYourCertificate();
configuration.SecureTransportWith(new TlsStreamSecurity(async (uri, stream) =>
{
    //client side with no validation of server certificate
    var sslStream = new SslStream(stream, true, (_, _, _, _) => true, null);
    await sslStream.AuthenticateAsClientAsync(uri.Host);
    return sslStream;
}),
new TlsStreamSecurity(async (_, stream) =>
{
    var sslStream = new SslStream(stream, false);
    await sslStream.AuthenticateAsServerAsync(certificate, false,
        checkCertificateRevocation: false, enabledSslProtocols: SslProtocols.Tls12);
    return sslStream;
}));
```

You can customize the encryption level based on your requirements.

---

Licensed under the MIT license.  