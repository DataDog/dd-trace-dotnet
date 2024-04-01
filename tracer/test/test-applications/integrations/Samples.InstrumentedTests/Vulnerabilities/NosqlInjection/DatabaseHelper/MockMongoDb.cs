using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.NosqlInjection.DatabaseHelper;

public class MockMongoDb
{
    public static Mock<IMongoClient> MockMongoClient()
    {
        var mockClient = new Mock<IMongoClient>();
        var mockDatabase = new Mock<IMongoDatabase>();
        var mockCollection = new Mock<IMongoCollection<BsonDocument>>();

        // Mock GetDatabase operation
        mockClient.Setup(c => c.GetDatabase(It.IsAny<string>(), null)).Returns(mockDatabase.Object);
        
        // Mock GetCollection operation
        mockDatabase.Setup(d => d.GetCollection<BsonDocument>(It.IsAny<string>(), null)).Returns(mockCollection.Object);

        // Mock RunCommand and RunCommandAsync operation
        mockDatabase.Setup(d => d.RunCommand(It.IsAny<Command<BsonDocument>>(), It.IsAny<ReadPreference>(), It.IsAny<CancellationToken>())).Returns(new BsonDocument());
        mockDatabase.Setup(d => d.RunCommandAsync(It.IsAny<Command<BsonDocument>>(), It.IsAny<ReadPreference>(), It.IsAny<CancellationToken>())).ReturnsAsync(new BsonDocument());

        // Mock Find and FindAsync operations
        var mockCursor = new Mock<IAsyncCursor<BsonDocument>>();
        mockCursor.Setup(c => c.Current).Returns(new List<BsonDocument> { new BsonDocument("mockField", "mockValue") });
        mockCursor.SetupSequence(c => c.MoveNext(It.IsAny<CancellationToken>())).Returns(true);
        mockCursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>())).Returns(Task.FromResult(true));
        mockCollection.Setup(c => c.FindSync(It.IsAny<FilterDefinition<BsonDocument>>(), It.IsAny<FindOptions<BsonDocument, BsonDocument>>(), default)).Returns(mockCursor.Object);
        mockCollection.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<BsonDocument>>(), It.IsAny<FindOptions<BsonDocument, BsonDocument>>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockCursor.Object);

        return mockClient;
    }

}
