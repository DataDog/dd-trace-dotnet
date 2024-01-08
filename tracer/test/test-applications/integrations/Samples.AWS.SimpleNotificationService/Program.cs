using System;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;

namespace Samples.AWS.SimpleNotificationService
{
    public class Program
    {
        private static async Task Main(string[] args)
        {
            var snsClient = GetAmazonSimpleNotificationServiceClient();

#if NETFRAMEWORK
            SyncHelpers.StartSNSTasks(snsClient);
#endif
            await AsyncHelpers.StartSNSTasks(snsClient);
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
            return Environment.GetEnvironmentVariable("AWS_SDK_HOST") ?? "localhost:4566";
        }
    }
}
