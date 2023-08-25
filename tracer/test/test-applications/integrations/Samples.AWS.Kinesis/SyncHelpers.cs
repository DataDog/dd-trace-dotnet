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
    }
}
#endif
