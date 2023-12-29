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

[Trait("RequiresDockerDependency", "true")]
[Trait("Category", "EndToEnd")]
public class MongoDbTests : InstrumentationTestsBase, IDisposable
{
    private readonly ITestOutputHelper _testOutputHelper;
    private MongoClient _client;
    private IMongoDatabase _database;

    readonly string taintedString12 = "12";
    readonly string taintedStringCommand = "dbstats";
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
        AddTainted(taintedString12);
        AddTainted(taintedStringCommand);
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
            { "Price", "12" }
        };
        
        collection.InsertOne(newDocument);
    }
    

    public override void Dispose()
    {
        _client = null;
        base.Dispose();
    }

    // We exclude the tests that only pass when using a real MongoDB Connection
    // These tests have been left here for local testing purposes with MongoDB installed
    
    [Fact]
    public void GivenAMongoDb_JSON_JsonCommand_WhenRunCommandWithTainted_VulnerabilityReported()
    {
        var json = "{ \"" + taintedStringCommand + "\" : 1 }";
        var command = new JsonCommand<BsonDocument>(json);
        var result = _database.RunCommand(command);
        
        Assert.NotNull(result);
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAMongoDb_JSON_JsonCommand_WhenRunCommandWithNotTainted_NotVulnerable()
    {
        const string json = "{ \"dbstats\" : 1 }";  
        var command = new JsonCommand<BsonDocument>(json);
        var result = _database.RunCommand(command);

        Assert.NotNull(result);
        AssertNotVulnerable();
    }
    
    [Fact]
    public void GivenAMongoDb_JSON_JsonCommand_WhenRunCommandAsyncWithTainted_VulnerabilityReported()
    {
        var json = "{ \"" + taintedStringCommand + "\" : 1 }";
        var command = new JsonCommand<BsonDocument>(json);
        var result = _database.RunCommandAsync(command).Result;
        
        Assert.NotNull(result);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMongoDb_BsonDocumentCommand_BsonParse_WhenRunCommandWithTainted_VulnerabilityReported()
    {
        var json = "{ \"" + taintedStringCommand + "\" : 1 }";
        var document = BsonDocument.Parse(json);
        var command = new BsonDocumentCommand<BsonDocument>(document);
        var result = _database.RunCommand(command);

        Assert.NotNull(result);
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAMongoDb_BsonDocumentCommand_BsonParse_WhenRunCommandWithNotTainted_NotVulnerable()
    {
        const string json = "{ \"dbstats\" : 1 }";  
        var document = BsonDocument.Parse(json);
        var command = new BsonDocumentCommand<BsonDocument>(document);
        var result = _database.RunCommand(command);

        Assert.NotNull(result);
        AssertNotVulnerable();
    }
    
    [Fact]
    public void GivenAMongoDb_BsonDocumentCommand_BsonParse_WhenRunCommandAsyncWithTainted_VulnerabilityReported()
    {
        var json = "{ \"" + taintedStringCommand + "\" : 1 }";
        var document = BsonDocument.Parse(json);
        var command = new BsonDocumentCommand<BsonDocument>(document);
        var result = _database.RunCommandAsync(command).Result;

        Assert.NotNull(result);
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAMongoDb_BsonDocument_BsonParse_WhenFindWithTainted_VulnerabilityReported()
    {
        var json = "{ \"Price\" :\"" + taintedString12 + "\"   }";
        var document = BsonDocument.Parse(json);
        var collection = _database.GetCollection<BsonDocument>("Books");
        var find = collection.Find(document).ToList();

        Assert.True(find.Count > 0);
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAMongoDb_BsonDocument_BsonParse_WhenFindAsyncWithTainted_VulnerabilityReported()
    {
        var json = "{ \"Price\" :\"" + taintedString12 + "\"   }";
        var document = BsonDocument.Parse(json);
        var collection = _database.GetCollection<BsonDocument>("Books");
        var find = collection.FindAsync(document).Result.ToList();

        Assert.True(find.Count > 0);
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAMongoDb_BsonDocument_JsonReaderWithContext_WhenFindWithTainted_VulnerabilityReported()
    {
        var json = "{ \"Price\" :\"" + taintedString12 + "\"   }";
        var reader = new JsonReader(json);
        var serializer = BsonSerializer.LookupSerializer<BsonDocument>();
        var context = BsonDeserializationContext.CreateRoot(reader);
        var doc = serializer.Deserialize(context);
        var collection = _database.GetCollection<BsonDocument>("Books");
        var books = collection.Find(doc).ToList();
        
        Assert.True(books.Count > 0);
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAMongoDb_BsonDocument_JsonReaderWithContext_WhenFindAsyncWithTainted_VulnerabilityReported()
    {
        var json = "{ \"Price\" :\"" + taintedString12 + "\"   }";
        var reader = new JsonReader(json);
        var serializer = BsonSerializer.LookupSerializer<BsonDocument>();
        var context = BsonDeserializationContext.CreateRoot(reader);
        var doc = serializer.Deserialize(context);
        var collection = _database.GetCollection<BsonDocument>("Books");
        var books = collection.FindAsync(doc).Result.ToList();
        
        Assert.True(books.Count > 0);
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAMongoDb_BsonDocument_JsonReader_WhenFindWithTainted_VulnerabilityReported()
    {
        var json = "{ \"Price\" :\"" + taintedString12 + "\"   }";
        var reader = new JsonReader(json);
        var doc = BsonSerializer.Deserialize<BsonDocument>(reader);
        var collection = _database.GetCollection<BsonDocument>("Books");
        var find = collection.Find(doc).ToList();
        
        Assert.True(find.Count > 0);
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAMongoDb_BsonDocument_JsonReader_WhenFindAsyncWithTainted_VulnerabilityReported()
    {
        var json = "{ \"Price\" :\"" + taintedString12 + "\"   }";
        var reader = new JsonReader(json);
        var doc = BsonSerializer.Deserialize<BsonDocument>(reader);
        var collection = _database.GetCollection<BsonDocument>("Books");
        var find = collection.FindAsync(doc).Result.ToList();
        
        Assert.True(find.Count > 0);
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAMongoDbString_WhenFindWithTainted_VulnerabilityReported()
    {
        var json = "{ \"Price\" :\"" + taintedString12 + "\"   }";
        var collection = _database.GetCollection<BsonDocument>("Books");
        var find = collection.Find(json).ToList();

        Assert.True(find.Count > 0);
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAMongoDb_String_WhenFindAsyncWithTainted_VulnerabilityReported()
    {
        var json = "{ \"Price\" :\"" + taintedString12 + "\"   }";
        var collection = _database.GetCollection<BsonDocument>("Books");
        var find = collection.FindAsync(json).Result.ToList();

        Assert.True(find.Count > 0);
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAMongoDb_String_WhenFindWithTainted_Attack_VulnerabilityReported()
    {
        var json = "{ \"$or\" : [{ \"Author.LastName\" : { \"$eq\" : \"notTainted2\" } }, { \"Author.Name\" : { \"$eq\" : \""+ taintedStringAttack +"\" } }] }";
        var collection = _database.GetCollection<BsonDocument>("Books");
        var find = collection.Find(json).ToList();

        Assert.True(find.Count > 0);
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAMongoDb_String_WhenFindAsyncWithTainted_Attack_VulnerabilityReported()
    {
        var json = "{ \"$or\" : [{ \"Author.LastName\" : { \"$eq\" : \"notTainted2\" } }, { \"Author.Name\" : { \"$eq\" : \""+ taintedStringAttack +"\" } }] }";
        var collection = _database.GetCollection<BsonDocument>("Books");
        var find = collection.FindAsync(json).Result.ToList();

        Assert.True(find.Count > 0);
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAMongoDb_String_WhenFindWithNotTainted_NotVulnerable()
    {
        var json = "{ \"Author.Name\" : \"John\"}";
        var collection = _database.GetCollection<BsonDocument>("Books");
        var find = collection.Find(json).ToList();

        Assert.True(find.Count > 0);
        AssertNotVulnerable();
    }
    
    [Fact]
    public void GivenAMongoDb_BsonDocument_WhenFindWithNotTainted_FirstOrDefault_NotVulnerable()
    {
        var bElement1 = new BsonElement("Author.Name", "John");
        var doc = new BsonDocument() { bElement1 };
        
        var collection = _database.GetCollection<BsonDocument>("Books");
        var find = collection.Find(doc).FirstOrDefault();

        Assert.NotNull(find);
        AssertNotVulnerable();
    }
}
