using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Newtonsoft.Json;

namespace Samples.AWS.SQS
{
    static class AsyncHelpers
    {
        private const string SingleQueue = "MyAsyncSQSQueue";
        private const string BatchedQueue = "MyAsyncSQSQueue2";

        private static string _singleQueueUrl;
        private static string _batchedQueueUrl;
        private static Barrier _messsageBarrier = new Barrier(2);
        private static Barrier _messsageBatchBarrier = new Barrier(2);

        private static readonly Dictionary<string, MessageAttributeValue> AutoRemovedMessageAttributes = new()
        {
            { "x-datadog-1", new MessageAttributeValue() { DataType = "String", StringValue = "value1" } },
            { "x-datadog-2", new MessageAttributeValue() { DataType = "String", StringValue = "value2" } },
            { "x-datadog-3", new MessageAttributeValue() { DataType = "String", StringValue = "value3" } },
            { "x-datadog-4", new MessageAttributeValue() { DataType = "String", StringValue = "value4" } },
            { "x-datadog-5", new MessageAttributeValue() { DataType = "String", StringValue = "value5" } },
            { "x-datadog-6", new MessageAttributeValue() { DataType = "String", StringValue = "value6" } },
            { "x-datadog-7", new MessageAttributeValue() { DataType = "String", StringValue = "value7" } },
            { "x-datadog-8", new MessageAttributeValue() { DataType = "String", StringValue = "value8" } },
            { "x-datadog-9", new MessageAttributeValue() { DataType = "String", StringValue = "value9" } },
            { "dd-pathway-ctx", new MessageAttributeValue() { DataType = "String", StringValue = "value10" } }
        };

        private static readonly Dictionary<string, MessageAttributeValue> FullMessageAttributes = new()
        {
            { "attribute1", new MessageAttributeValue() { DataType = "String", StringValue = "value1" } },
            { "attribute2", new MessageAttributeValue() { DataType = "String", StringValue = "value2" } },
            { "attribute3", new MessageAttributeValue() { DataType = "String", StringValue = "value3" } },
            { "attribute4", new MessageAttributeValue() { DataType = "String", StringValue = "value4" } },
            { "attribute5", new MessageAttributeValue() { DataType = "String", StringValue = "value5" } },
            { "attribute6", new MessageAttributeValue() { DataType = "String", StringValue = "value6" } },
            { "attribute7", new MessageAttributeValue() { DataType = "String", StringValue = "value7" } },
            { "attribute8", new MessageAttributeValue() { DataType = "String", StringValue = "value8" } },
            { "attribute9", new MessageAttributeValue() { DataType = "String", StringValue = "value9" } },
            { "attribute10", new MessageAttributeValue() { DataType = "String", StringValue = "value10" } }
        };

        public static async Task RunAllScenarios(AmazonSQSClient sqsClient)
        {
            Console.WriteLine("Beginning Async methods");

            using (var scope = SampleHelpers.CreateScope("async-methods"))
            {
                await CreateSqsQueuesAsync(sqsClient);
                await ListQueuesAsync(sqsClient);
                await GetQueuesUrlAsync(sqsClient);
                await PurgeQueueAsync(sqsClient);

                await RunValidationsWithoutReceiveLoopAsync(sqsClient);

                var receiveMessageTask = Task.Run(() => ReceiveMessageAndDeleteMessageAsync(sqsClient)); // Run on separate thread
                var receiveMessageBatchTask = Task.Run(() => ReceiveMessagesAndDeleteMessageBatchAsync(sqsClient)); // Run on separate thread

                await SendMessageAsync(sqsClient);
                _messsageBarrier.SignalAndWait(); // Start the SingleMessage receive loop
                await receiveMessageTask;

                await SendMessageBatchAsync(sqsClient);
                _messsageBatchBarrier.SignalAndWait(); // Start the BatchedMessage receive loop
                await receiveMessageBatchTask;

                await DeleteQueuesAsync(sqsClient);
            }
        }

        [Flags]
        public enum Scenario
        {
            None = 0,
            Batch = 1,
            SameThread = 2,
            Injected = 4
        }

