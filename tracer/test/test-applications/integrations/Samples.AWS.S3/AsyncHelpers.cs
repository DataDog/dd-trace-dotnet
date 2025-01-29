using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;

namespace Samples.AWS.S3
{
    static class AsyncHelpers
    {
        private const string BucketName = "MyBucket";
        private const string ObjectKey = "sample.txt";
        private const string ObjectContent = "Hello World!";

        public static async Task StartS3Tasks(AmazonS3Client s3Client)
        {
            Console.WriteLine("Beginning Async methods");
            using (var scope = SampleHelpers.CreateScope("async-methods"))
            {
                await CreateBucketAsync(s3Client, BucketName);

                // Allow time for the bucket to be ready
                Thread.Sleep(1000);

                await PutObjectAsync(s3Client, BucketName, ObjectKey);

                await DeleteBucketAsync(s3Client, BucketName);

                // Allow time for the bucket to be deleted
                Thread.Sleep(1000);
            }
        }

        private static async Task PutObjectAsync(AmazonS3Client s3Client, string bucketName, string objectKey)
        {
            var request = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = objectKey,
                ContentBody = ObjectContent
            };

            var response = await s3Client.PutObjectAsync(request);

            Console.WriteLine($"PutObjectAsync(PutObjectRequest) HTTP status code: {response.HttpStatusCode}");
        }

        private static async Task DeleteBucketAsync(AmazonS3Client s3Client, string bucketName)
        {
            var deleteBucketRequest = new DeleteBucketRequest
            {
                BucketName = bucketName
            };

            var response = await s3Client.DeleteBucketAsync(deleteBucketRequest);

            Console.WriteLine($"DeleteBucketAsync(DeleteBucketRequest) HTTP status code: {response.HttpStatusCode}");
        }

        private static async Task CreateBucketAsync(AmazonS3Client s3Client, string bucketName)
        {
            var createBucketRequest = new PutBucketRequest
            {
                BucketName = bucketName
            };

            var response = await s3Client.PutBucketAsync(createBucketRequest);

            Console.WriteLine($"CreateBucketAsync(PutBucketRequest) HTTP status code: {response.HttpStatusCode}");
        }
    }
}
