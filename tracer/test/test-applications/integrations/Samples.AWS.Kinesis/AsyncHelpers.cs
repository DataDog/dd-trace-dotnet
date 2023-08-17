using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Kinesis;
using Amazon.Kinesis.Model;
using Newtonsoft.Json;

namespace Samples.AWS.Kinesis
{
    static class AsyncHelpers
    {

        private const string StreamName = "MyStreamName";

        public static async Task StartKinesisTasks(AmazonKinesisClient kinesisClient)
        {
            Console.WriteLine("Beginning Async methods");
            using (var scope = SampleHelpers.CreateScope("async-methods"))
            {
                await CreateStreamAsync(kinesisClient);
                
                // Needed in order to allow Kinesis Stream to be in
                // Ready status.
                Thread.Sleep(1000);
                await PutRecordAsync(kinesisClient);

                await DeleteStreamAsync(kinesisClient);
            }
        }

        public static async Task CreateStreamAsync(AmazonKinesisClient kinesisClient)
        {
            var createStreamRequest = new CreateStreamRequest { StreamName = StreamName };

            var response = await kinesisClient.CreateStreamAsync(createStreamRequest);
            Console.WriteLine($"CreateStreamAsync(CreateStreamRequest) HTTP status code: {response.HttpStatusCode}");
        }
        
        public static async Task DeleteStreamAsync(AmazonKinesisClient kinesisClient)
        {
            var deleteStreamRequest = new DeleteStreamRequest { StreamName = StreamName };

            var response = await kinesisClient.DeleteStreamAsync(deleteStreamRequest);
            Console.WriteLine($"DeleteStreamAsync(DeleteStreamRequest) HTTP status code: {response.HttpStatusCode}");
        }

        public static async Task PutRecordAsync(AmazonKinesisClient kinesisClient)
        {
            var putRecordRequest = new PutRecordRequest
            {
                StreamName = StreamName,
                PartitionKey = Guid.NewGuid().ToString(),
            };

            var data = new Dictionary<string, object> { { "name", "Jordan" }, { "lastname", "González Bustamante" }, { "age", 24 } };
            var jsonString = JsonConvert.SerializeObject(data);
            var bytes = Encoding.UTF8.GetBytes(jsonString);
            putRecordRequest.Data = new MemoryStream(bytes);

            var response = await kinesisClient.PutRecordAsync(putRecordRequest);
            Console.WriteLine($"PutRecordAsync(PutRecordRequest) HTTP status code: {response.HttpStatusCode}");
        }
    }
}