        public static async Task RunSpecificScenario(AmazonSQSClient sqsClient, Scenario s)
        {
            // Ensure there's a parent span for all the requests, so that when we compare
            // the injected context to the active, we _have_ an active trace
            using var scope = SampleHelpers.CreateScope("async-methods");

            // setup
            await CreateSqsQueuesAsync(sqsClient);
            await GetQueuesUrlAsync(sqsClient);
            await PurgeQueueAsync(sqsClient);

            Console.WriteLine($"Running test scenario {s}");
            if (s.HasFlag(Scenario.SameThread))
            {
                if (s.HasFlag(Scenario.Injected))
                {
                    if (s.HasFlag(Scenario.Batch))
                    {
                        await SendBatchMessagesWithInjectedHeadersAsync(sqsClient);
                    }
                    else
                    {
                        await SendMessagesWithInjectedHeadersAsync(sqsClient);
                    }
                }
                else
                {
                    if (s.HasFlag(Scenario.Batch))
                    {
                        await SendBatchMessagesWithoutInjectedHeadersAsync(sqsClient);
                    }
                    else
                    {
                        await SendMessagesWithoutInjectedHeadersAsync(sqsClient);
                    }
                }
            }
            else
            {
                if (!s.HasFlag(Scenario.Injected))
                {
                    throw new Exception($"Bad scenario requested ({s}): there is no non-injected multi-thread scenario");
                }

                if (s.HasFlag(Scenario.Batch))
                {
                    var receiveMessageBatchTask = Task.Run(() => ReceiveMessagesAndDeleteMessageBatchAsync(sqsClient)); // Run on separate thread
                    await SendMessageBatchAsync(sqsClient);
                    _messsageBatchBarrier.SignalAndWait(); // Start the BatchedMessage receive loop
                    await receiveMessageBatchTask;
                }
                else
                {
                    var receiveMessageTask = Task.Run(() => ReceiveMessageAndDeleteMessageAsync(sqsClient)); // Run on separate thread

                    await SendMessageAsync(sqsClient);
                    _messsageBarrier.SignalAndWait(); // Start the SingleMessage receive loop
                    await receiveMessageTask;
                }
            }

            // teardown
            await DeleteQueuesAsync(sqsClient);
        }

        private static async Task CreateSqsQueuesAsync(AmazonSQSClient sqsClient)
        {
            var createQueueRequest = new CreateQueueRequest();

            createQueueRequest.QueueName = SingleQueue;
            var attrs = new Dictionary<string, string>();
            attrs.Add(QueueAttributeName.VisibilityTimeout, "0");
            createQueueRequest.Attributes = attrs;
            var response1 = await sqsClient.CreateQueueAsync(createQueueRequest);
            Console.WriteLine($"CreateQueueAsync(CreateQueueRequest) HTTP status code: {response1.HttpStatusCode}");

            var response2 = await sqsClient.CreateQueueAsync(BatchedQueue);
            Console.WriteLine($"CreateQueueAsync(string) HTTP status code: {response2.HttpStatusCode}");
        }

        private static async Task ListQueuesAsync(AmazonSQSClient sqsClient)
        {
            var listQueuesResponse = await sqsClient.ListQueuesAsync(new ListQueuesRequest());
            Console.WriteLine($"ListQueuesAsync HTTP status code: {listQueuesResponse.HttpStatusCode}");
        }

        private static async Task GetQueuesUrlAsync(AmazonSQSClient sqsClient)
        {
            var getQueueUrlRequest = new GetQueueUrlRequest
            {
                QueueName = SingleQueue
            };

            var response1 = await sqsClient.GetQueueUrlAsync(getQueueUrlRequest);
            Console.WriteLine($"GetQueueUrlAsync(GetQueueUrlRequest) HTTP status code: {response1.HttpStatusCode}");
            _singleQueueUrl = response1.QueueUrl;

            var response2 = await sqsClient.GetQueueUrlAsync(BatchedQueue);
            Console.WriteLine($"GetQueueUrlAsync(string) HTTP status code: {response2.HttpStatusCode}");
            _batchedQueueUrl = response2.QueueUrl;
        }

