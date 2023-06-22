using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace Samples.AWS.SNS
{
    public class Program
    {
        private static async Task Main(string[] args)
        {
            var snsClient = GetAmazonSimpleNotificationServiceClient();
            var sqsClient = GetAmazonSQSClient();

            string topicName = "MyTopic";
            string queueName = "MyQueue";

            string topicArn = await GetOrCreateTopicAsync(snsClient, topicName);
            string queueUrl = await GetOrCreateQueueAsync(sqsClient, queueName);

            await SubscribeQueueToTopicAsync(sqsClient, snsClient, queueUrl, topicArn);

            string message = "Hello, SNS!";

            await PublishMessageAsync(snsClient, topicArn, message);
            await DeleteTopicAsync(snsClient, topicArn);
        }
        private static AmazonSQSClient GetAmazonSQSClient()
        {
            if (Environment.GetEnvironmentVariable("AWS_ACCESSKEY") is string accessKey &&
                Environment.GetEnvironmentVariable("AWS_SECRETKEY") is string secretKey &&
                Environment.GetEnvironmentVariable("AWS_REGION") is string region)
            {
                var awsCredentials = new BasicAWSCredentials(accessKey, secretKey);
                var sqsConfig = new AmazonSQSConfig 
                { 
                    ServiceURL = "http://localhost:4566",
                    RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region)
                };
                return new AmazonSQSClient(awsCredentials, sqsConfig);
            }
            else
            {
                var awsCredentials = new BasicAWSCredentials("x", "x");
                var sqsConfig = new AmazonSQSConfig { ServiceURL = "http://localhost:4566" };
                return new AmazonSQSClient(awsCredentials, sqsConfig);
            }
        }

        private static async Task<string> GetOrCreateQueueAsync(AmazonSQSClient sqsClient, string queueName)
        {
            var createQueueRequest = new CreateQueueRequest { QueueName = queueName };
            var createQueueResponse = await sqsClient.CreateQueueAsync(createQueueRequest);

            return createQueueResponse.QueueUrl;
        }
        

        private static async Task SubscribeQueueToTopicAsync(AmazonSQSClient sqsClient, AmazonSimpleNotificationServiceClient snsClient, string queueUrl, string topicArn)
        {
            var attributesResponse = await sqsClient.GetQueueAttributesAsync(new GetQueueAttributesRequest
            {
                QueueUrl = queueUrl,
                AttributeNames = new List<string> { "QueueArn" }
            });

            string queueArn = attributesResponse.Attributes["QueueArn"];

            await snsClient.SubscribeQueueAsync(topicArn, sqsClient, queueUrl);
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
            // Create the topic. This is a no-op if the topic has already been created.
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
            Console.WriteLine("topicArn:");
            Console.WriteLine(topicArn);
            Console.WriteLine("message:");
            Console.WriteLine(message);
            Console.WriteLine("before PublishAsync");
            try
            {
                var response = await snsClient.PublishAsync(request);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to PublishAsync: {ex.Message}");
                throw;
            }
            Console.WriteLine("after pub async");
            Console.WriteLine("Message published");
        }

        public static async Task DeleteTopicAsync(AmazonSimpleNotificationServiceClient snsClient, string topicArn)
        {
            var deleteTopicRequest = new DeleteTopicRequest { TopicArn = topicArn };
            await snsClient.DeleteTopicAsync(deleteTopicRequest);

            Console.WriteLine($"Topic deleted. Topic ARN: {topicArn}");
        }
    }
}
