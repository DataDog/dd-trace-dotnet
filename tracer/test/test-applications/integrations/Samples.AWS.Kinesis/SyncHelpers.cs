#if NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.Threading;
using Amazon.Kinesis;
using Amazon.Kinesis.Model;

namespace Samples.AWS.Kinesis
{
    static class SyncHelpers
    {

        private const string StreamName = "MyStreamName";

        public static void StartKinesisTasks(AmazonKinesisClient kinesisClient)
        {
            Console.WriteLine("Beginning Synchronous methods");
            using (var scope = SampleHelpers.CreateScope("sync-methods"))
            {
                CreateStream(kinesisClient);
                
                // Needed in order to allow Kinesis Stream to be in
                // Ready status.
                Thread.Sleep(1000);
                PutRecord(kinesisClient);
                PutRecords(kinesisClient);
#if KINESIS_3_7_0
                if (Common.UseStreamArn)
                {
                    var streamArn = GetStreamArn(kinesisClient);
                    PutRecordByStreamArn(kinesisClient, streamArn);
                    PutRecordsByStreamArn(kinesisClient, streamArn);
                }
#endif

                DeleteStream(kinesisClient);

                // Needed in order to allow Kineses Stream to be deleted
                Thread.Sleep(1000);
            }
        }

        public static void CreateStream(AmazonKinesisClient kinesisClient)
        {
            var createStreamRequest = new CreateStreamRequest { StreamName = StreamName };

            var response = kinesisClient.CreateStream(createStreamRequest);
            Console.WriteLine($"CreateStream(CreateStreamRequest) HTTP status code: {response.HttpStatusCode}");
        }

        public static void DeleteStream(AmazonKinesisClient kinesisClient)
        {
            var deleteStreamRequest = new DeleteStreamRequest { StreamName = StreamName };

            var response = kinesisClient.DeleteStream(deleteStreamRequest);
            Console.WriteLine($"DeleteStream(DeleteStreamRequest) HTTP status code: {response.HttpStatusCode}");
        }

        public static void PutRecord(AmazonKinesisClient kinesisClient)
        {
            var putRecordRequest = new PutRecordRequest
            {
                StreamName = StreamName,
                PartitionKey = Guid.NewGuid().ToString(),
            };

            var data = new Dictionary<string, object> { { "name", "Jordan" }, { "lastname", "González Bustamante" }, { "age", 24 } };
            putRecordRequest.Data = Common.DictionaryToMemoryStream(data);

            var response = kinesisClient.PutRecord(putRecordRequest);
            Console.WriteLine($"PutRecord(PutRecordRequest) HTTP status code: {response.HttpStatusCode}");
        }

        public static void PutRecords(AmazonKinesisClient kinesisClient)
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

            var response = kinesisClient.PutRecords(putRecordsRequest);
            Console.WriteLine($"PutRecords(PutRecordsRequest) HTTP status code: {response.HttpStatusCode}");
        }

#if KINESIS_3_7_0
        // A stream can be named by its ARN, which the span tags as aws.stream.arn
        public static string GetStreamArn(AmazonKinesisClient kinesisClient)
        {
            var request = new DescribeStreamSummaryRequest { StreamName = StreamName };
            var response = kinesisClient.DescribeStreamSummary(request);
            return response.StreamDescriptionSummary.StreamARN;
        }

        public static void PutRecordByStreamArn(AmazonKinesisClient kinesisClient, string streamArn)
        {
            var putRecordRequest = new PutRecordRequest
            {
                StreamARN = streamArn,
                PartitionKey = Guid.NewGuid().ToString(),
                Data = Common.DictionaryToMemoryStream(new Dictionary<string, object> { { "name", "Jordan" } }),
            };

            var response = kinesisClient.PutRecord(putRecordRequest);
            Console.WriteLine($"PutRecord(PutRecordRequest) by stream ARN HTTP status code: {response.HttpStatusCode}");
        }

        public static void PutRecordsByStreamArn(AmazonKinesisClient kinesisClient, string streamArn)
        {
            var putRecordsRequest = new PutRecordsRequest
            {
                StreamARN = streamArn,
                Records = new List<PutRecordsRequestEntry>
                {
                    new PutRecordsRequestEntry
                    {
                        Data = Common.DictionaryToMemoryStream(new Dictionary<string, object> { { "name", "Piplup" } }),
                        PartitionKey = Guid.NewGuid().ToString()
                    }
                }
            };

            var response = kinesisClient.PutRecords(putRecordsRequest);
            Console.WriteLine($"PutRecords(PutRecordsRequest) by stream ARN HTTP status code: {response.HttpStatusCode}");
        }
#endif
    }
}
#endif
