using System;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace;
using Datadog.Trace.ClrProfiler;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Operations;

namespace Samples.MongoDB
{
    public static class Program
    {
        private static string Host()
        {
            return Environment.GetEnvironmentVariable("MONGO_HOST") ?? "localhost";
        }

        public static void Main(string[] args)
        {
            Console.WriteLine($"Profiler attached: {Instrumentation.ProfilerAttached}");
            Console.WriteLine($"Platform: {(Environment.Is64BitProcess ? "x64" : "x32")}");

            var newDocument = new BsonDocument
            {
                { "name", "MongoDB" },
                { "type", "Database" },
                { "count", 1 },
                {
                    "info", new BsonDocument
                    {
                        { "x", 203 },
                        { "y", 102 }
                    }
                }
            };


            using (var mainScope = Tracer.Instance.StartActive("Main()", serviceName: "Samples.MongoDB"))
            {
                var connectionString = $"mongodb://{Host()}:27017";
                var client = new MongoClient(connectionString);
                var database = client.GetDatabase("test-db");
                var collection = database.GetCollection<BsonDocument>("employees");

                Run(collection, newDocument);
                RunAsync(collection, newDocument).Wait();
            }
        }

        public static void Run(IMongoCollection<BsonDocument> collection, BsonDocument newDocument)
        {
            var allFilter = new BsonDocument();

            using (var syncScope = Tracer.Instance.StartActive("sync-calls", serviceName: "Samples.MongoDB"))
            {
#if MONGODB_2_2
                collection.DeleteMany(allFilter);
                collection.InsertOne(newDocument);

#if MONGODB_2_7
                var count = collection.CountDocuments(new BsonDocument());
#else
                var count = collection.Count(new BsonDocument());
#endif
                Console.WriteLine($"Documents: {count}");

                var find = collection.Find(allFilter);
                var allDocuments = find.ToList();
                Console.WriteLine(allDocuments.FirstOrDefault());

                // Run an explain query to invoke problematic MongoDB.Driver.Core.Operations.FindOpCodeOperation<TDocument>
                // https://stackoverflow.com/questions/49506857/how-do-i-run-an-explain-query-with-the-2-4-c-sharp-mongo-driver
                var options = new FindOptions
                {
                    Modifiers = new BsonDocument("$explain", true)
                };
                // Without properly unboxing generic arguments whose instantiations
                // are valuetypes, the following line will fail with
                // System.EntryPointNotFoundException: Entry point was not found.
                var cursor = collection.Find(x => true, options).ToCursor();
                foreach (var document in cursor.ToEnumerable())
                {
                    Console.WriteLine(document);
                }
#endif
            }
        }

        public static async Task RunAsync(IMongoCollection<BsonDocument> collection, BsonDocument newDocument)
        {
            var allFilter = new BsonDocument();

            using (var asyncScope = Tracer.Instance.StartActive("async-calls", serviceName: "Samples.MongoDB"))
            {
                await collection.DeleteManyAsync(allFilter);
                await collection.InsertOneAsync(newDocument);

#if MONGODB_2_7
                var count = await collection.CountDocumentsAsync(new BsonDocument());
#else
                var count = await collection.CountAsync(new BsonDocument());
#endif

                Console.WriteLine($"Documents: {count}");

                var find = await collection.FindAsync(allFilter);
                var allDocuments = await find.ToListAsync();
                Console.WriteLine(allDocuments.FirstOrDefault());
            }
        }
    }
}
