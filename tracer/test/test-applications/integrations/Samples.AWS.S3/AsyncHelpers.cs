using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;

namespace Samples.AWS.S3
{
    static class AsyncHelpers
    {
        private const string BucketName = "my-bucket";
        private const string ObjectKey = "sample.txt";
        private const string CopiedObjectKey = "copy.txt";
        private const string MultipartObjectKey = "multipart.txt";
        private const string ObjectContent = "Hello World!";
        private const int PartSize = 5 * 1024 * 1024; // 5MB for each part

        public static async Task StartS3Tasks(AmazonS3Client s3Client)
        {
            Console.WriteLine("Beginning Async methods");
            using (var scope = SampleHelpers.CreateScope("async-methods"))
            {
                await CreateBucketAsync(s3Client, BucketName);

                // Allow time for the bucket to be ready
                Thread.Sleep(1000);

                // Object management
                await PutObjectAsync(s3Client, BucketName, ObjectKey);
                await GetObjectAsync(s3Client, BucketName, ObjectKey);
                await CopyObjectAsync(s3Client, BucketName, ObjectKey, CopiedObjectKey);
                await ListObjectsAsync(s3Client, BucketName);
                Thread.Sleep(1000);
                await DeleteObjectAsync(s3Client, BucketName, CopiedObjectKey);
                await DeleteObjectsAsync(s3Client, BucketName, [ObjectKey]);

                // Multipart uploads
                var uploadId = await InitiateMultipartUpload(s3Client, BucketName, MultipartObjectKey);
                var part1Content = new string('a', PartSize);
                var uploadPart1Response = await UploadPart(s3Client, BucketName, MultipartObjectKey, uploadId, 1, part1Content);

                var part2Content = new string('b', PartSize);
                var uploadPart2Response = await UploadPart(s3Client, BucketName, MultipartObjectKey, uploadId, 2, part2Content);
                List<PartETag> etagParts = [
                    new(1, uploadPart1Response.ETag),
                    new(2, uploadPart2Response.ETag)
                ];
                await CompleteMultipartUpload(s3Client, BucketName, MultipartObjectKey, uploadId, etagParts);

                // Bucket management
                await ListBucketsAsync(s3Client);
                await DeleteBucketAsync(s3Client, BucketName);

                // Allow time for the bucket to be deleted
                Thread.Sleep(1000);
            }
        }

        public static async Task StartS3DuckTypingErrorTasks(AmazonS3Client s3Client)
        {
            Console.WriteLine("Beginning Async methods with duck typing error");
            using (var scope = SampleHelpers.CreateScope("async-methods-duck-typing-error"))
            {
                // Each operation will fail due to invalid endpoint, but we should instrument still
                // previously we would throw null refs
                await TryS3Operation("CreateBucket", () => CreateBucketAsync(s3Client, BucketName));
                await TryS3Operation("PutObject", () => PutObjectAsync(s3Client, BucketName, ObjectKey));
                await TryS3Operation("GetObject", () => GetObjectAsync(s3Client, BucketName, ObjectKey));
                await TryS3Operation("CopyObject", () => CopyObjectAsync(s3Client, BucketName, ObjectKey, CopiedObjectKey));
                await TryS3Operation("ListObjects", () => ListObjectsAsync(s3Client, BucketName));
                await TryS3Operation("DeleteObject", () => DeleteObjectAsync(s3Client, BucketName, CopiedObjectKey));
                await TryS3Operation("DeleteObjects", () => DeleteObjectsAsync(s3Client, BucketName, [ObjectKey]));
                await TryS3Operation("ListBuckets", () => ListBucketsAsync(s3Client));
                await TryS3Operation("DeleteBucket", () => DeleteBucketAsync(s3Client, BucketName));

            }
        }

        private static async Task TryS3Operation(string operationName, Func<Task> operation)
        {
            try
            {
                await operation();
            }
            catch (Exception ex) when (ex.Message.Contains("No such host is known"))
            {
                Console.WriteLine($"{operationName}: Expected connection failure");
            }
            catch (NullReferenceException ex) when (ex.ToString().Contains("get_ETag"))
            {
                Console.WriteLine($"*** DUCK TYPING ERROR IN {operationName.ToUpper()}! ***");
                Console.WriteLine("This should be fixed by checking returnValue?.Instance is not null");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{operationName}: Unexpected exception - {ex.GetType().Name}: {ex.Message}");
            }
        }

        // OBJECT MANAGEMENT
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
        
        private static async Task CopyObjectAsync(AmazonS3Client s3Client, string bucketName, string sourceKey, string destKey)
        {
            var request = new CopyObjectRequest
            {
                SourceBucket = bucketName,
                DestinationBucket = bucketName,
                SourceKey = sourceKey,
                DestinationKey = destKey
            };

            var response = await s3Client.CopyObjectAsync(request);
            Console.WriteLine($"CopyObjectAsync(CopyObjectRequest) HTTP status code: {response.HttpStatusCode}");
        }
        
        private static async Task ListObjectsAsync(AmazonS3Client s3Client, string bucketName)
        {
            var request = new ListObjectsV2Request
            {
                BucketName = bucketName,
            };

            var response = await s3Client.ListObjectsV2Async(request);
            Console.WriteLine($"ListObjectsV2Async(ListObjectsV2Request) HTTP status code: {response.HttpStatusCode}");
        }
        
