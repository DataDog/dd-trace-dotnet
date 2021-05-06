using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using Datadog.Trace;

namespace Samples.AWS.SQS
{
    public class Program
    {
        private static IAmazonSQS _sqsClient;
        private static string _queue1Url;
        private static string _queue2Url;
        private static int _queue1MessagesRemaining;
        private static int _queue2MessagesRemaining;
        private static Barrier _messsageBarrier = new Barrier(2);
        private static Barrier _messsageBatchBarrier = new Barrier(2);
        private const string Queue1Name = "MySQSQueue";
        private const string Queue2Name = "MySQSQueue2";

        private static async Task Main(string[] args)
        {
            // Set up AmazonSQSConfig and redirect to the local message queue instance
            var awsCredentials = new BasicAWSCredentials("x", "x");
            var sqsConfig = new AmazonSQSConfig { ServiceURL = "http://" + Host() };
            _sqsClient = new AmazonSQSClient(awsCredentials, sqsConfig);

            Task receiveMessageTask = default;
            Task receiveMessageBatchTask = default;

#if NETFRAMEWORK
            Console.WriteLine("Starting Synchronous Consumer Threads");
            
            // Start IndividualMessages Thread
            receiveMessageTask = Task.Run(() => ReceiveMessageAndDeleteMessage()); // Run on separate thread
            receiveMessageBatchTask = Task.Run(() => ReceiveMessagesAndDeleteMessageBatch()); // Run on separate thread
            
            // Start MessageBatch Thread
            
            Console.WriteLine("Beginning Synchronous methods");
            using (var scope = Tracer.Instance.StartActive("sync-methods"))
            {
                CreateSqsQueue();
                ListQueues();
                GetQueueUrl();
                SendMessage();
                _messsageBarrier.SignalAndWait();
                receiveMessageTask.Wait();

                SendMessageBatch();
                _messsageBatchBarrier.SignalAndWait();
                receiveMessageBatchTask.Wait();
                
                PurgeQueue();
                DeleteQueue();
            }
#endif
            Console.WriteLine();
            Console.WriteLine("Starting Async Receive Threads");
            receiveMessageTask = Task.Run(ReceiveMessageAndDeleteMessageAsync); // Run on separate thread
            receiveMessageBatchTask = Task.Run(ReceiveMessagesAndDeleteMessageBatchAsync); // Run on separate thread

            Console.WriteLine("Beginning Async methods");
            using (var scope = Tracer.Instance.StartActive("async-methods"))
            {
                await CreateSqsQueueAsync();
                await ListQueuesAsync();
                await GetQueueUrlAsync();

                await SendMessageAsync();
                _messsageBarrier.SignalAndWait();
                await receiveMessageTask;
                
                await SendMessageBatchAsync();
                _messsageBatchBarrier.SignalAndWait();
                await receiveMessageBatchTask;
                
                await PurgeQueueAsync();
                await DeleteQueueAsync();
            }
        }

        private static string Host()
        {
            return Environment.GetEnvironmentVariable("AWS_SQS_HOST") ?? "localhost:9324";
        }

        #region Synchronous Methods (.NET Framework)
#if NETFRAMEWORK
        private static void CreateSqsQueue()
        {
            var createQueueRequest = new CreateQueueRequest();

            createQueueRequest.QueueName = Queue1Name;
            var attrs = new Dictionary<string, string>();
            attrs.Add(QueueAttributeName.VisibilityTimeout, "0");
            createQueueRequest.Attributes = attrs;
            var response1 = _sqsClient.CreateQueue(createQueueRequest);
            Console.WriteLine($"CreateQueue(CreateQueueRequest) HTTP status code: {response1.HttpStatusCode}");

            var response2 = _sqsClient.CreateQueue(Queue2Name);
            Console.WriteLine($"CreateQueue(string) HTTP status code: {response2.HttpStatusCode}");
        }

        private static void ListQueues()
        {
            var listQueuesResponse = _sqsClient.ListQueues(new ListQueuesRequest());
            Console.WriteLine($"ListQueues HTTP status code: {listQueuesResponse.HttpStatusCode}");
        }

        private static void GetQueueUrl()
        {
            var getQueueUrlRequest = new GetQueueUrlRequest
            {
                QueueName = Queue1Name
            };

            var response1 = _sqsClient.GetQueueUrl(getQueueUrlRequest);
            Console.WriteLine($"GetQueueUrl(GetQueueUrlRequest) HTTP status code: {response1.HttpStatusCode}");
            _queue1Url = response1.QueueUrl;

            var response2 = _sqsClient.GetQueueUrl(Queue2Name);
            Console.WriteLine($"GetQueueUrl(string) HTTP status code: {response2.HttpStatusCode}");
            _queue2Url = response2.QueueUrl;
        }