        private static async Task RunValidationsWithoutReceiveLoopAsync(AmazonSQSClient sqsClient)
        {
            // We'll throw eagerly throw exceptions when doing validation
            // to make it easier to debug later
            await SendMessagesWithInjectedHeadersAsync(sqsClient);
            await SendBatchMessagesWithInjectedHeadersAsync(sqsClient);
            await SendMessagesWithoutInjectedHeadersAsync(sqsClient);
            await SendBatchMessagesWithoutInjectedHeadersAsync(sqsClient);
        }

        private static async Task SendMessagesWithInjectedHeadersAsync(AmazonSQSClient sqsClient)
        {
            // Send one message, receive it, and parse it for its headers
            // Add a bunch of datadog tags to validate that we remove them

            // Send one message, receive it, and parse it for its headers
            var sendRequest = new SendMessageRequest() { QueueUrl = _singleQueueUrl, MessageBody = "SendMessageAsync_SendMessageRequest", MessageAttributes = AutoRemovedMessageAttributes };

            // Send a message with the SendMessageRequest argument
            await sqsClient.SendMessageAsync(sendRequest);

            var receiveMessageRequest = new ReceiveMessageRequest() { QueueUrl = _singleQueueUrl, MessageAttributeNames = new List<string>() { ".*" } };

            var receiveMessageResponse1 = await sqsClient.ReceiveMessageAsync(receiveMessageRequest);
            await sqsClient.DeleteMessageAsync(_singleQueueUrl, receiveMessageResponse1.Messages.First().ReceiptHandle);

            Common.AssertDistributedTracingHeaders(receiveMessageResponse1.Messages);
            Common.AssertNoXDatadogTracingHeaders(receiveMessageResponse1.Messages);
        }

        private static async Task SendBatchMessagesWithInjectedHeadersAsync(AmazonSQSClient sqsClient)
        {
            // Send a batch of messages, receive them, and parse them for headers
            var sendMessageBatchRequest = new SendMessageBatchRequest
            {
                Entries =
                [
                    new SendMessageBatchRequestEntry("message1", "SendMessageBatchAsync: FirstMessageContent") { MessageAttributes = AutoRemovedMessageAttributes },
                    new SendMessageBatchRequestEntry("message2", "SendMessageBatchAsync: SecondMessageContent") { MessageAttributes = AutoRemovedMessageAttributes },
                    new SendMessageBatchRequestEntry("message3", "SendMessageBatchAsync: ThirdMessageContent") { MessageAttributes = AutoRemovedMessageAttributes }
                ],
                QueueUrl = _batchedQueueUrl
            };
            await sqsClient.SendMessageBatchAsync(sendMessageBatchRequest);

            var receiveMessageBatchRequest = new ReceiveMessageRequest()
            {
                QueueUrl = _batchedQueueUrl,
                MessageAttributeNames = new List<string>() { ".*" },
                MaxNumberOfMessages = 3
            };
            var receiveMessageBatchResponse1 = await sqsClient.ReceiveMessageAsync(receiveMessageBatchRequest);

            await sqsClient.DeleteMessageBatchAsync(_singleQueueUrl, receiveMessageBatchResponse1.Messages.Select(m => new DeleteMessageBatchRequestEntry(m.MessageId, m.ReceiptHandle)).ToList());

            Common.AssertDistributedTracingHeaders(receiveMessageBatchResponse1.Messages);
            Common.AssertNoXDatadogTracingHeaders(receiveMessageBatchResponse1.Messages);
        }

