using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using Datadog.Trace;

namespace Samples.AWS.SQS
{
    public class Program
    {
        static IAmazonSQS _sqsClient;
        static string _queueUrl1;
        static string _queueUrl2;
        const string QueueName1 = "MySQSQueue";
        const string QueueName2 = "MySQSQueue2";

        // See https://markmcgookin.com/2017/03/17/posting-to-amazon-sqs-with-net-core/
        static async Task Main(string[] args)
        {
            // Set up AmazonSQSConfig and redirect to the local message queue instance
            var awsCredentials = new BasicAWSCredentials("x", "x");
            var sqsConfig = new AmazonSQSConfig { ServiceURL = "http://" + Host() };
            _sqsClient = new AmazonSQSClient(awsCredentials, sqsConfig);

#if NETFRAMEWORK
            Console.WriteLine("Beginning Synchronous methods");
            using (var scope = Tracer.Instance.StartActive("sync-methods"))
            {
                CreateSqsQueue();
                ListQueues();
                _queueUrl1 = GetQueueUrl();
                SendMessage();
                ReceiveMessage();
                SendMessageBatch();
                ReceiveMessageBatch();
                PurgeQueue();
                DeleteQueue();
            }
#endif
            Console.WriteLine();
            Console.WriteLine("Beginning Async methods");
            using (var scope = Tracer.Instance.StartActive("async-methods"))
            {
                await CreateSqsQueueAsync();
                await ListQueuesAsync();
                await GetQueueUrlAsync();
                await SendMessageAsync();
                await ReceiveMessageAndDeleteMessageAsync();
                await SendMessageBatchAsync();
                await ReceiveMessagesAndDeleteMessageBatchAsync();
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
        static void CreateSqsQueue()
        {
            var createQueueRequest = new CreateQueueRequest();

            createQueueRequest.QueueName = QueueName1;
            var attrs = new Dictionary<string, string>();
            attrs.Add(QueueAttributeName.VisibilityTimeout, "0");
            createQueueRequest.Attributes = attrs;
            var createQueueResponse = _sqsClient.CreateQueue(createQueueRequest);

            Console.WriteLine($"CreateQueue HTTP status code: {createQueueResponse.HttpStatusCode}");
        }

        static void ListQueues()
        {
            var listQueuesResponse = _sqsClient.ListQueues(new ListQueuesRequest());
            Console.WriteLine($"ListQueues HTTP status code: {listQueuesResponse.HttpStatusCode}");
        }

        static string GetQueueUrl()
        {
            var getQueueUrlRequest = new GetQueueUrlRequest
            {
                QueueName = QueueName1
            };

            var getQueueUrlResponse = _sqsClient.GetQueueUrl(getQueueUrlRequest);
            Console.WriteLine($"GetQueueUrl HTTP status code: {getQueueUrlResponse.HttpStatusCode}");

            // Set queue url for later calls
            return getQueueUrlResponse.QueueUrl;
        }

        static void SendMessage()
        {
            var sendRequest = new SendMessageRequest();
            sendRequest.QueueUrl = _queueUrl1;
            sendRequest.MessageBody = "SendMessage_SendMessageRequest";

            // Send a message with the SendMessageRequest argument
            var sendMessageResponse = _sqsClient.SendMessage(sendRequest);
            Console.WriteLine($"SendMessage HTTP status code: {sendMessageResponse.HttpStatusCode}");

            // Send a message with the string,string arguments
            sendMessageResponse = _sqsClient.SendMessage(_queueUrl1, "SendMessage_string_string");
            Console.WriteLine($"SendMessage HTTP status code: {sendMessageResponse.HttpStatusCode}");
        }

        static void ReceiveMessage()
        {
            var receiveMessageRequest = new ReceiveMessageRequest();
            receiveMessageRequest.QueueUrl = _queueUrl1;
            receiveMessageRequest.MaxNumberOfMessages = 1;

            var receiveMessageResponse = _sqsClient.ReceiveMessage(receiveMessageRequest);
            Console.WriteLine($"ReceiveMessage HTTP status code: {receiveMessageResponse.HttpStatusCode}");

            // Delete the message from the queue
            var message = receiveMessageResponse.Messages.Single();
            var deleteMessageRequest = new DeleteMessageRequest();
            deleteMessageRequest.QueueUrl = _queueUrl1;
            deleteMessageRequest.ReceiptHandle = message.ReceiptHandle;

            var deleteMessageResponse = _sqsClient.DeleteMessage(deleteMessageRequest);
            Console.WriteLine($"DeleteMessage HTTP status code: {deleteMessageResponse.HttpStatusCode}");
        }

        static void SendMessageBatch()
        {
            var sendMessageBatchRequest = new SendMessageBatchRequest
            {
                Entries = new List<SendMessageBatchRequestEntry>
                {
                    new SendMessageBatchRequestEntry("message1", "SendMessageBatch: FirstMessageContent"),
                    new SendMessageBatchRequestEntry("message2", "SendMessageBatch: SecondMessageContent"),
                    new SendMessageBatchRequestEntry("message3", "SendMessageBatch: ThirdMessageContent")
                },
                QueueUrl = _queueUrl1
            };
            var sendMessageBatchResponse = _sqsClient.SendMessageBatch(sendMessageBatchRequest);
            Console.WriteLine($"SendMessageBatch HTTP status code: {sendMessageBatchResponse.HttpStatusCode}");

            var sendMessageBatchRequestEntryList = new List<SendMessageBatchRequestEntry>
            {
                new SendMessageBatchRequestEntry("message1", "SendMessageBatch_SendMessageBatchRequestEntries: FirstMessageContent"),
                new SendMessageBatchRequestEntry("message2", "SendMessageBatch_SendMessageBatchRequestEntries: SecondMessageContent"),
                new SendMessageBatchRequestEntry("message3", "SendMessageBatch_SendMessageBatchRequestEntries: ThirdMessageContent")
            };
            sendMessageBatchResponse = _sqsClient.SendMessageBatch(_queueUrl1, sendMessageBatchRequestEntryList);
            Console.WriteLine($"SendMessageBatch HTTP status code: {sendMessageBatchResponse.HttpStatusCode}");
        }

        static void ReceiveMessageBatch()
        {
            var receiveMessageRequest = new ReceiveMessageRequest();
            receiveMessageRequest.QueueUrl = _queueUrl1;
            receiveMessageRequest.MaxNumberOfMessages = 3;

            var receiveMessageResponse = _sqsClient.ReceiveMessage(receiveMessageRequest);
            Console.WriteLine($"ReceiveMessage HTTP status code: {receiveMessageResponse.HttpStatusCode}");

            // Delete the message batch from the queue
            var deleteMessageBatchRequest = new DeleteMessageBatchRequest()
            {
                Entries = receiveMessageResponse.Messages.Select(message => new DeleteMessageBatchRequestEntry(message.MessageId, message.ReceiptHandle)).ToList(),
                QueueUrl = _queueUrl1
            };
            var deleteMessageBatchResponse = _sqsClient.DeleteMessageBatch(deleteMessageBatchRequest);
            Console.WriteLine($"DeleteMessageBatch HTTP status code: {deleteMessageBatchResponse.HttpStatusCode}");
        }

        static void PurgeQueue()
        {
            var purgeQueueRequest = new PurgeQueueRequest()
            {
                QueueUrl = _queueUrl1
            };
            var purgeQueueResponse = _sqsClient.PurgeQueue(purgeQueueRequest);
            Console.WriteLine($"PurgeQueue HTTP status code: {purgeQueueResponse.HttpStatusCode}");
        }

        static void DeleteQueue()
        {
            var deleteQueueRequest = new DeleteQueueRequest
            {
                QueueUrl = _queueUrl1
            };
            var deleteQueueResponse = _sqsClient.DeleteQueue(deleteQueueRequest);
            Console.WriteLine($"DeleteQueue HTTP status code: {deleteQueueResponse.HttpStatusCode}");
        }

#endif
        #endregion

        #region Async Methods (.NET Framework and .NET Core)
        static async Task CreateSqsQueueAsync()
        {
            var createQueueRequest = new CreateQueueRequest();

            createQueueRequest.QueueName = QueueName1;
            var attrs = new Dictionary<string, string>();
            attrs.Add(QueueAttributeName.VisibilityTimeout, "0");
            createQueueRequest.Attributes = attrs;
            var response1 = await _sqsClient.CreateQueueAsync(createQueueRequest);
            Console.WriteLine($"CreateQueueAsync(CreateQueueRequest) HTTP status code: {response1.HttpStatusCode}");

            var response2 = await _sqsClient.CreateQueueAsync(QueueName2);
            Console.WriteLine($"CreateQueueAsync(string) HTTP status code: {response2.HttpStatusCode}");
        }

        static async Task ListQueuesAsync()
        {
            var listQueuesResponse = await _sqsClient.ListQueuesAsync(new ListQueuesRequest());
            Console.WriteLine($"ListQueuesAsync HTTP status code: {listQueuesResponse.HttpStatusCode}");
        }

        static async Task GetQueueUrlAsync()
        {
            var getQueueUrlRequest = new GetQueueUrlRequest
            {
                QueueName = QueueName1
            };

            var response1 = await _sqsClient.GetQueueUrlAsync(getQueueUrlRequest);
            Console.WriteLine($"GetQueueUrlAsync(GetQueueUrlRequest) HTTP status code: {response1.HttpStatusCode}");
            _queueUrl1 = response1.QueueUrl;

            var response2 = await _sqsClient.GetQueueUrlAsync(QueueName2);
            Console.WriteLine($"GetQueueUrlAsync(string) HTTP status code: {response2.HttpStatusCode}");
            _queueUrl2 = response2.QueueUrl;
        }

        static async Task SendMessageAsync()
        {
            var sendRequest = new SendMessageRequest();
            sendRequest.QueueUrl = _queueUrl1;
            sendRequest.MessageBody = "SendMessageAsync_SendMessageRequest";

            // Send a message with the SendMessageRequest argument
            var sendMessageResponse = await _sqsClient.SendMessageAsync(sendRequest);
            Console.WriteLine($"SendMessageAsync HTTP status code: {sendMessageResponse.HttpStatusCode}");

            // Send a message with the string,string arguments
            sendMessageResponse = await _sqsClient.SendMessageAsync(_queueUrl1, "SendMessageAsync_string_string");
            Console.WriteLine($"SendMessageAsync HTTP status code: {sendMessageResponse.HttpStatusCode}");
        }

        static async Task ReceiveMessageAndDeleteMessageAsync()
        {
            // Receive and delete the first message
            var receiveMessageRequest = new ReceiveMessageRequest();
            receiveMessageRequest.QueueUrl = _queueUrl1;
            receiveMessageRequest.MaxNumberOfMessages = 1;

            var receiveMessageResponse1 = await _sqsClient.ReceiveMessageAsync(receiveMessageRequest);
            Console.WriteLine($"ReceiveMessageAsync(ReceiveMessageRequest) HTTP status code: {receiveMessageResponse1.HttpStatusCode}");

            var deleteMessageRequest = new DeleteMessageRequest();
            deleteMessageRequest.QueueUrl = _queueUrl1;
            deleteMessageRequest.ReceiptHandle = receiveMessageResponse1.Messages.Single().ReceiptHandle;

            var deleteMessageResponse1 = await _sqsClient.DeleteMessageAsync(deleteMessageRequest);
            Console.WriteLine($"DeleteMessageAsync(DeleteMessageRequest) HTTP status code: {deleteMessageResponse1.HttpStatusCode}");

            // Receive and delete the first message
            var receiveMessageResponse2 = await _sqsClient.ReceiveMessageAsync(_queueUrl1);
            Console.WriteLine($"ReceiveMessageAsync(string) HTTP status code: {receiveMessageResponse2.HttpStatusCode}");

            var deleteMessageResponse2 = await _sqsClient.DeleteMessageAsync(_queueUrl1, receiveMessageResponse2.Messages.Single().ReceiptHandle);
            Console.WriteLine($"DeleteMessageAsync(string, string) HTTP status code: {deleteMessageResponse2.HttpStatusCode}");
        }

        static async Task SendMessageBatchAsync()
        {
            var sendMessageBatchRequest = new SendMessageBatchRequest
            {
                Entries = new List<SendMessageBatchRequestEntry>
                {
                    new SendMessageBatchRequestEntry("message1", "SendMessageBatchAsync: FirstMessageContent"),
                    new SendMessageBatchRequestEntry("message2", "SendMessageBatchAsync: SecondMessageContent"),
                    new SendMessageBatchRequestEntry("message3", "SendMessageBatchAsync: ThirdMessageContent")
                },
                QueueUrl = _queueUrl1
            };
            var response1 = await _sqsClient.SendMessageBatchAsync(sendMessageBatchRequest);
            Console.WriteLine($"SendMessageBatchAsync HTTP status code: {response1.HttpStatusCode}");

            var sendMessageBatchRequestEntryList = new List<SendMessageBatchRequestEntry>
            {
                new SendMessageBatchRequestEntry("message1", "SendMessageBatchAsync_SendMessageBatchRequestEntries: FirstMessageContent"),
                new SendMessageBatchRequestEntry("message2", "SendMessageBatchAsync_SendMessageBatchRequestEntries: SecondMessageContent"),
                new SendMessageBatchRequestEntry("message3", "SendMessageBatchAsync_SendMessageBatchRequestEntries: ThirdMessageContent")
            };
            var response2 = await _sqsClient.SendMessageBatchAsync(_queueUrl1, sendMessageBatchRequestEntryList);
            Console.WriteLine($"SendMessageBatchAsync HTTP status code: {response2.HttpStatusCode}");
        }

        static async Task ReceiveMessagesAndDeleteMessageBatchAsync()
        {
            // Get the first 3 messages and delete them as a batch
            var receiveMessageRequest = new ReceiveMessageRequest();
            receiveMessageRequest.QueueUrl = _queueUrl1;
            receiveMessageRequest.MaxNumberOfMessages = 3;

            var receiveMessageResponse = await _sqsClient.ReceiveMessageAsync(receiveMessageRequest);
            Console.WriteLine($"ReceiveMessageAsync HTTP status code: {receiveMessageResponse.HttpStatusCode}");

            var deleteMessageBatchRequest = new DeleteMessageBatchRequest()
            {
                Entries = receiveMessageResponse.Messages.Select(message => new DeleteMessageBatchRequestEntry(message.MessageId, message.ReceiptHandle)).ToList(),
                QueueUrl = _queueUrl1
            };
            var deleteMessageBatchResponse1 = await _sqsClient.DeleteMessageBatchAsync(deleteMessageBatchRequest);
            Console.WriteLine($"DeleteMessageBatchAsync HTTP status code: {deleteMessageBatchResponse1.HttpStatusCode}");

            // Get the second 3 messages and delete them as a batch
            // Re-use the already parameterized request object
            var receiveMessageResponse2 = await _sqsClient.ReceiveMessageAsync(receiveMessageRequest);
            Console.WriteLine($"ReceiveMessageAsync HTTP status code: {receiveMessageResponse2.HttpStatusCode}");

            var entries = receiveMessageResponse2.Messages.Select(message => new DeleteMessageBatchRequestEntry(message.MessageId, message.ReceiptHandle)).ToList();
            var deleteMessageBatchResponse2 = await _sqsClient.DeleteMessageBatchAsync(_queueUrl1, entries);
            Console.WriteLine($"DeleteMessageBatchAsync HTTP status code: {deleteMessageBatchResponse2.HttpStatusCode}");
        }

        static async Task PurgeQueueAsync()
        {
            var purgeQueueRequest = new PurgeQueueRequest()
            {
                QueueUrl = _queueUrl1
            };
            var purgeQueueResponse = await _sqsClient.PurgeQueueAsync(purgeQueueRequest);
            Console.WriteLine($"PurgeQueueAsync HTTP status code: {purgeQueueResponse.HttpStatusCode}");
        }

        static async Task DeleteQueueAsync()
        {
            var deleteQueueRequest = new DeleteQueueRequest
            {
                QueueUrl = _queueUrl1
            };
            var response1 = await _sqsClient.DeleteQueueAsync(deleteQueueRequest);
            Console.WriteLine($"DeleteQueueAsync(DeleteQueueRequest) HTTP status code: {response1.HttpStatusCode}");

            var response2 = await _sqsClient.DeleteQueueAsync(_queueUrl2);
            Console.WriteLine($"DeleteQueueAsync(string) HTTP status code: {response2.HttpStatusCode}");
        }
        #endregion
    }
}
