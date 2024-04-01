using MongoDB.Driver;

namespace Samples.Security.Data;

public static class MongoDbHelper
{
    public static IMongoDatabase CreateMongoDb()
    {
        var client = MockMongoDb.MockMongoClient();
        return client.Object.GetDatabase("test-db");
    }
}
