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
            var awsCredentials = new BasicAWSCredentials("x", "x");
            var sqsConfig = new AmazonSQSConfig { ServiceURL = "http://" + Host() };
            var sqsClient = new AmazonSQSClient(awsCredentials, sqsConfig);

#if NETFRAMEWORK
            SyncHelpers.SendAndReceiveMessages(sqsClient);
#endif
            await AsyncHelpers.SendAndReceiveMessages(sqsClient);
        }

        private static string Host()
        {
            return Environment.GetEnvironmentVariable("AWS_SQS_HOST") ?? "localhost:9324";
        }
    }
}
