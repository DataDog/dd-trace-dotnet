#if NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Amazon.S3;
using Amazon.S3.Model;

namespace Samples.AWS.S3
{
    static class SyncHelpers
    {
        private const string BucketName = "my-bucket";
        private const string ObjectKey = "sample.txt";
        private const string CopiedObjectKey = "copy.txt";
        private const string MultipartObjectKey = "multipart.txt";
        private const string ObjectContent = "Hello World!";
        private const int PartSize = 5 * 1024 * 1024; // 5MB for each part

        public static void StartS3Tasks(AmazonS3Client s3Client)
        {
            Console.WriteLine("Beginning Sync methods");
            using (var scope = SampleHelpers.CreateScope("sync-methods"))
            {
                CreateBucket(s3Client, BucketName);

                // Allow time for the bucket to be ready
                Thread.Sleep(1000);

                // Object management
                PutObject(s3Client, BucketName, ObjectKey);
                CopyObject(s3Client, BucketName, ObjectKey, CopiedObjectKey);
                ListObjects(s3Client, BucketName);
                Thread.Sleep(1000);
                DeleteObject(s3Client, BucketName, CopiedObjectKey);

                // Multipart uploads
                var uploadId = InitiateMultipartUpload(s3Client, BucketName, MultipartObjectKey);
                var part1Content = new string('a', PartSize);
                var uploadPart1Response = UploadPart(s3Client, BucketName, MultipartObjectKey, uploadId, 1, part1Content);
                
                var part2Content = new string('b', PartSize);
                var uploadPart2Response = UploadPart(s3Client, BucketName, MultipartObjectKey, uploadId, 2, part2Content);

                var etagParts = new List<PartETag>
                {
                    new PartETag(1, uploadPart1Response.ETag),
                    new PartETag(2, uploadPart2Response.ETag)
                };

                CompleteMultipartUpload(s3Client, BucketName, MultipartObjectKey, uploadId, etagParts);

                // Bucket management
                ListBuckets(s3Client);
                DeleteBucket(s3Client, BucketName);

                // Allow time for the bucket to be deleted
                Thread.Sleep(1000);
            }
        }

        // OBJECT MANAGEMENT
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

        private static void CopyObject(AmazonS3Client s3Client, string bucketName, string sourceKey, string destKey)
        {
            var request = new CopyObjectRequest
            {
                SourceBucket = bucketName,
                SourceKey = sourceKey,
                DestinationBucket = bucketName,
                DestinationKey = destKey
            };

            var response = s3Client.CopyObject(request);
            Console.WriteLine($"CopyObject(CopyObjectRequest) HTTP status code: {response.HttpStatusCode}");
        }

        private static void ListObjects(AmazonS3Client s3Client, string bucketName)
        {
            var request = new ListObjectsV2Request
            {
                BucketName = bucketName,
            };

            var response = s3Client.ListObjectsV2(request);
            Console.WriteLine($"ListObjectsV2(ListObjectsV2Request) HTTP status code: {response.HttpStatusCode}");
        }

        private static void DeleteObject(AmazonS3Client s3Client, string bucketName, string objectKey)
        {
            var request = new DeleteObjectRequest
            {
                BucketName = bucketName,
                Key = objectKey
            };

            var response = s3Client.DeleteObject(request);
            Console.WriteLine($"DeleteObject(DeleteObjectRequest) HTTP status code: {response.HttpStatusCode}");
        }

        // MULTIPART UPLOADS
        private static string InitiateMultipartUpload(AmazonS3Client s3Client, string bucketName, string objectKey)
        {
            var request = new InitiateMultipartUploadRequest
            {
                BucketName = bucketName,
                Key = objectKey
            };

            var response = s3Client.InitiateMultipartUpload(request);
            Console.WriteLine($"InitiateMultipartUpload(InitiateMultipartUploadRequest) HTTP status code: {response.HttpStatusCode}");
            return response.UploadId;
        }

        private static UploadPartResponse UploadPart(
            AmazonS3Client s3Client,
            string bucketName,
            string objectKey,
            string uploadId,
            int partNumber,
            string content
        ) {
            using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            var request = new UploadPartRequest
            {
                BucketName = bucketName,
                Key = objectKey,
                UploadId = uploadId,
                PartNumber = partNumber,
                InputStream = memoryStream
            };

            var response = s3Client.UploadPart(request);
            Console.WriteLine($"UploadPart(UploadPartRequest) HTTP status code: {response.HttpStatusCode}");
            return response;
        }

        private static void CompleteMultipartUpload(
            AmazonS3Client s3Client, 
            string bucketName, 
            string objectKey, 
            string uploadId,
            List<PartETag> partEtags
        ){
            var request = new CompleteMultipartUploadRequest
            {
                BucketName = bucketName,
                Key = objectKey,
                UploadId = uploadId,
                PartETags = partEtags
            };

            var response = s3Client.CompleteMultipartUpload(request);
            Console.WriteLine($"CompleteMultipartUpload(CompleteMultipartUploadRequest) HTTP status code: {response.HttpStatusCode}");
        }

        // BUCKET MANAGEMENT
        private static void CreateBucket(AmazonS3Client s3Client, string bucketName)
        {
            var createBucketRequest = new PutBucketRequest
            {
                BucketName = bucketName
            };

            var response = s3Client.PutBucket(createBucketRequest);
            Console.WriteLine($"CreateBucket(PutBucketRequest) HTTP status code: {response.HttpStatusCode}");
        }

        private static void ListBuckets(AmazonS3Client s3Client)
        {
            var response = s3Client.ListBuckets();
            Console.WriteLine($"ListBuckets() HTTP status code: {response.HttpStatusCode}");
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
    }
}

#endif
