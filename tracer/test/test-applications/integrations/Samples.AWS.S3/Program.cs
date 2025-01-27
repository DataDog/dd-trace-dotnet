using System;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Samples.AWS.S3;

namespace Samples.AWS.S3
{
    public class Program
    {
        private static async Task Main(string[] args)
        {
            var s3Client = GetAmazonS3Client();
            try 
            {
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = "bucket" });
            }
            catch (AmazonS3Exception) { } // Ignore if bucket already exists
    
            Console.WriteLine("[App] Calling PutObjectAsync...");
            await s3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = "bucket",
                Key = "key",
                ContentBody = "content"
            });
            Console.WriteLine("[App] Done calling PutObjectAsync.");
#if NETFRAMEWORK
            SyncHelpers.StartS3Tasks(s3Client);
#endif
            await AsyncHelpers.StartS3Tasks(s3Client);
        }

        private static AmazonS3Client GetAmazonS3Client()
        {
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
                    ForcePathStyle = true, // Add this line
                    UseHttp = true
                };
                return new AmazonS3Client(awsCredentials, s3Config);
            }
        }

        private static string Host()
        {
            return Environment.GetEnvironmentVariable("AWS_SDK_HOST") ?? "localhost:4566";
        }
    }
}
