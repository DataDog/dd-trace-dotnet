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
                await PutRecordsAsync(kinesisClient);
#if KINESIS_3_7_0
                if (Common.UseStreamArn)
                {
                    await PutRecordByStreamArnAsync(kinesisClient);
                    await PutRecordsByStreamArnAsync(kinesisClient);
                }
#endif

                await DeleteStreamAsync(kinesisClient);

                // Needed in order to allow Kineses Stream to be deleted
                Thread.Sleep(1000);
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
            putRecordRequest.Data = Common.DictionaryToMemoryStream(data);

            var response = await kinesisClient.PutRecordAsync(putRecordRequest);
            Console.WriteLine($"PutRecordAsync(PutRecordRequest) HTTP status code: {response.HttpStatusCode}");
        }

        public static async Task PutRecordsAsync(AmazonKinesisClient kinesisClient)
        {
            var person = new Dictionary<string, object> { { "name", "Jordan" }, { "lastname", "Gonzalez" }, { "city", "NYC" } };
            var pokemon = new Dictionary<string, object> { { "id", "393" }, { "name", "Piplup" }, { "type", "water" } };
            var putRecordsRequest = new PutRecordsRequest
            {
                StreamName = StreamName,
                Records = new List<PutRecordsRequestEntry>
                {
                    new PutRecordsRequestEntry
                    {
                        Data = Common.DictionaryToMemoryStream(person),
                        PartitionKey = Guid.NewGuid().ToString()
                    }, 
                    new PutRecordsRequestEntry
                    {
                        Data = Common.DictionaryToMemoryStream(pokemon),
                        PartitionKey = Guid.NewGuid().ToString()
                    }
                }
            };

            var response = await kinesisClient.PutRecordsAsync(putRecordsRequest);
            Console.WriteLine($"PutRecordsAsync(PutRecordsRequest) HTTP status code: {response.HttpStatusCode}");
        }

#if KINESIS_3_7_0
        // A stream can be named by its ARN, which the span tags as aws.stream.arn
        public static async Task PutRecordByStreamArnAsync(AmazonKinesisClient kinesisClient)
        {
            var putRecordRequest = new PutRecordRequest
            {
                StreamARN = Common.StreamArn,
                PartitionKey = Guid.NewGuid().ToString(),
                Data = Common.DictionaryToMemoryStream(new Dictionary<string, object> { { "name", "Jordan" } }),
            };

            var response = await kinesisClient.PutRecordAsync(putRecordRequest);
            Console.WriteLine($"PutRecordAsync(PutRecordRequest) by stream ARN HTTP status code: {response.HttpStatusCode}");
        }

        public static async Task PutRecordsByStreamArnAsync(AmazonKinesisClient kinesisClient)
        {
            var putRecordsRequest = new PutRecordsRequest
            {
                StreamARN = Common.StreamArn,
                Records = new List<PutRecordsRequestEntry>
                {
                    new PutRecordsRequestEntry
                    {
                        Data = Common.DictionaryToMemoryStream(new Dictionary<string, object> { { "name", "Piplup" } }),
                        PartitionKey = Guid.NewGuid().ToString()
                    }
                }
            };

            var response = await kinesisClient.PutRecordsAsync(putRecordsRequest);
            Console.WriteLine($"PutRecordsAsync(PutRecordsRequest) by stream ARN HTTP status code: {response.HttpStatusCode}");
        }
#endif
    }
}