        private static void SendMessage()
        {
            var sendRequest = new SendMessageRequest();
            sendRequest.QueueUrl = _queue1Url;
            sendRequest.MessageBody = "SendMessage_SendMessageRequest";

            // Send a message with the SendMessageRequest argument
            var sendMessageResponse = _sqsClient.SendMessage(sendRequest);
            Console.WriteLine($"SendMessage HTTP status code: {sendMessageResponse.HttpStatusCode}");
            if (sendMessageResponse.HttpStatusCode == HttpStatusCode.OK)
            {
                _queue1MessagesRemaining++;
            }

            // Send a message with the string,string arguments
            sendMessageResponse = _sqsClient.SendMessage(_queue1Url, "SendMessage_string_string");
            Console.WriteLine($"SendMessage HTTP status code: {sendMessageResponse.HttpStatusCode}");
            if (sendMessageResponse.HttpStatusCode == HttpStatusCode.OK)
            {
                _queue1MessagesRemaining++;
            }
        }

        private static void ReceiveMessageAndDeleteMessage()
        {
            _messsageBarrier.SignalAndWait();

            while (_queue1MessagesRemaining > 0)
            {
                // Receive and delete the first message
                var receiveMessageRequest = new ReceiveMessageRequest();
                receiveMessageRequest.QueueUrl = _queue1Url;
                receiveMessageRequest.MaxNumberOfMessages = 1;
                receiveMessageRequest.MessageAttributeNames = new List<string> { ".*" };

                var receiveMessageResponse1 = _sqsClient.ReceiveMessage(receiveMessageRequest);
                Console.WriteLine($"ReceiveMessage(ReceiveMessageRequest) HTTP status code: {receiveMessageResponse1.HttpStatusCode}");

                var deleteMessageRequest = new DeleteMessageRequest();
                deleteMessageRequest.QueueUrl = _queue1Url;
                deleteMessageRequest.ReceiptHandle = receiveMessageResponse1.Messages.Single().ReceiptHandle;

                var deleteMessageResponse1 = _sqsClient.DeleteMessage(deleteMessageRequest);
                Console.WriteLine($"DeleteMessage(DeleteMessageRequest) HTTP status code: {deleteMessageResponse1.HttpStatusCode}");
                _queue1MessagesRemaining--;

                // Receive and delete the first message
                var receiveMessageResponse2 = _sqsClient.ReceiveMessage(_queue1Url);
                Console.WriteLine($"ReceiveMessage(string) HTTP status code: {receiveMessageResponse2.HttpStatusCode}");

                var deleteMessageResponse2 = _sqsClient.DeleteMessage(_queue1Url, receiveMessageResponse2.Messages.Single().ReceiptHandle);
                Console.WriteLine($"DeleteMessage(string, string) HTTP status code: {deleteMessageResponse2.HttpStatusCode}");
                _queue1MessagesRemaining--;
            }
        }

        private static void SendMessageBatch()
        {
            var sendMessageBatchRequest = new SendMessageBatchRequest
            {
                Entries = new List<SendMessageBatchRequestEntry>
                {
                    new SendMessageBatchRequestEntry("message1", "SendMessageBatch: FirstMessageContent"),
                    new SendMessageBatchRequestEntry("message2", "SendMessageBatch: SecondMessageContent"),
                    new SendMessageBatchRequestEntry("message3", "SendMessageBatch: ThirdMessageContent")
                },
                QueueUrl = _queue2Url
            };
            var response1 = _sqsClient.SendMessageBatch(sendMessageBatchRequest);
            Console.WriteLine($"SendMessageBatch HTTP status code: {response1.HttpStatusCode}");
            if (response1.HttpStatusCode == HttpStatusCode.OK)
            {
                _queue2MessagesRemaining++;
            }

            var sendMessageBatchRequestEntryList = new List<SendMessageBatchRequestEntry>
            {
                new SendMessageBatchRequestEntry("message1", "SendMessageBatch_SendMessageBatchRequestEntries: FirstMessageContent"),
                new SendMessageBatchRequestEntry("message2", "SendMessageBatch_SendMessageBatchRequestEntries: SecondMessageContent"),
                new SendMessageBatchRequestEntry("message3", "SendMessageBatch_SendMessageBatchRequestEntries: ThirdMessageContent")
            };
            var response2 = _sqsClient.SendMessageBatch(_queue2Url, sendMessageBatchRequestEntryList);
            Console.WriteLine($"SendMessageBatch HTTP status code: {response2.HttpStatusCode}");
            if (response2.HttpStatusCode == HttpStatusCode.OK)
            {
                _queue2MessagesRemaining++;
            }
        }

