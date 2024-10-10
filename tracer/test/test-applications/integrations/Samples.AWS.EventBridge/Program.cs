using System;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.EventBridge;

namespace Samples.AWS.EventBridge
{
    public class Program
    {
        private static async Task Main(string[] args)
        {
            var eventBridgeClient = GetAmazonEventBridgeClient();
#if NETFRAMEWORK
            SyncHelpers.StartEventBridgeTasks(eventBridgeClient);
#endif
            await AsyncHelpers.StartEventBridgeTasks(eventBridgeClient);
        }

        private static AmazonEventBridgeClient GetAmazonEventBridgeClient()
        {
            if (Environment.GetEnvironmentVariable("AWS_ACCESSKEY") is string accessKey &&
                Environment.GetEnvironmentVariable("AWS_SECRETKEY") is string secretKey &&
                Environment.GetEnvironmentVariable("AWS_REGION") is string region)
            {
                var awsCredentials = new BasicAWSCredentials(accessKey, secretKey);
                return new AmazonEventBridgeClient(awsCredentials, Amazon.RegionEndpoint.GetBySystemName(region));
            }
            else
            {
                var awsCredentials = new BasicAWSCredentials("x", "x");
                var eventBridgeConfig = new AmazonEventBridgeConfig { ServiceURL = "http://" + Host() };
                return new AmazonEventBridgeClient(awsCredentials, eventBridgeConfig);
            }
        }

        private static string Host()
        {
            return Environment.GetEnvironmentVariable("AWS_SDK_HOST") ?? "localhost:4566";
        }
    }
}
