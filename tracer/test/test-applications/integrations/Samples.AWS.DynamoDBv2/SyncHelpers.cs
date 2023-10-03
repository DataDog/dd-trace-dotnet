#if NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.Threading;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace Samples.AWS.DynamoDBv2
{
    static class SyncHelpers
    {

        private const string TableName = "MyTableName";

        public static void StartDynamoDBTasks(AmazonDynamoDBClient dynamoDBClient)
        {
            Console.WriteLine("Beginning Sync methods");
            using (var scope = SampleHelpers.CreateScope("sync-methods"))
            {
                CreateTable(dynamoDBClient);

                // Needed in order to allow DynamoDB Table to be in
                // Ready status.
                Thread.Sleep(1000);
                PutItem(dynamoDBClient);
                GetItem(dynamoDBClient);
                UpdateItem(dynamoDBClient);
                DeleteItem(dynamoDBClient);
                
                BatchWriteItem(dynamoDBClient);
                BatchGetItem(dynamoDBClient);
                DeleteItems(dynamoDBClient);

                DeleteTable(dynamoDBClient);

                // Needed in order to allow DynamoDB Table to be deleted
                Thread.Sleep(1000);
            }
        }

        public static void CreateTable(AmazonDynamoDBClient dynamoDBClient)
        {
            var schema = new List<KeySchemaElement>
            {
                new() { AttributeName = "id", KeyType = "HASH" },
                new() { AttributeName = "name", KeyType = "RANGE" }
            };
            var definitions = new List<AttributeDefinition>
            {
                new() { AttributeName = "id", AttributeType = "S" },
                new() { AttributeName = "name", AttributeType = "S" }
            };
            var throughput = new ProvisionedThroughput
            {
                ReadCapacityUnits = 20,
                WriteCapacityUnits = 50
            };
            var createTableRequest = new CreateTableRequest
            {
                TableName = TableName,
                KeySchema = schema,
                ProvisionedThroughput = throughput,
                AttributeDefinitions = definitions
            };

            var response = dynamoDBClient.CreateTable(createTableRequest);
            Console.WriteLine($"CreateTable(CreateTableRequest) HTTP status code: {response.HttpStatusCode}");
        }

        public static void DeleteTable(AmazonDynamoDBClient dynamoDBClient)
        {
            var deleteStreamRequest = new DeleteTableRequest { TableName = TableName };

            var response = dynamoDBClient.DeleteTable(deleteStreamRequest);
            Console.WriteLine($"DeleteTable(DeleteTableRequest) HTTP status code: {response.HttpStatusCode}");
        }

        public static void PutItem(AmazonDynamoDBClient dynamoDBClient)
        {
            var item = new Dictionary<string, AttributeValue>
            {
                ["id"] = new() { S = Guid.NewGuid().ToString() },
                ["name"] = new() { S = "Jordan" },
                ["lastname"] = new() { S = "González Bustamante" },
                ["age"] = new() { N = "24" }
            };

            var putItemRequest = new PutItemRequest
            {
                TableName = TableName,
                Item = item,
            };

            var response = dynamoDBClient.PutItem(putItemRequest);
            Console.WriteLine($"PutItem(PutItemRequest) HTTP status code: {response.HttpStatusCode}");
        }

        public static void BatchWriteItem(AmazonDynamoDBClient dynamoDBClient)
        {
            var person = new Dictionary<string, AttributeValue>
            {
                ["id"] = new() { S = "1" },
                ["name"] = new() { S = "Jordan" },
                ["lastname"] = new() { S = "González Bustamante" },
                ["city"] = new() { S = "NYC" }
            };
            var pokemon = new Dictionary<string, AttributeValue>
            {
                ["id"] = new() { S = "2" },
                ["pid"] = new() { N = "393" },
                ["name"] = new() { S = "Piplup" },
                ["type"] = new() { S = "water" }
            };
            var writeRequestList = new List<WriteRequest>
            {
                new() { PutRequest = new() { Item = person }, }, 
                new() { PutRequest = new() { Item = pokemon } }
            };
            var batchWriteItemRequest = new BatchWriteItemRequest
            {
                RequestItems =
                {
                    { TableName, writeRequestList }
                }
            };

            var response = dynamoDBClient.BatchWriteItem(batchWriteItemRequest);
            Console.WriteLine($"BatchWriteItem(BatchWriteItemRequest) HTTP status code: {response.HttpStatusCode}");
        }

        public static void GetItem(AmazonDynamoDBClient dynamoDBClient)
        {
            var key = new Dictionary<string, AttributeValue>
            {
                ["id"] = new() { S = "1" },
                ["name"] = new() { S = "Jordan" }
            };
            
            var getItemRequest = new GetItemRequest
            {
                TableName = TableName,
                Key = key
            };
            
            var response = dynamoDBClient.GetItem(getItemRequest);
            Console.WriteLine($"GetItem(GetItemRequest) HTTP status code: {response.HttpStatusCode}");
        }
        
        public static void BatchGetItem(AmazonDynamoDBClient dynamoDBClient)
        {
            var person = new Dictionary<string, AttributeValue>
            {
                ["id"] = new() { S = "1" },
                ["name"] = new() { S = "Jordan" }
            };
            var pokemon = new Dictionary<string, AttributeValue>
            {
                ["id"] = new() { S = "2" },
                ["name"] = new() { S = "Piplup" }
            };
            var keysAndAttributes = new KeysAndAttributes
            {
                Keys = new List<Dictionary<string, AttributeValue>>
                {
                    person,
                    pokemon
                }
            };
            var batchGetItemRequest = new BatchGetItemRequest
            {
                RequestItems =
                {
                    { TableName, keysAndAttributes }
                }
            };

            var response = dynamoDBClient.BatchGetItem(batchGetItemRequest);
            Console.WriteLine($"BatchGetItem(BatchGetItemRequest) HTTP status code: {response.HttpStatusCode}");
        }

        public static void DeleteItem(AmazonDynamoDBClient dynamoDBClient)
        {
            var key = new Dictionary<string, AttributeValue>
            {
                ["id"] = new() { S = "1" },
                ["name"] = new() { S = "Jordan" }
            };
            
            var deleteItemRequest = new DeleteItemRequest
            {
                TableName = TableName,
                Key = key
            };
            
            var response = dynamoDBClient.DeleteItem(deleteItemRequest);
            Console.WriteLine($"DeleteItem(DeleteItemRequest) HTTP status code: {response.HttpStatusCode}");
        }
        
        public static void DeleteItems(AmazonDynamoDBClient dynamoDBClient)
        {
            var person = new Dictionary<string, AttributeValue>
            {
                ["id"] = new() { S = "1" },
                ["name"] = new() { S = "Jordan" }
            };
            var pokemon = new Dictionary<string, AttributeValue>
            {
                ["id"] = new() { S = "2" },
                ["name"] = new() { S = "Piplup" },
            };
            var writeRequestList = new List<WriteRequest>
            {
                new() { DeleteRequest = new() { Key = person }, }, 
                new() { DeleteRequest = new() { Key = pokemon } }
            };
            
            var batchWriteItemRequest = new BatchWriteItemRequest
            {
                RequestItems =
                {
                    { TableName, writeRequestList }
                }
            };

            var response = dynamoDBClient.BatchWriteItem(batchWriteItemRequest);
            Console.WriteLine($"BatchWriteItem(BatchWriteItemRequest) HTTP status code: {response.HttpStatusCode}");
        }
        
        public static void UpdateItem(AmazonDynamoDBClient dynamoDBClient)
        {
            var key = new Dictionary<string, AttributeValue>
            {
                ["id"] = new() { S = "1" },
                ["name"] = new() { S = "Jordan" }
            };

            var updates = new Dictionary<string, AttributeValueUpdate>
            {
                ["lastname"] = new()
                {
                    Action = AttributeAction.PUT, 
                    Value = new () { S = "Gonzalez" }
                }
            };
            
            var updateItemRequest = new UpdateItemRequest
            {
                TableName = TableName,
                Key = key,
                AttributeUpdates = updates
            };
            
            var response = dynamoDBClient.UpdateItem(updateItemRequest);
            Console.WriteLine($"UpdateItem(UpdateItemRequest) HTTP status code: {response.HttpStatusCode}");
        }
    }
}
#endif
