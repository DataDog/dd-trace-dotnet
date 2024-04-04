using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.SQS;

namespace Samples.AWS.SQS
{
    public class Program
    {
        private static async Task Main(string[] args)
        {
            // Set up AmazonSQSConfig and redirect to the local message queue instance
            var sqsClient = GetAmazonSQSClient();

#if NETFRAMEWORK
            SyncHelpers.SendAndReceiveMessages(sqsClient);
#endif
            if (Environment.GetEnvironmentVariable("TEST_BATCH") is string batch &&
                Environment.GetEnvironmentVariable("TEST_SAME_THREAD") is string thread &&
                Environment.GetEnvironmentVariable("TEST_INJECT") is string inject)
            {
                var scenario = AsyncHelpers.Scenario.None;
                if (batch == "1")
                {
                    scenario |= AsyncHelpers.Scenario.Batch;
                }

                if (thread == "1")
                {
                    scenario |= AsyncHelpers.Scenario.SameThread;
                }

                if (inject == "1")
                {
                    scenario |= AsyncHelpers.Scenario.Injected;
                }

                await AsyncHelpers.RunSpecificScenario(sqsClient, scenario);
            }
            else
            {
                await AsyncHelpers.RunAllScenarios(sqsClient);
            }
        }

        private static AmazonSQSClient GetAmazonSQSClient()
        {
            if (Environment.GetEnvironmentVariable("AWS_ACCESSKEY") is string accessKey &&
                Environment.GetEnvironmentVariable("AWS_SECRETKEY") is string secretKey &&
                Environment.GetEnvironmentVariable("AWS_REGION") is string region)
            {
                var awsCredentials = new BasicAWSCredentials(accessKey, secretKey);
                return new AmazonSQSClient(awsCredentials, Amazon.RegionEndpoint.GetBySystemName(region));
            }
            else
            {
                var awsCredentials = new BasicAWSCredentials("x", "x");
                var sqsConfig = new AmazonSQSConfig { ServiceURL = "http://" + Host() };
                return new AmazonSQSClient(awsCredentials, sqsConfig);
            }
        }

        private static string Host()
        {
            return Environment.GetEnvironmentVariable("AWS_SDK_HOST") ?? "localhost:4566";
        }
    }
}
