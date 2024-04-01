using System;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Moq;
using Samples.InstrumentedTests.Iast.Vulnerabilities.NosqlInjection.DatabaseHelper;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.NosqlInjection;

public class MongoDbTests : InstrumentationTestsBase
{
    private Mock<IMongoClient> _client;
    private readonly IMongoDatabase _database;
    
    private readonly string _taintedString12 = "12";
    private readonly string _taintedStringCommand = "dbstats";
    private readonly string _taintedStringAttack = "nnn\"}}, { \"Author.Name\" : { \"$ne\" : \"notTainted2";

    public MongoDbTests()
    {
        _client = MockMongoDb.MockMongoClient();
        _database = _client.Object.GetDatabase("test-db");

        // Add all tainted values
        AddTainted(_taintedString12);
        AddTainted(_taintedStringCommand);
        AddTainted(_taintedStringAttack);
    }

    public override void Dispose()
    {
        _client = null;
        base.Dispose();
    }

    [Fact]
    public void GivenAMongoDb_JSON_JsonCommand_WhenRunCommandWithTainted_VulnerabilityReported()
    {
        var json = "{ \"" + _taintedStringCommand + "\" : 1 }";
        var command = new JsonCommand<BsonDocument>(json);
        var result = _database.RunCommand(command);
        
        Assert.NotNull(result);
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAMongoDb_JSON_JsonCommand_notPrettyFormatted_WhenRunCommandWithTainted_VulnerabilityReported()
    {
        var json = "{ \"" + _taintedStringCommand + "\" :    1    }";
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
        var json = "{ \"" + _taintedStringCommand + "\" : 1 }";
        var command = new JsonCommand<BsonDocument>(json);
        var result = _database.RunCommandAsync(command).Result;
        
        Assert.NotNull(result);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAMongoDb_BsonDocumentCommand_BsonParse_WhenRunCommandWithTainted_VulnerabilityReported()
    {
        var json = "{ \"" + _taintedStringCommand + "\" : 1 }";
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
        var json = "{ \"" + _taintedStringCommand + "\" : 1 }";
        var document = BsonDocument.Parse(json);
        var command = new BsonDocumentCommand<BsonDocument>(document);
        var result = _database.RunCommandAsync(command).Result;

        Assert.NotNull(result);
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAMongoDb_BsonDocument_BsonParse_WhenFindWithTainted_VulnerabilityReported()
    {
        var json = "{ \"Price\" :\"" + _taintedString12 + "\"   }";
        var document = BsonDocument.Parse(json);
        var collection = _database.GetCollection<BsonDocument>("Books");
        var find = collection.Find(document).ToList();

        Assert.True(find.Count > 0);
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAMongoDb_BsonDocument_BsonParse_WhenFindAsyncWithTainted_VulnerabilityReported()
    {
        var json = "{ \"Price\" :\"" + _taintedString12 + "\"   }";
        var document = BsonDocument.Parse(json);
        var collection = _database.GetCollection<BsonDocument>("Books");
        var find = collection.FindAsync(document).Result.ToList();

        Assert.True(find.Count > 0);
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAMongoDb_BsonDocument_JsonReaderWithContext_WhenFindWithTainted_VulnerabilityReported()
    {
        var json = "{ \"Price\" :\"" + _taintedString12 + "\"   }";
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
        var json = "{ \"Price\" :\"" + _taintedString12 + "\"   }";
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
        var json = "{ \"Price\" :\"" + _taintedString12 + "\"   }";
        var reader = new JsonReader(json);
        var doc = BsonSerializer.Deserialize<BsonDocument>(reader);
        var collection = _database.GetCollection<BsonDocument>("Books");
        var find = collection.Find(doc).ToList();
        
        Assert.True(find.Count > 0);
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAMongoDb_BsonDocument_JsonReader_DeserializeString_WhenFindWithTainted_VulnerabilityReported()
    {
        var json = "{ \"Price\" :\"" + _taintedString12 + "\"   }";
        var doc = BsonSerializer.Deserialize<BsonDocument>(json);
        var collection = _database.GetCollection<BsonDocument>("Books");
        var find = collection.Find(doc).ToList();
        
        Assert.True(find.Count > 0);
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAMongoDb_BsonDocument_JsonReader_DeserializeString2_WhenFindWithTainted_VulnerabilityReported()
    {
        var json = "{ \"Price\" :\"" + _taintedString12 + "\"   }";
        var doc = BsonSerializer.Deserialize(json, typeof(BsonDocument));
        var collection = _database.GetCollection<BsonDocument>("Books");
        var find = collection.Find((BsonDocument)doc).ToList();
        
        Assert.True(find.Count > 0);
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAMongoDb_BsonDocument_JsonReader_WhenFindAsyncWithTainted_VulnerabilityReported()
    {
        var json = "{ \"Price\" :\"" + _taintedString12 + "\"   }";
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
        var json = "{ \"Price\" :\"" + _taintedString12 + "\"   }";
        var collection = _database.GetCollection<BsonDocument>("Books");
        var find = collection.Find(json).ToList();

        Assert.True(find.Count > 0);
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAMongoDb_String_WhenFindAsyncWithTainted_VulnerabilityReported()
    {
        var json = "{ \"Price\" :\"" + _taintedString12 + "\"   }";
        var collection = _database.GetCollection<BsonDocument>("Books");
        var find = collection.FindAsync(json).Result.ToList();

        Assert.True(find.Count > 0);
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAMongoDb_String_WhenFindWithTainted_Attack_VulnerabilityReported()
    {
        var json = "{ \"$or\" : [{ \"Author.LastName\" : { \"$eq\" : \"notTainted2\" } }, { \"Author.Name\" : { \"$eq\" : \""+ _taintedStringAttack +"\" } }] }";
        var collection = _database.GetCollection<BsonDocument>("Books");
        var find = collection.Find(json).ToList();

        Assert.True(find.Count > 0);
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAMongoDb_String_WhenFindAsyncWithTainted_Attack_VulnerabilityReported()
    {
        var json = "{ \"$or\" : [{ \"Author.LastName\" : { \"$eq\" : \"notTainted2\" } }, { \"Author.Name\" : { \"$eq\" : \""+ _taintedStringAttack +"\" } }] }";
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