        private static void ReceiveMessagesAndDeleteMessageBatch()
        {
            _messsageBatchBarrier.SignalAndWait();

            while (_queue2MessagesRemaining > 0)
            {
                // Get the first 3 messages and delete them as a batch
                var receiveMessageRequest = new ReceiveMessageRequest();
                receiveMessageRequest.QueueUrl = _queue2Url;
                receiveMessageRequest.MaxNumberOfMessages = 3;
                receiveMessageRequest.MessageAttributeNames = new List<string> { ".*" };

                var receiveMessageResponse = _sqsClient.ReceiveMessage(receiveMessageRequest);
                Console.WriteLine($"ReceiveMessage HTTP status code: {receiveMessageResponse.HttpStatusCode}");

                var deleteMessageBatchRequest = new DeleteMessageBatchRequest()
                {
                    Entries = receiveMessageResponse.Messages.Select(message => new DeleteMessageBatchRequestEntry(message.MessageId, message.ReceiptHandle)).ToList(),
                    QueueUrl = _queue2Url
                };
                var deleteMessageBatchResponse1 = _sqsClient.DeleteMessageBatch(deleteMessageBatchRequest);
                Console.WriteLine($"DeleteMessageBatch HTTP status code: {deleteMessageBatchResponse1.HttpStatusCode}");
                _queue2MessagesRemaining--;

                // Get the second 3 messages and delete them as a batch
                // Re-use the already parameterized request object
                var receiveMessageResponse2 = _sqsClient.ReceiveMessage(receiveMessageRequest);
                Console.WriteLine($"ReceiveMessage HTTP status code: {receiveMessageResponse2.HttpStatusCode}");

                var entries = receiveMessageResponse2.Messages.Select(message => new DeleteMessageBatchRequestEntry(message.MessageId, message.ReceiptHandle)).ToList();
                var deleteMessageBatchResponse2 = _sqsClient.DeleteMessageBatch(_queue2Url, entries);
                Console.WriteLine($"DeleteMessageBatch HTTP status code: {deleteMessageBatchResponse2.HttpStatusCode}");
                _queue2MessagesRemaining--;
            }
        }

        private static void PurgeQueue()
        {
            var purgeQueueRequest = new PurgeQueueRequest()
            {
                QueueUrl = _queue1Url
            };
            var purgeQueueResponse = _sqsClient.PurgeQueue(purgeQueueRequest);
            Console.WriteLine($"PurgeQueue HTTP status code: {purgeQueueResponse.HttpStatusCode}");
        }

        private static void DeleteQueue()
        {
            var deleteQueueRequest = new DeleteQueueRequest
            {
                QueueUrl = _queue1Url
            };
            var response1 = _sqsClient.DeleteQueue(deleteQueueRequest);
            Console.WriteLine($"DeleteQueue(DeleteQueueRequest) HTTP status code: {response1.HttpStatusCode}");

            var response2 = _sqsClient.DeleteQueue(_queue2Url);
            Console.WriteLine($"DeleteQueue(string) HTTP status code: {response2.HttpStatusCode}");
        }

#endif
        #endregion

        #region Async Methods (.NET Framework and .NET Core)

        private static async Task CreateSqsQueueAsync()
        {
            var createQueueRequest = new CreateQueueRequest();

            createQueueRequest.QueueName = Queue1Name;
            var attrs = new Dictionary<string, string>();
            attrs.Add(QueueAttributeName.VisibilityTimeout, "0");
            createQueueRequest.Attributes = attrs;
            var response1 = await _sqsClient.CreateQueueAsync(createQueueRequest);
            Console.WriteLine($"CreateQueueAsync(CreateQueueRequest) HTTP status code: {response1.HttpStatusCode}");

            var response2 = await _sqsClient.CreateQueueAsync(Queue2Name);
            Console.WriteLine($"CreateQueueAsync(string) HTTP status code: {response2.HttpStatusCode}");
        }

