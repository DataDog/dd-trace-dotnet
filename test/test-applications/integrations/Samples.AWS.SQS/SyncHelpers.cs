#if NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Datadog.Trace;

namespace Samples.AWS.SQS
{
    static class SyncHelpers
    {
        private const string SingleQueue = "MySQSQueue";
        private const string BatchedQueue = "MySQSQueue2";

        private static string _singleQueueUrl;
        private static string _batchedQueueUrl;
        private static Barrier _messsageBarrier = new Barrier(2);
        private static Barrier _messsageBatchBarrier = new Barrier(2);

        public static void SendAndReceiveMessages(AmazonSQSClient sqsClient)
        {
            Console.WriteLine("Starting Synchronous Receive Threads");

            var receiveMessageTask = Task.Run(() => ReceiveMessageAndDeleteMessage(sqsClient)); // Run on separate thread
            var receiveMessageBatchTask = Task.Run(() => ReceiveMessagesAndDeleteMessageBatch(sqsClient)); // Run on separate thread

            Console.WriteLine("Beginning Synchronous methods");

            using (var scope = Tracer.Instance.StartActive("sync-methods"))
            {
                CreateSqsQueue(sqsClient);
                ListQueues(sqsClient);
                GetQueueUrl(sqsClient);
                SendMessage(sqsClient);
                _messsageBarrier.SignalAndWait(); // Start the SingleMessage receive loop
                receiveMessageTask.Wait();

                SendMessageBatch(sqsClient);
                _messsageBatchBarrier.SignalAndWait(); // Start the BatchedMessage receive loop
                receiveMessageBatchTask.Wait();

                PurgeQueue(sqsClient);
                DeleteQueue(sqsClient);
            }
        }

        private static void CreateSqsQueue(AmazonSQSClient sqsClient)
        {
            var createQueueRequest = new CreateQueueRequest();

            createQueueRequest.QueueName = SingleQueue;
            var attrs = new Dictionary<string, string>();
            attrs.Add(QueueAttributeName.VisibilityTimeout, "0");
            createQueueRequest.Attributes = attrs;
            var response1 = sqsClient.CreateQueue(createQueueRequest);
            Console.WriteLine($"CreateQueue(CreateQueueRequest) HTTP status code: {response1.HttpStatusCode}");

            var response2 = sqsClient.CreateQueue(BatchedQueue);
            Console.WriteLine($"CreateQueue(string) HTTP status code: {response2.HttpStatusCode}");
        }

        private static void ListQueues(AmazonSQSClient sqsClient)
        {
            var listQueuesResponse = sqsClient.ListQueues(new ListQueuesRequest());
            Console.WriteLine($"ListQueues HTTP status code: {listQueuesResponse.HttpStatusCode}");
        }

        private static void GetQueueUrl(AmazonSQSClient sqsClient)
        {
            var getQueueUrlRequest = new GetQueueUrlRequest
            {
                QueueName = SingleQueue
            };

            var response1 = sqsClient.GetQueueUrl(getQueueUrlRequest);
            Console.WriteLine($"GetQueueUrl(GetQueueUrlRequest) HTTP status code: {response1.HttpStatusCode}");
            _singleQueueUrl = response1.QueueUrl;

            var response2 = sqsClient.GetQueueUrl(BatchedQueue);
            Console.WriteLine($"GetQueueUrl(string) HTTP status code: {response2.HttpStatusCode}");
            _batchedQueueUrl = response2.QueueUrl;
        }

        private static void SendMessage(AmazonSQSClient sqsClient)
        {
            var sendRequest = new SendMessageRequest();
            sendRequest.QueueUrl = _singleQueueUrl;
            sendRequest.MessageBody = "SendMessage_SendMessageRequest";

            // Send a message with the SendMessageRequest argument
            var sendMessageResponse = sqsClient.SendMessage(sendRequest);
            Console.WriteLine($"SendMessage HTTP status code: {sendMessageResponse.HttpStatusCode}");

            // Send a message with the string,string arguments
            sendMessageResponse = sqsClient.SendMessage(_singleQueueUrl, "SendMessage_string_string");
            Console.WriteLine($"SendMessage HTTP status code: {sendMessageResponse.HttpStatusCode}");
        }

        private static void ReceiveMessageAndDeleteMessage(AmazonSQSClient sqsClient)
        {
            _messsageBarrier.SignalAndWait();

            // Receive and delete the first message
            var receiveMessageRequest = new ReceiveMessageRequest();
            receiveMessageRequest.QueueUrl = _singleQueueUrl;
            receiveMessageRequest.MaxNumberOfMessages = 1;
            receiveMessageRequest.MessageAttributeNames = new List<string> { ".*" };

            var receiveMessageResponse1 = sqsClient.ReceiveMessage(receiveMessageRequest);
            Console.WriteLine($"ReceiveMessage(ReceiveMessageRequest) HTTP status code: {receiveMessageResponse1.HttpStatusCode}");
            if (receiveMessageResponse1.HttpStatusCode == HttpStatusCode.OK)
            {
                var deleteMessageRequest = new DeleteMessageRequest();
                deleteMessageRequest.QueueUrl = _singleQueueUrl;
                deleteMessageRequest.ReceiptHandle = receiveMessageResponse1.Messages.Single().ReceiptHandle;

                var deleteMessageResponse1 = sqsClient.DeleteMessage(deleteMessageRequest);
                Console.WriteLine($"DeleteMessage(DeleteMessageRequest) HTTP status code: {deleteMessageResponse1.HttpStatusCode}");
            }

            // Receive and delete the first message
            var receiveMessageResponse2 = sqsClient.ReceiveMessage(_singleQueueUrl);
            Console.WriteLine($"ReceiveMessage(string) HTTP status code: {receiveMessageResponse2.HttpStatusCode}");
            if (receiveMessageResponse2.HttpStatusCode == HttpStatusCode.OK)
            {
                var deleteMessageResponse2 = sqsClient.DeleteMessage(_singleQueueUrl, receiveMessageResponse2.Messages.Single().ReceiptHandle);
                Console.WriteLine($"DeleteMessage(string, string) HTTP status code: {deleteMessageResponse2.HttpStatusCode}");
            }
        }

