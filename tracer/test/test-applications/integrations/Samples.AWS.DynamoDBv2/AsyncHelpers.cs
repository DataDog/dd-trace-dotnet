using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace Samples.AWS.DynamoDBv2
{
    static class AsyncHelpers
    {

        private const string TableName = "MyTableName";

        public static async Task StartDynamoDBTasks(AmazonDynamoDBClient dynamoDBClient)
        {
            Console.WriteLine("Beginning Async methods");
            using (var scope = SampleHelpers.CreateScope("async-methods"))
            {
                await CreateTableAsync(dynamoDBClient);

                // Needed in order to allow DynamoDB Table to be in
                // Ready status.
                Thread.Sleep(1000);
                await PutItemAsync(dynamoDBClient);
                await GetItemAsync(dynamoDBClient);
                await UpdateItemAsync(dynamoDBClient);
                await DeleteItemAsync(dynamoDBClient);
                
                await BatchWriteItemAsync(dynamoDBClient);
                await BatchGetItemAsync(dynamoDBClient);
                await DeleteItemsAsync(dynamoDBClient);

                await DeleteTableAsync(dynamoDBClient);

                // Needed in order to allow DynamoDB Table to be deleted
                Thread.Sleep(1000);
            }
        }

        public static async Task CreateTableAsync(AmazonDynamoDBClient dynamoDBClient)
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

            var response = await dynamoDBClient.CreateTableAsync(createTableRequest);
            Console.WriteLine($"CreateTableAsync(CreateTableRequest) HTTP status code: {response.HttpStatusCode}");
        }

        public static async Task DeleteTableAsync(AmazonDynamoDBClient dynamoDBClient)
        {
            var deleteStreamRequest = new DeleteTableRequest { TableName = TableName };

            var response = await dynamoDBClient.DeleteTableAsync(deleteStreamRequest);
            Console.WriteLine($"DeleteTableAsync(DeleteTableRequest) HTTP status code: {response.HttpStatusCode}");
        }

        public static async Task PutItemAsync(AmazonDynamoDBClient dynamoDBClient)
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

            var response = await dynamoDBClient.PutItemAsync(putItemRequest);
            Console.WriteLine($"PutItemAsync(PutItemRequest) HTTP status code: {response.HttpStatusCode}");
        }

        public static async Task BatchWriteItemAsync(AmazonDynamoDBClient dynamoDBClient)
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

            var response = await dynamoDBClient.BatchWriteItemAsync(batchWriteItemRequest);
            Console.WriteLine($"BatchWriteItemAsync(BatchWriteItemRequest) HTTP status code: {response.HttpStatusCode}");
        }

        public static async Task GetItemAsync(AmazonDynamoDBClient dynamoDBClient)
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
            
            var response = await dynamoDBClient.GetItemAsync(getItemRequest);
            Console.WriteLine($"GetItemAsync(GetItemRequest) HTTP status code: {response.HttpStatusCode}");
        }
        
        public static async Task BatchGetItemAsync(AmazonDynamoDBClient dynamoDBClient)
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

            var response = await dynamoDBClient.BatchGetItemAsync(batchGetItemRequest);
            Console.WriteLine($"BatchGetItemAsync(BatchGetItemRequest) HTTP status code: {response.HttpStatusCode}");
        }

        public static async Task DeleteItemAsync(AmazonDynamoDBClient dynamoDBClient)
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
            
            var response = await dynamoDBClient.DeleteItemAsync(deleteItemRequest);
            Console.WriteLine($"DeleteItemAsync(DeleteItemRequest) HTTP status code: {response.HttpStatusCode}");
        }
        
        public static async Task DeleteItemsAsync(AmazonDynamoDBClient dynamoDBClient)
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

            var response = await dynamoDBClient.BatchWriteItemAsync(batchWriteItemRequest);
            Console.WriteLine($"BatchWriteItemAsync(BatchWriteItemRequest) HTTP status code: {response.HttpStatusCode}");
        }
        
        public static async Task UpdateItemAsync(AmazonDynamoDBClient dynamoDBClient)
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
            
            var response = await dynamoDBClient.UpdateItemAsync(updateItemRequest);
            Console.WriteLine($"UpdateItemAsync(UpdateItemRequest) HTTP status code: {response.HttpStatusCode}");
        }
    }
}
