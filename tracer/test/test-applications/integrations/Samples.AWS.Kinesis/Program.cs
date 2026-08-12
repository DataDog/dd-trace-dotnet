using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.Kinesis;

namespace Samples.AWS.Kinesis
{
    public class Program
    {
        // Keep in sync with the name in KinesisTests - this is used to be able to remove the trace/spans associated with this
        private const string PreTestCleanupOperationName = "KINESIS-CLEAN-UP-SHOULD-NOT-BE-IN-SNAPSHOT";

        private static async Task Main(string[] args)
        {
            // Set up AmazonSQSConfig and redirect to the local message queue instance
            var kinesisClient = GetAmazonKinesisClient();
            await TryDeleteStreamBeforeRunningTests(kinesisClient);

#if NETFRAMEWORK
            SyncHelpers.StartKinesisTasks(kinesisClient);
#endif
            await AsyncHelpers.StartKinesisTasks(kinesisClient);
        }

        private static async Task TryDeleteStreamBeforeRunningTests(AmazonKinesisClient kinesisClient)
        {
            using (SampleHelpers.CreateScope(PreTestCleanupOperationName))
            {
                try
                {
                    await AsyncHelpers.DeleteStreamAsync(kinesisClient);
                }
                catch
                {
                    // Saw flake in CI where the stream from the prior test wasn't deleted (they are re-used)
                    // we can't easily change the stream names to be unique as the DSM hashes use it
                    // So this is a best effort to try
                }

                await Task.Delay(1000);
            }
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
