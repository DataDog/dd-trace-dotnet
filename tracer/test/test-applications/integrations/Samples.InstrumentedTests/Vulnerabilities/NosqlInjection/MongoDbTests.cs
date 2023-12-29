using System;
using System.Data;
using System.Threading;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Xunit;
using Xunit.Abstractions;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.NosqlInjection;

public class MongoDbTests : InstrumentationTestsBase, IDisposable
{
    private readonly ITestOutputHelper _testOutputHelper;
    private MongoClient _client;
    private IMongoDatabase _database;

    readonly string taintedString = "tainted";
    readonly string taintedString2 = "{ \"$ne\" : \"eee\" }";
    readonly string taintedString3 = "12";
    readonly string taintedString4 = "dbstats";
    readonly string taintedStringAttack = "nnn\"}}, { \"Author.Name\" : { \"$ne\" : \"notTainted2";

    private static string Host()
    {
        return Environment.GetEnvironmentVariable("MONGO_HOST") ?? "localhost";
    }

    public MongoDbTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        var connectionString = $"mongodb://{Host()}:27017";
        _client = new MongoClient(connectionString);
        _database = _client.GetDatabase("test-db");

        InitializeDatabase();
        
        // Add all tainted values
        AddTainted(taintedString);
        AddTainted(taintedString2);
        AddTainted(taintedString3);
        AddTainted(taintedString4);
        AddTainted(taintedStringAttack);
    }
    
    // Initialize the database with default values that will be used in the tests
    private void InitializeDatabase()
    {
        // Empty the database
        var collection = _database.GetCollection<BsonDocument>("Books");
        var allFilter = new BsonDocument();
        collection.DeleteMany(allFilter);
        
        // Insert the default values
        var newDocument = new BsonDocument
        {
            { "Author", new BsonDocument
                {
                    { "Name", "John" },
                    { "LastName", "Perkins" }
                }
            },
            { "BookName", "name" },
            { "Category", "Economy" },
            { "Price", 12 }
        };
        
        collection.InsertOne(newDocument);
    }
    

    public override void Dispose()
    {
        _client = null;
        base.Dispose();
    }

    // We exclude the tests that only pass when using a real MySql Connection
    // These tests have been left here for local testing purposes with MySql installed
    
    [Fact]
    public void GivenAMongoDb_JSON_JsonCommand_WhenRunCommandWithTainted_VulnerabilityReported()
    {
        TestDummyDDBBCall(
            () =>
            {
                var json = "{\"$or\":[{ \"Author.LastName\":{\"$eq\":\"notTainted2\"}},{\"Author.Name\":{\"$eq\":\""+ taintedStringAttack +"\"}}]}";
                var command = new JsonCommand<BsonDocument>(json);
                _database.RunCommand(command);
            });
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAMongoDb_JSON_JsonCommand_WhenRunCommandWithNotTainted_NotVulnerable()
    {
        TestDummyDDBBCall(
            () =>
            {
                const string json = "{ \"Author.Name\" : \"John\"}";
                var command = new JsonCommand<BsonDocument>(json);
                _database.RunCommand(command);
            });
        AssertNotVulnerable();
    }
    
    [Fact]
    public void GivenAMongoDb_JSON_JsonCommand_WhenRunCommandAsyncWithTainted_VulnerabilityReported()
    {
        TestDummyDDBBCall(
            () =>
            {
                var json = "{\"$or\":[{ \"Author.LastName\":{\"$eq\":\"notTainted2\"}},{\"Author.Name\":{\"$eq\":\""+ taintedStringAttack +"\"}}]}";
                var command = new JsonCommand<BsonDocument>(json);
                _database.RunCommandAsync(command);
            });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMongoDb_JSON_BsonParse_WhenRunCommandWithTainted_VulnerabilityReported()
    {
        TestDummyDDBBCall(
            () =>
            {
                var json = "{\"$or\":[{ \"Author.LastName\":{\"$eq\":\"notTainted2\"}},{\"Author.Name\":{\"$eq\":\""+ taintedStringAttack +"\"}}]}";
                var document = BsonDocument.Parse(json);
                var command = new BsonDocumentCommand<BsonDocument>(document);
                _database.RunCommand(command);
            });
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAMongoDb_JSON_BsonParse_WhenRunCommandWithNotTainted_NotVulnerable()
    {
        TestDummyDDBBCall(
            () =>
            {
                const string json = "{ \"Author.Name\" : \"John\"}";
                var document = BsonDocument.Parse(json);
                var command = new BsonDocumentCommand<BsonDocument>(document);
                _database.RunCommand(command);
            });
        AssertNotVulnerable();
    }
    
    [Fact]
    public void GivenAMongoDb_JSON_BsonParse_WhenRunCommandAsyncWithTainted_VulnerabilityReported()
    {
        TestDummyDDBBCall(
            () =>
            {
                var json = "{\"$or\":[{ \"Author.LastName\":{\"$eq\":\"notTainted2\"}},{\"Author.Name\":{\"$eq\":\""+ taintedStringAttack +"\"}}]}";
                var document = BsonDocument.Parse(json);
                var command = new BsonDocumentCommand<BsonDocument>(document);
                _database.RunCommandAsync(command);
            });
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAMongoDb_JSON_BsonParse_WhenFindWithTainted_VulnerabilityReported()
    {
        TestDummyDDBBCall(
            () =>
            {
                var json = "{ \"Price\" :\"" + taintedString3 + "\"   }";
                var document = BsonDocument.Parse(json);
                var collection = _database.GetCollection<BsonDocument>("Books");
                collection.Find(document).ToList();
            });
        AssertVulnerable();
    }
    
    /// TESTING ZONE
    
    [Fact]
    public void GivenAMongoDb_JSON_JsonReader_WhenFindWithTainted_VulnerabilityReported()
    {
        var json = "{ \"Price\" :\"" + taintedString3 + "\"   }";
        var reader = new JsonReader(json);
        IBsonSerializer<BsonDocument> serializer = BsonSerializer.LookupSerializer<BsonDocument>();
        BsonDeserializationContext context = BsonDeserializationContext.CreateRoot(reader);
        BsonDocument doc = serializer.Deserialize(context);
        var collection = _database.GetCollection<BsonDocument>("Books");
        var books = collection.Find(doc).ToList();
        AssertVulnerable();
    }
    
    /*
    [Fact]
    public void test()
    {
        TestDummyDDBBCall(
            () =>
            {
                var json = "{\"$or\":[{ \"Author.LastName\":{\"$eq\":\"notTainted2\"}},{\"Author.Name\":{\"$eq\":\""+ taintedStringAttack +"\"}}]}";
                var document = BsonDocument.Parse(json);
                var command = new BsonDocumentCommand<BsonDocument>(document);
                _database.RunCommandAs
            });
        AssertVulnerable();
    }*/
}
