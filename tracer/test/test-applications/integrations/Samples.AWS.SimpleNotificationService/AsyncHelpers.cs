using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

namespace Samples.AWS.SimpleNotificationService
{
    static class AsyncHelpers
    {
        private const string TopicName = "MyTopic";
        private const string Message = "Hello, SNS!";


        public static async Task StartSNSTasks(AmazonSimpleNotificationServiceClient snsClient)
        {
            Console.WriteLine("Beginning Async methods");
            using (var scope = SampleHelpers.CreateScope("async-methods"))
            {
                var topicArn = await CreateTopicAsync(snsClient, TopicName);

                // Needed in order to allow resource to be in
                // Ready status.
                Thread.Sleep(1000);

                await PublishMessageAsync(snsClient, topicArn);
#if AWS_SNS_3_7_3
                await PublishBatchAsync(snsClient, topicArn);
#endif
                await DeleteTopicAsync(snsClient, topicArn);

                // Needed in order to allow resource to be deleted
                Thread.Sleep(1000);
            }
        }

        private static async Task PublishMessageAsync(AmazonSimpleNotificationServiceClient snsClient, string topicArn)
        {
            var request = new PublishRequest { TopicArn = topicArn, Message = Message };

            var response = await snsClient.PublishAsync(request);

            Console.WriteLine($"PublishMessageAsync(PublishRequest) HTTP status code: {response.HttpStatusCode}");
        }
#if AWS_SNS_3_7_3
        private static async Task PublishBatchAsync(AmazonSimpleNotificationServiceClient snsClient, string topicArn)
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

            var response = await snsClient.PublishBatchAsync(publishBatchRequest);
            
            Console.WriteLine($"PublishBatchAsync(PublishBatchRequest) HTTP status code: {response.HttpStatusCode}");
        }
#endif
        private static async Task DeleteTopicAsync(AmazonSimpleNotificationServiceClient snsClient, string topicArn)
        {
            var deleteTopicRequest = new DeleteTopicRequest { TopicArn = topicArn };

            var response = await snsClient.DeleteTopicAsync(deleteTopicRequest);

            Console.WriteLine($"DeleteTopicAsync(DeleteTopicRequest) HTTP status code: {response.HttpStatusCode}");
        }

        private static async Task<string> CreateTopicAsync(AmazonSimpleNotificationServiceClient snsClient, string topicName)
        {
            var createTopicRequest = new CreateTopicRequest { Name = topicName };

            var response = await snsClient.CreateTopicAsync(createTopicRequest);

            Console.WriteLine($"CreateTopicAsync(CreateTopicRequest) HTTP status code: {response.HttpStatusCode}");

            return response.TopicArn;
        }
    }
}