        private static void SendMessageBatch(AmazonSQSClient sqsClient)
        {
            var sendMessageBatchRequest = new SendMessageBatchRequest
            {
                Entries = new List<SendMessageBatchRequestEntry>
                {
                    new SendMessageBatchRequestEntry("message1", "SendMessageBatch: FirstMessageContent"),
                    new SendMessageBatchRequestEntry("message2", "SendMessageBatch: SecondMessageContent"),
                    new SendMessageBatchRequestEntry("message3", "SendMessageBatch: ThirdMessageContent")
                },
                QueueUrl = _batchedQueueUrl
            };
            var response1 = sqsClient.SendMessageBatch(sendMessageBatchRequest);
            Console.WriteLine($"SendMessageBatch HTTP status code: {response1.HttpStatusCode}");

            var sendMessageBatchRequestEntryList = new List<SendMessageBatchRequestEntry>
            {
                new SendMessageBatchRequestEntry("message1", "SendMessageBatch_SendMessageBatchRequestEntries: FirstMessageContent"),
                new SendMessageBatchRequestEntry("message2", "SendMessageBatch_SendMessageBatchRequestEntries: SecondMessageContent"),
                new SendMessageBatchRequestEntry("message3", "SendMessageBatch_SendMessageBatchRequestEntries: ThirdMessageContent")
            };
            var response2 = sqsClient.SendMessageBatch(_batchedQueueUrl, sendMessageBatchRequestEntryList);
            Console.WriteLine($"SendMessageBatch HTTP status code: {response2.HttpStatusCode}");
        }

        private static void ReceiveMessagesAndDeleteMessageBatch(AmazonSQSClient sqsClient)
        {
            _messsageBatchBarrier.SignalAndWait();

            // Get the first 3 messages and delete them as a batch
            var receiveMessageRequest = new ReceiveMessageRequest();
            receiveMessageRequest.QueueUrl = _batchedQueueUrl;
            receiveMessageRequest.MaxNumberOfMessages = 3;
            receiveMessageRequest.MessageAttributeNames = new List<string> { ".*" };

            var receiveMessageResponse = sqsClient.ReceiveMessage(receiveMessageRequest);
            Console.WriteLine($"ReceiveMessage HTTP status code: {receiveMessageResponse.HttpStatusCode}");

            if (receiveMessageResponse.HttpStatusCode == HttpStatusCode.OK)
            {
                var deleteMessageBatchRequest = new DeleteMessageBatchRequest()
                {
                    Entries = receiveMessageResponse.Messages.Select(message => new DeleteMessageBatchRequestEntry(message.MessageId, message.ReceiptHandle)).ToList(),
                    QueueUrl = _batchedQueueUrl
                };
                var deleteMessageBatchResponse1 = sqsClient.DeleteMessageBatch(deleteMessageBatchRequest);
                Console.WriteLine($"DeleteMessageBatch HTTP status code: {deleteMessageBatchResponse1.HttpStatusCode}");
            }

            // Get the second 3 messages and delete them as a batch
            // Re-use the already parameterized request object
            var receiveMessageResponse2 = sqsClient.ReceiveMessage(receiveMessageRequest);
            Console.WriteLine($"ReceiveMessage HTTP status code: {receiveMessageResponse2.HttpStatusCode}");

            if (receiveMessageResponse2.HttpStatusCode == HttpStatusCode.OK)
            {
                var entries = receiveMessageResponse2.Messages.Select(message => new DeleteMessageBatchRequestEntry(message.MessageId, message.ReceiptHandle)).ToList();
                var deleteMessageBatchResponse2 = sqsClient.DeleteMessageBatch(_batchedQueueUrl, entries);
                Console.WriteLine($"DeleteMessageBatch HTTP status code: {deleteMessageBatchResponse2.HttpStatusCode}");
            }
        }

        private static void PurgeQueue(AmazonSQSClient sqsClient)
        {
            var purgeQueueRequest = new PurgeQueueRequest()
            {
                QueueUrl = _singleQueueUrl
            };
            var purgeQueueResponse = sqsClient.PurgeQueue(purgeQueueRequest);
            Console.WriteLine($"PurgeQueue HTTP status code: {purgeQueueResponse.HttpStatusCode}");
        }

        private static void DeleteQueue(AmazonSQSClient sqsClient)
        {
            var deleteQueueRequest = new DeleteQueueRequest
            {
                QueueUrl = _singleQueueUrl
            };
            var response1 = sqsClient.DeleteQueue(deleteQueueRequest);
            Console.WriteLine($"DeleteQueue(DeleteQueueRequest) HTTP status code: {response1.HttpStatusCode}");

            var response2 = sqsClient.DeleteQueue(_batchedQueueUrl);
            Console.WriteLine($"DeleteQueue(string) HTTP status code: {response2.HttpStatusCode}");
        }
    }
}
#endif
