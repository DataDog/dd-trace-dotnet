using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.Kinesis;

namespace Samples.AWS.Kinesis
{
    public class Program
    {
        private static async Task Main(string[] args)
        {
            // Set up AmazonSQSConfig and redirect to the local message queue instance
            var kinesisClient = GetAmazonKinesisClient();

#if NETFRAMEWORK
            SyncHelpers.StartKinesisTasks(kinesisClient);
#endif
            await AsyncHelpers.StartKinesisTasks(kinesisClient);
        }

        private static AmazonKinesisClient GetAmazonKinesisClient()
        {
            if (Environment.GetEnvironmentVariable("AWS_ACCESSKEY") is string accessKey &&
                Environment.GetEnvironmentVariable("AWS_SECRETKEY") is string secretKey &&
                Environment.GetEnvironmentVariable("AWS_REGION") is string region)
            {
                var awsCredentials = new BasicAWSCredentials(accessKey, secretKey);
                return new AmazonKinesisClient(awsCredentials, Amazon.RegionEndpoint.GetBySystemName(region));
            }
            else
            {
                var awsCredentials = new BasicAWSCredentials("x", "x");
                var kinesisConfig = new AmazonKinesisConfig { ServiceURL = "http://" + Host() };
                return new AmazonKinesisClient(awsCredentials, kinesisConfig);
            }
        }

        private static string Host()
        {
            return Environment.GetEnvironmentVariable("AWS_SDK_HOST") ?? "localhost:4566";
        }
    }
}
