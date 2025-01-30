#if NETFRAMEWORK

using System;
using System.IO;
using System.Threading;
using Amazon.S3;
using Amazon.S3.Model;

namespace Samples.AWS.S3
{
    static class SyncHelpers
    {
        private const string BucketName = "my-bucket";
        private const string ObjectKey = "sample.txt";
        private const string ObjectContent = "Hello World!";

        public static void StartS3Tasks(AmazonS3Client s3Client)
        {
            Console.WriteLine("Beginning Sync methods");
            using (var scope = SampleHelpers.CreateScope("sync-methods"))
            {
                CreateBucket(s3Client, BucketName);

                // Allow time for the bucket to be ready
                Thread.Sleep(1000);

                PutObject(s3Client, BucketName, ObjectKey);

                DeleteBucket(s3Client, BucketName);

                // Allow time for the bucket to be deleted
                Thread.Sleep(1000);
            }
        }

        private static void PutObject(AmazonS3Client s3Client, string bucketName, string objectKey)
        {
            var request = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = objectKey,
                ContentBody = ObjectContent
            };

            var response = s3Client.PutObject(request);

            Console.WriteLine($"PutObject(PutObjectRequest) HTTP status code: {response.HttpStatusCode}");
        }

        private static void DeleteBucket(AmazonS3Client s3Client, string bucketName)
        {
            try 
            {
                // First, delete all objects in the bucket
                var listRequest = new ListObjectsV2Request
                {
                    BucketName = bucketName
                };
                
                var listResponse = s3Client.ListObjectsV2(listRequest);
                foreach (var obj in listResponse.S3Objects)
                {
                    s3Client.DeleteObject(bucketName, obj.Key);
                    Console.WriteLine($"Deleted object {obj.Key} from bucket {bucketName}");
                }

                // Now delete the empty bucket
                var deleteBucketRequest = new DeleteBucketRequest
                {
                    BucketName = bucketName
                };

                var response = s3Client.DeleteBucket(deleteBucketRequest);
                Console.WriteLine($"DeleteBucket(DeleteBucketRequest) HTTP status code: {response.HttpStatusCode}");
            }
            catch (AmazonS3Exception ex)
            {
                Console.WriteLine($"Error in DeleteBucket: {ex.Message}");
                throw;
            }
        }

        private static void CreateBucket(AmazonS3Client s3Client, string bucketName)
        {
            var createBucketRequest = new PutBucketRequest
            {
                BucketName = bucketName
            };

            var response = s3Client.PutBucket(createBucketRequest);

            Console.WriteLine($"CreateBucket(PutBucketRequest) HTTP status code: {response.HttpStatusCode}");
        }
    }
}

#endif
