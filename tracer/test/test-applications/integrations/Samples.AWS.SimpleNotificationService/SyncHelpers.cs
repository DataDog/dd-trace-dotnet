#if NETFRAMEWORK

using System;
using System.Threading;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

namespace Samples.AWS.SimpleNotificationService
{
    static class SyncHelpers
    {
        private const string TopicName = "MyTopic";
        private const string Message = "Hello, SNS!";

        public static void StartSNSTasks(AmazonSimpleNotificationServiceClient snsClient)
        {
            Console.WriteLine("Beginning Sync methods");
            using (var scope = SampleHelpers.CreateScope("sync-methods"))
            {
                var topicArn = CreateTopic(snsClient, TopicName);

                // Needed in order to allow resource to be in
                // Ready status.
                Thread.Sleep(1000);

                PublishMessage(snsClient, topicArn);
#if AWS_SNS_3_7_3
                PublishBatch(snsClient, topicArn);
#endif
                DeleteTopic(snsClient, topicArn);

                // Needed in order to allow resource to be deleted
                Thread.Sleep(1000);
            }
        }

        private static void PublishMessage(AmazonSimpleNotificationServiceClient snsClient, string topicArn)
        {
            var request = new PublishRequest { TopicArn = topicArn, Message = Message };

            var response = snsClient.Publish(request);

            Console.WriteLine($"PublishMessageAsync(PublishRequest) HTTP status code: {response.HttpStatusCode}");
        }
#if AWS_SNS_3_7_3
        private static void PublishBatch(AmazonSimpleNotificationServiceClient snsClient, string topicArn)
        {
            var publishBatchRequest = new PublishBatchRequest
            {
                TopicArn = topicArn,
                PublishBatchRequestEntries = new()
                {
                    new()
                    {
                        Id = "MessageId-1",
                        Message = "Message without MessageAttributes!",
                    },
                    new()
                    {
                        Id = "MessageId-2",
                        Message = "Another message without MessageAttributes!",
                    }
                }
            };

            var response = snsClient.PublishBatch(publishBatchRequest);
            
            Console.WriteLine($"PublishBatch(PublishBatchRequest) HTTP status code: {response.HttpStatusCode}");
        }
#endif
        private static void DeleteTopic(AmazonSimpleNotificationServiceClient snsClient, string topicArn)
        {
            var deleteTopicRequest = new DeleteTopicRequest { TopicArn = topicArn };

            var response = snsClient.DeleteTopic(deleteTopicRequest);

            Console.WriteLine($"DeleteTopic(DeleteTopicRequest) HTTP status code: {response.HttpStatusCode}");
        }

        private static string CreateTopic(AmazonSimpleNotificationServiceClient snsClient, string topicName)
        {
            var createTopicRequest = new CreateTopicRequest { Name = topicName };

            var response = snsClient.CreateTopic(createTopicRequest);

            Console.WriteLine($"CreateTopic(CreateTopicRequest) HTTP status code: {response.HttpStatusCode}");

            return response.TopicArn;
        }
    }

}
#endif
