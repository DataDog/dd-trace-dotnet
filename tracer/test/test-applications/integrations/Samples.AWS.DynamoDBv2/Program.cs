using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.DynamoDBv2;

namespace Samples.AWS.DynamoDBv2
{
    public class Program
    {
        private static async Task Main(string[] args)
        {
            // Set up AmazonDynamoDBConfig and redirect to the local message queue instance
            var dynamoDBClient = GetAmazonDynamoDBClient();

#if NETFRAMEWORK
            SyncHelpers.StartDynamoDBTasks(dynamoDBClient);
#endif
            await AsyncHelpers.StartDynamoDBTasks(dynamoDBClient);
        }

        private static AmazonDynamoDBClient GetAmazonDynamoDBClient()
        {
            if (Environment.GetEnvironmentVariable("AWS_ACCESSKEY") is string accessKey &&
                Environment.GetEnvironmentVariable("AWS_SECRETKEY") is string secretKey &&
                Environment.GetEnvironmentVariable("AWS_REGION") is string region)
            {
                var awsCredentials = new BasicAWSCredentials(accessKey, secretKey);
                return new AmazonDynamoDBClient(awsCredentials, Amazon.RegionEndpoint.GetBySystemName(region));
            }
            else
            {
                var awsCredentials = new BasicAWSCredentials("x", "x");
                var dynamoDBConfig = new AmazonDynamoDBConfig { ServiceURL = "http://" + Host() };
                return new AmazonDynamoDBClient(awsCredentials, dynamoDBConfig);
            }
        }

        private static string Host()
        {
            return Environment.GetEnvironmentVariable("AWS_SDK_HOST") ?? "localhost:4566";
        }
    }
}