        private static async Task SendMessagesWithoutInjectedHeadersAsync(AmazonSQSClient sqsClient)
        {
            // Send one message, receive it, and parse it for its headers
            var sendRequest = new SendMessageRequest() { QueueUrl = _singleQueueUrl, MessageBody = "SendMessageAsync_SendMessageRequest", MessageAttributes = FullMessageAttributes };

            // Send a message with the SendMessageRequest argument
            await sqsClient.SendMessageAsync(sendRequest);

            var receiveMessageRequest = new ReceiveMessageRequest() { QueueUrl = _singleQueueUrl, MessageAttributeNames = new List<string>() { ".*" } };
            var receiveMessageResponse1 = await sqsClient.ReceiveMessageAsync(receiveMessageRequest);
            await sqsClient.DeleteMessageAsync(_singleQueueUrl, receiveMessageResponse1.Messages.First().ReceiptHandle);

            // Validate that the trace id made it into the message
            Common.AssertNoDistributedTracingHeaders(receiveMessageResponse1.Messages);
        }

        private static async Task SendBatchMessagesWithoutInjectedHeadersAsync(AmazonSQSClient sqsClient)
        {
            // Send a batch of messages, receive them, and parse them for headers
            var sendMessageBatchRequest = new SendMessageBatchRequest
            {
                Entries =
                [
                    new SendMessageBatchRequestEntry("message1", "SendMessageBatchAsync: FirstMessageContent") { MessageAttributes = FullMessageAttributes },
                    new SendMessageBatchRequestEntry("message2", "SendMessageBatchAsync: SecondMessageContent") { MessageAttributes = FullMessageAttributes },
                    new SendMessageBatchRequestEntry("message3", "SendMessageBatchAsync: ThirdMessageContent") { MessageAttributes = FullMessageAttributes }
                ],
                QueueUrl = _batchedQueueUrl
            };
            await sqsClient.SendMessageBatchAsync(sendMessageBatchRequest);

            var receiveMessageBatchRequest = new ReceiveMessageRequest()
            {
                QueueUrl = _batchedQueueUrl,
                MessageAttributeNames = new List<string>() { ".*" },
                MaxNumberOfMessages = 3
            };
            var receiveMessageBatchResponse1 = await sqsClient.ReceiveMessageAsync(receiveMessageBatchRequest);
            await sqsClient.DeleteMessageBatchAsync(_singleQueueUrl, receiveMessageBatchResponse1.Messages.Select(m => new DeleteMessageBatchRequestEntry(m.MessageId, m.ReceiptHandle)).ToList());

            // Validate that the trace id made it into the messages
            Common.AssertNoDistributedTracingHeaders(receiveMessageBatchResponse1.Messages);
        }

        private static async Task SendMessageAsync(AmazonSQSClient sqsClient)
        {
            var sendRequest = new SendMessageRequest();
            sendRequest.QueueUrl = _singleQueueUrl;
            sendRequest.MessageBody = "SendMessageAsync_SendMessageRequest";
            sendRequest.MessageAttributes = null; // Set message attributes to null so we are forced to handle the scenario

            // Send a message with the SendMessageRequest argument
            var sendMessageResponse = await sqsClient.SendMessageAsync(sendRequest);
            Console.WriteLine($"SendMessageAsync HTTP status code: {sendMessageResponse.HttpStatusCode}");
        }

        private static async Task ReceiveMessageAndDeleteMessageAsync(AmazonSQSClient sqsClient)
        {
            _messsageBarrier.SignalAndWait();

            // Receive and delete message
            var receiveMessageRequest = new ReceiveMessageRequest();
            receiveMessageRequest.QueueUrl = _singleQueueUrl;
            receiveMessageRequest.MaxNumberOfMessages = 1;
            // setting those to null is "legal" and should be handled by the instrumentation
            receiveMessageRequest.MessageAttributeNames = null;
            receiveMessageRequest.AttributeNames = null;

            var receiveMessageResponse = await sqsClient.ReceiveMessageAsync(receiveMessageRequest);
            Console.WriteLine($"ReceiveMessageAsync(ReceiveMessageRequest) HTTP status code: {receiveMessageResponse.HttpStatusCode}");
            if (receiveMessageResponse.HttpStatusCode == HttpStatusCode.OK)
            {
                var deleteMessageRequest = new DeleteMessageRequest();
                deleteMessageRequest.QueueUrl = _singleQueueUrl;
                deleteMessageRequest.ReceiptHandle = receiveMessageResponse.Messages.Single().ReceiptHandle;

                var deleteMessageResponse1 = await sqsClient.DeleteMessageAsync(deleteMessageRequest);
                Console.WriteLine($"DeleteMessageAsync(DeleteMessageRequest) HTTP status code: {deleteMessageResponse1.HttpStatusCode}");
            }
        }