        private static async Task ListQueuesAsync()
        {
            var listQueuesResponse = await _sqsClient.ListQueuesAsync(new ListQueuesRequest());
            Console.WriteLine($"ListQueuesAsync HTTP status code: {listQueuesResponse.HttpStatusCode}");
        }

        private static async Task GetQueueUrlAsync()
        {
            var getQueueUrlRequest = new GetQueueUrlRequest
            {
                QueueName = Queue1Name
            };

            var response1 = await _sqsClient.GetQueueUrlAsync(getQueueUrlRequest);
            Console.WriteLine($"GetQueueUrlAsync(GetQueueUrlRequest) HTTP status code: {response1.HttpStatusCode}");
            _queue1Url = response1.QueueUrl;

            var response2 = await _sqsClient.GetQueueUrlAsync(Queue2Name);
            Console.WriteLine($"GetQueueUrlAsync(string) HTTP status code: {response2.HttpStatusCode}");
            _queue2Url = response2.QueueUrl;
        }

        private static async Task SendMessageAsync()
        {
            var sendRequest = new SendMessageRequest();
            sendRequest.QueueUrl = _queue1Url;
            sendRequest.MessageBody = "SendMessageAsync_SendMessageRequest";

            // Send a message with the SendMessageRequest argument
            var sendMessageResponse = await _sqsClient.SendMessageAsync(sendRequest);
            Console.WriteLine($"SendMessageAsync HTTP status code: {sendMessageResponse.HttpStatusCode}");
            if (sendMessageResponse.HttpStatusCode == HttpStatusCode.OK)
            {
                _queue1MessagesRemaining++;
            }

            // Send a message with the string,string arguments
            sendMessageResponse = await _sqsClient.SendMessageAsync(_queue1Url, "SendMessageAsync_string_string");
            Console.WriteLine($"SendMessageAsync HTTP status code: {sendMessageResponse.HttpStatusCode}");
            if (sendMessageResponse.HttpStatusCode == HttpStatusCode.OK)
            {
                _queue1MessagesRemaining++;
            }
        }

        private static async Task ReceiveMessageAndDeleteMessageAsync()
        {
            _messsageBarrier.SignalAndWait();

            while (_queue1MessagesRemaining > 0)
            {
                // Receive and delete the first message
                var receiveMessageRequest = new ReceiveMessageRequest();
                receiveMessageRequest.QueueUrl = _queue1Url;
                receiveMessageRequest.MaxNumberOfMessages = 1;
                receiveMessageRequest.MessageAttributeNames = new List<string> { ".*" };

                var receiveMessageResponse1 = await _sqsClient.ReceiveMessageAsync(receiveMessageRequest);
                Console.WriteLine($"ReceiveMessageAsync(ReceiveMessageRequest) HTTP status code: {receiveMessageResponse1.HttpStatusCode}");

                var deleteMessageRequest = new DeleteMessageRequest();
                deleteMessageRequest.QueueUrl = _queue1Url;
                deleteMessageRequest.ReceiptHandle = receiveMessageResponse1.Messages.Single().ReceiptHandle;

                var deleteMessageResponse1 = await _sqsClient.DeleteMessageAsync(deleteMessageRequest);
                Console.WriteLine($"DeleteMessageAsync(DeleteMessageRequest) HTTP status code: {deleteMessageResponse1.HttpStatusCode}");
                _queue1MessagesRemaining--;

                // Receive and delete the first message
                var receiveMessageResponse2 = await _sqsClient.ReceiveMessageAsync(_queue1Url);
                Console.WriteLine($"ReceiveMessageAsync(string) HTTP status code: {receiveMessageResponse2.HttpStatusCode}");

                var deleteMessageResponse2 = await _sqsClient.DeleteMessageAsync(_queue1Url, receiveMessageResponse2.Messages.Single().ReceiptHandle);
                Console.WriteLine($"DeleteMessageAsync(string, string) HTTP status code: {deleteMessageResponse2.HttpStatusCode}");
                _queue1MessagesRemaining--;
            }
        }