        private static async Task DeleteObjectsAsync(AmazonS3Client s3Client, string bucketName, List<string> objectKeys)
        {
            var deleteObjects = objectKeys.Select(key => new KeyVersion { Key = key }).ToList();
            
            var request = new DeleteObjectsRequest
            {
                BucketName = bucketName,
                Objects = deleteObjects
            };
        
            var response = await s3Client.DeleteObjectsAsync(request);
            Console.WriteLine($"DeleteObjectsAsync(DeleteObjectsRequest) HTTP status code: {response.HttpStatusCode}");
        }

        private static async Task DeleteObjectAsync(AmazonS3Client s3Client, string bucketName, string objectKey)
        {
            var request = new DeleteObjectRequest
            {
                BucketName = bucketName,
                Key = objectKey
            };

            var response = await s3Client.DeleteObjectAsync(request);
            Console.WriteLine($"DeleteObjectAsync(DeleteObjectRequest) HTTP status code: {response.HttpStatusCode}");
        }

        private static async Task GetObjectAsync(AmazonS3Client s3Client, string bucketName, string objectKey)
        {
            var request = new GetObjectRequest
            {
                BucketName = bucketName,
                Key = objectKey
            };
        
            var response = await s3Client.GetObjectAsync(request);
            Console.WriteLine($"GetObjectAsync(GetObjectRequest) HTTP status code: {response.HttpStatusCode}");
        }

        // MULTIPART UPLOADS
        private static async Task<string> InitiateMultipartUpload(AmazonS3Client s3Client, string bucketName, string objectKey)
        {
            var initRequest = new InitiateMultipartUploadRequest
            {
                BucketName = bucketName,
                Key = objectKey
            };

            var initResponse = await s3Client.InitiateMultipartUploadAsync(initRequest);
            Console.WriteLine($"InitiateMultipartUploadAsync(InitiateMultipartUploadRequest) HTTP status code: {initResponse.HttpStatusCode}");
            return initResponse.UploadId;
        }

        private static async Task<UploadPartResponse> UploadPart(
            AmazonS3Client s3Client,
            string bucketName,
            string objectKey,
            string uploadId,
            int partNumber,
            string content
        ) {
            using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            var uploadRequest = new UploadPartRequest
            {
                BucketName = bucketName,
                Key = objectKey,
                UploadId = uploadId,
                PartNumber = partNumber,
                InputStream = memoryStream,
            };

            var response = await s3Client.UploadPartAsync(uploadRequest);
            Console.WriteLine($"UploadPartAsync(UploadPartRequest) HTTP status code: {response.HttpStatusCode}");
            return response;
        }

        private static async Task CompleteMultipartUpload(
            AmazonS3Client s3Client, 
            string bucketName, 
            string objectKey, 
            string uploadId,
            List<PartETag> partEtags
        ){
            var completionRequest = new CompleteMultipartUploadRequest
            {
                BucketName = bucketName,
                Key = objectKey,
                UploadId = uploadId,
                PartETags = partEtags
            };
            
            var completeUploadResponse = await s3Client.CompleteMultipartUploadAsync(completionRequest);
            Console.WriteLine($"CompleteMultipartUploadAsync(CompleteMultipartUploadRequest) HTTP status code: {completeUploadResponse.HttpStatusCode}");
        }

        // BUCKET MANAGEMENT
        private static async Task CreateBucketAsync(AmazonS3Client s3Client, string bucketName)
        {
            var createBucketRequest = new PutBucketRequest
            {
                BucketName = bucketName
            };

            var response = await s3Client.PutBucketAsync(createBucketRequest);
            Console.WriteLine($"CreateBucketAsync(PutBucketRequest) HTTP status code: {response.HttpStatusCode}");
        }

        private static async Task ListBucketsAsync(AmazonS3Client s3Client)
        {
            var response = await s3Client.ListBucketsAsync();
            Console.WriteLine($"ListBucketsAsync(ListBucketsRequest) HTTP status code: {response.HttpStatusCode}");
        }

        private static async Task DeleteBucketAsync(AmazonS3Client s3Client, string bucketName)
        {
            try 
            {
                // First, delete all objects in the bucket
                var listRequest = new ListObjectsV2Request
                {
                    BucketName = bucketName
                };
        
                var listResponse = await s3Client.ListObjectsV2Async(listRequest);
                foreach (var obj in listResponse.S3Objects)
                {
                    await s3Client.DeleteObjectAsync(bucketName, obj.Key);
                    Console.WriteLine($"Deleted object {obj.Key} from bucket {bucketName}");
                }

                // Now delete the empty bucket
                var deleteBucketRequest = new DeleteBucketRequest
                {
                    BucketName = bucketName
                };

                var response = await s3Client.DeleteBucketAsync(deleteBucketRequest);
                Console.WriteLine($"DeleteBucketAsync(DeleteBucketRequest) HTTP status code: {response.HttpStatusCode}");
            }
            catch (AmazonS3Exception ex)
            {
                Console.WriteLine($"Error in DeleteBucketAsync: {ex.Message}");
                throw;
            }
        }
    }
}