        private static async Task SendMessageBatchAsync(AmazonSQSClient sqsClient)
        {
            var sendMessageBatchRequest = new SendMessageBatchRequest
            {
                Entries = new List<SendMessageBatchRequestEntry>
                {
                    new("message1", "SendMessageBatchAsync: FirstMessageContent") { MessageAttributes = null }, // Set message attributes to null so we are forced to handle the scenario
                    new("message2", "SendMessageBatchAsync: SecondMessageContent") { MessageAttributes = null }, // Set message attributes to null so we are forced to handle the scenario
                    new("message3", "SendMessageBatchAsync: ThirdMessageContent") { MessageAttributes = null }, // Set message attributes to null so we are forced to handle the scenario
                },
                QueueUrl = _batchedQueueUrl
            };
            var response1 = await sqsClient.SendMessageBatchAsync(sendMessageBatchRequest);
            Console.WriteLine($"SendMessageBatchAsync HTTP status code: {response1.HttpStatusCode}");
        }

        private static async Task ReceiveMessagesAndDeleteMessageBatchAsync(AmazonSQSClient sqsClient)
        {
            _messsageBatchBarrier.SignalAndWait();

            // Get the 3 messages and delete them as a batch
            var receiveMessageRequest = new ReceiveMessageRequest();
            receiveMessageRequest.QueueUrl = _batchedQueueUrl;
            receiveMessageRequest.MaxNumberOfMessages = 3;
            receiveMessageRequest.MessageAttributeNames = new List<string> { ".*" };

            var receiveMessageResponse = await sqsClient.ReceiveMessageAsync(receiveMessageRequest);
            Console.WriteLine($"ReceiveMessageAsync HTTP status code: {receiveMessageResponse.HttpStatusCode}");

            if (receiveMessageResponse.HttpStatusCode == HttpStatusCode.OK)
            {
                var deleteMessageBatchRequest = new DeleteMessageBatchRequest()
                {
                    Entries = receiveMessageResponse.Messages.Select(message => new DeleteMessageBatchRequestEntry(message.MessageId, message.ReceiptHandle)).ToList(),
                    QueueUrl = _batchedQueueUrl
                };
                var deleteMessageBatchResponse1 = await sqsClient.DeleteMessageBatchAsync(deleteMessageBatchRequest);
                Console.WriteLine($"DeleteMessageBatchAsync HTTP status code: {deleteMessageBatchResponse1.HttpStatusCode}");
            }
        }

        private static async Task PurgeQueueAsync(AmazonSQSClient sqsClient)
        {
            var purgeQueueRequest = new PurgeQueueRequest()
            {
                QueueUrl = _singleQueueUrl
            };
            var purgeQueueResponse = await sqsClient.PurgeQueueAsync(purgeQueueRequest);
            Console.WriteLine($"PurgeQueueAsync HTTP status code: {purgeQueueResponse.HttpStatusCode}");

            var response2 = await sqsClient.PurgeQueueAsync(_batchedQueueUrl);
            Console.WriteLine($"PurgeQueueAsync HTTP status code: {response2.HttpStatusCode}");
        }

        private static async Task DeleteQueuesAsync(AmazonSQSClient sqsClient)
        {
            var deleteQueueRequest = new DeleteQueueRequest
            {
                QueueUrl = _singleQueueUrl
            };
            var response1 = await sqsClient.DeleteQueueAsync(deleteQueueRequest);
            Console.WriteLine($"DeleteQueueAsync(DeleteQueueRequest) HTTP status code: {response1.HttpStatusCode}");

            var response2 = await sqsClient.DeleteQueueAsync(_batchedQueueUrl);
            Console.WriteLine($"DeleteQueueAsync(string) HTTP status code: {response2.HttpStatusCode}");
        }
    }
}