        private static async Task SendMessageBatchAsync()
        {
            var sendMessageBatchRequest = new SendMessageBatchRequest
            {
                Entries = new List<SendMessageBatchRequestEntry>
                {
                    new SendMessageBatchRequestEntry("message1", "SendMessageBatchAsync: FirstMessageContent"),
                    new SendMessageBatchRequestEntry("message2", "SendMessageBatchAsync: SecondMessageContent"),
                    new SendMessageBatchRequestEntry("message3", "SendMessageBatchAsync: ThirdMessageContent")
                },
                QueueUrl = _queue2Url
            };
            var response1 = await _sqsClient.SendMessageBatchAsync(sendMessageBatchRequest);
            Console.WriteLine($"SendMessageBatchAsync HTTP status code: {response1.HttpStatusCode}");
            if (response1.HttpStatusCode == HttpStatusCode.OK)
            {
                _queue2MessagesRemaining++;
            }

            var sendMessageBatchRequestEntryList = new List<SendMessageBatchRequestEntry>
            {
                new SendMessageBatchRequestEntry("message1", "SendMessageBatchAsync_SendMessageBatchRequestEntries: FirstMessageContent"),
                new SendMessageBatchRequestEntry("message2", "SendMessageBatchAsync_SendMessageBatchRequestEntries: SecondMessageContent"),
                new SendMessageBatchRequestEntry("message3", "SendMessageBatchAsync_SendMessageBatchRequestEntries: ThirdMessageContent")
            };
            var response2 = await _sqsClient.SendMessageBatchAsync(_queue2Url, sendMessageBatchRequestEntryList);
            Console.WriteLine($"SendMessageBatchAsync HTTP status code: {response2.HttpStatusCode}");
            if (response2.HttpStatusCode == HttpStatusCode.OK)
            {
                _queue2MessagesRemaining++;
            }
        }

        private static async Task ReceiveMessagesAndDeleteMessageBatchAsync()
        {
            _messsageBatchBarrier.SignalAndWait();

            while (_queue2MessagesRemaining > 0)
            {
                // Get the first 3 messages and delete them as a batch
                var receiveMessageRequest = new ReceiveMessageRequest();
                receiveMessageRequest.QueueUrl = _queue2Url;
                receiveMessageRequest.MaxNumberOfMessages = 3;
                receiveMessageRequest.MessageAttributeNames = new List<string> { ".*" };

                var receiveMessageResponse = await _sqsClient.ReceiveMessageAsync(receiveMessageRequest);
                Console.WriteLine($"ReceiveMessageAsync HTTP status code: {receiveMessageResponse.HttpStatusCode}");

                var deleteMessageBatchRequest = new DeleteMessageBatchRequest() { Entries = receiveMessageResponse.Messages.Select(message => new DeleteMessageBatchRequestEntry(message.MessageId, message.ReceiptHandle)).ToList(), QueueUrl = _queue2Url };
                var deleteMessageBatchResponse1 = await _sqsClient.DeleteMessageBatchAsync(deleteMessageBatchRequest);
                Console.WriteLine($"DeleteMessageBatchAsync HTTP status code: {deleteMessageBatchResponse1.HttpStatusCode}");
                _queue2MessagesRemaining--;

                // Get the second 3 messages and delete them as a batch
                // Re-use the already parameterized request object
                var receiveMessageResponse2 = await _sqsClient.ReceiveMessageAsync(receiveMessageRequest);
                Console.WriteLine($"ReceiveMessageAsync HTTP status code: {receiveMessageResponse2.HttpStatusCode}");

                var entries = receiveMessageResponse2.Messages.Select(message => new DeleteMessageBatchRequestEntry(message.MessageId, message.ReceiptHandle)).ToList();
                var deleteMessageBatchResponse2 = await _sqsClient.DeleteMessageBatchAsync(_queue2Url, entries);
                Console.WriteLine($"DeleteMessageBatchAsync HTTP status code: {deleteMessageBatchResponse2.HttpStatusCode}");
                _queue2MessagesRemaining--;
            }
        }

        private static async Task PurgeQueueAsync()
        {
            var purgeQueueRequest = new PurgeQueueRequest()
            {
                QueueUrl = _queue1Url
            };
            var purgeQueueResponse = await _sqsClient.PurgeQueueAsync(purgeQueueRequest);
            Console.WriteLine($"PurgeQueueAsync HTTP status code: {purgeQueueResponse.HttpStatusCode}");
        }

        private static async Task DeleteQueueAsync()
        {
            var deleteQueueRequest = new DeleteQueueRequest
            {
                QueueUrl = _queue1Url
            };
            var response1 = await _sqsClient.DeleteQueueAsync(deleteQueueRequest);
            Console.WriteLine($"DeleteQueueAsync(DeleteQueueRequest) HTTP status code: {response1.HttpStatusCode}");

            var response2 = await _sqsClient.DeleteQueueAsync(_queue2Url);
            Console.WriteLine($"DeleteQueueAsync(string) HTTP status code: {response2.HttpStatusCode}");
        }
        #endregion
    }
}
