using System;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

namespace Samples.AWS.SNS
{
    public class Program
    {
        private static async Task Main(string[] args)
        {
            var snsClient = GetAmazonSimpleNotificationServiceClient();

            string topicName = "MyTopic";
            string topicArn = await GetOrCreateTopicAsync(snsClient, topicName);
            string message = "Hello, SNS!";

            await PublishMessageAsync(snsClient, topicArn, message);
        }

        private static AmazonSimpleNotificationServiceClient GetAmazonSimpleNotificationServiceClient()
        {
            if (Environment.GetEnvironmentVariable("AWS_ACCESSKEY") is string accessKey &&
                Environment.GetEnvironmentVariable("AWS_SECRETKEY") is string secretKey &&
                Environment.GetEnvironmentVariable("AWS_REGION") is string region)
            {
                var awsCredentials = new BasicAWSCredentials(accessKey, secretKey);
                return new AmazonSimpleNotificationServiceClient(awsCredentials, Amazon.RegionEndpoint.GetBySystemName(region));
            }
            else
            {
                var awsCredentials = new BasicAWSCredentials("x", "x");
                var snsConfig = new AmazonSimpleNotificationServiceConfig { ServiceURL = "http://" + Host() };
                return new AmazonSimpleNotificationServiceClient(awsCredentials, snsConfig);
            }
        }

        private static string Host()
        {
            return Environment.GetEnvironmentVariable("AWS_SNS_HOST") ?? "localhost:4566";
        }

        private static async Task<string> GetOrCreateTopicAsync(AmazonSimpleNotificationServiceClient snsClient, string topicName)
        {
            var listTopicsRequest = new ListTopicsRequest();
            ListTopicsResponse listTopicsResponse;

            do
            {
                listTopicsResponse = await snsClient.ListTopicsAsync(listTopicsRequest);

                foreach (var topic in listTopicsResponse.Topics)
                {
                    if (topic.TopicArn.EndsWith($":{topicName}", StringComparison.OrdinalIgnoreCase))
                    {
                        return topic.TopicArn;
                    }
                }

                listTopicsRequest.NextToken = listTopicsResponse.NextToken;
            } while (listTopicsResponse.NextToken != null);

            // Topic not found, create a new one
            var createTopicRequest = new CreateTopicRequest { Name = topicName };
            var createTopicResponse = await snsClient.CreateTopicAsync(createTopicRequest);

            return createTopicResponse.TopicArn;
        }
        private static async Task PublishMessageAsync(AmazonSimpleNotificationServiceClient snsClient, string topicArn, string message)
        {
            var request = new PublishRequest
            {
                TopicArn = topicArn,
                Message = message
            };

            var response = await snsClient.PublishAsync(request);

            Console.WriteLine($"Message published. Message ID: {response.MessageId}");
        }
    }
}
