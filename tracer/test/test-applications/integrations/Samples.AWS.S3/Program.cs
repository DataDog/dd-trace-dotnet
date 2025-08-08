using System;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.S3;

namespace Samples.AWS.S3
{
    public class Program
    {
        private static async Task Main(string[] args)
        {
            var s3Client = GetAmazonS3Client();
            var missingEndpointClient = GetInvalidEndpointS3Client();

#if NETFRAMEWORK
            SyncHelpers.StartS3Tasks(s3Client);
            // FIXME: hangs
            // SyncHelpers.StartS3DuckTypingError(missingEndpointClient);
#endif
            await AsyncHelpers.StartS3Tasks(s3Client);
            await AsyncHelpers.StartS3DuckTypingErrorTasks(missingEndpointClient);
        }

        private static AmazonS3Client GetAmazonS3Client()
        {
            // Fixes localstack breaking change in AWSSSDK.S3 versions 3.7.412.0 and above.
            // https://github.com/aws/aws-sdk-net/issues/3610
            Environment.SetEnvironmentVariable("AWS_REQUEST_CHECKSUM_CALCULATION", "WHEN_REQUIRED");

            if (Environment.GetEnvironmentVariable("AWS_ACCESSKEY") is string accessKey &&
                Environment.GetEnvironmentVariable("AWS_SECRETKEY") is string secretKey &&
                Environment.GetEnvironmentVariable("AWS_REGION") is string region)
            {
                var awsCredentials = new BasicAWSCredentials(accessKey, secretKey);
                return new AmazonS3Client(awsCredentials, Amazon.RegionEndpoint.GetBySystemName(region));
            }
            else
            {
                var awsCredentials = new BasicAWSCredentials("x", "x");
                var s3Config = new AmazonS3Config 
                {
                    ServiceURL = "http://" + Host(),
                    ForcePathStyle = true,
                    UseHttp = true
                };
                return new AmazonS3Client(awsCredentials, s3Config);
            }
        }

        private static string Host()
        {
            return Environment.GetEnvironmentVariable("AWS_SDK_HOST") ?? "localhost:4566";
        }

        private static AmazonS3Client GetInvalidEndpointS3Client()
        {
            Console.WriteLine("Creating S3 client with invalid endpoint to reproduce duck typing issue...");
            Environment.SetEnvironmentVariable("AWS_REQUEST_CHECKSUM_CALCULATION", "WHEN_REQUIRED");

            var awsCredentials = new BasicAWSCredentials("x", "x");
            var s3Config = new AmazonS3Config
            {
                ServiceURL = "http://no-such-host-is-known-is-expected-if-you-see-it:9999",
                ForcePathStyle = true,
                UseHttp = true,
            };

            return new AmazonS3Client(awsCredentials, s3Config);
        }
    }
}
