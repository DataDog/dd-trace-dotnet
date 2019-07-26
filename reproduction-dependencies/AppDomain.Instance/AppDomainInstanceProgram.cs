using System;
using System.Data.Common;
using System.Data.SqlClient;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;

namespace AppDomain.Instance
{
    public class AppDomainInstanceProgram : MarshalByRefObject
    {
        public NestedProgram WorkerProgram { get; set; }

        public int Main(string[] args)
        {
            Console.WriteLine("Loading Datadog.Trace.ClrProfiler.Managed to kick off ModuleLoadFinished logic"
                + ". Profiler attached: " + Datadog.Trace.ClrProfiler.Instrumentation.ProfilerAttached);
            Console.WriteLine("Starting AppDomain Instance Test");

            string appDomainName = "crash-dummy";
            int index = 1;

            if (args?.Length > 0)
            {
                appDomainName = args[0];
                index = int.Parse(args[1]);
            }
            try
            {
                var instance = new NestedProgram()
                {
                    AppDomainName = appDomainName,
                    AppDomainIndex = index
                };

                WorkerProgram = instance;

                // Act like we're doing some continuing work
                while (true)
                {
                    Thread.Sleep(500);
                    instance.MakeSomeCall();

                    if (instance.TotalCallCount > 20)
                    {
                        // Meh, call it quits
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"We have encountered an exception in this instance: {appDomainName} : {ex.Message}");
                Console.Error.WriteLine(ex);
                return -10;
            }

            return 0;
        }

        public class NestedProgram : MarshalByRefObject
        {
            public object CallLock { get; } = new object();
            public string AppDomainName { get; set; }
            public int AppDomainIndex { get; set; }
            public int TotalCallCount { get; set; }
            public int CurrentCallCount { get; set; }
            public bool DenyAllCalls { get; set; }

            private readonly string _connectionString = @"Server=(localdb)\MSSQLLocalDB;Integrated Security=true";
            private readonly string _jokeTable = "GreatJoke";

            public NestedProgram()
            {
                InitializeJokeTable();
            }

            public void MakeSomeCall()
            {
                if (DenyAllCalls)
                {
                    return;
                }

                lock (CallLock)
                {
                    try
                    {
                        CurrentCallCount++;

                        var httpClient = new HttpClient();
                        httpClient.DefaultRequestHeaders
                              .Accept
                              .Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        Console.WriteLine($"App {AppDomainIndex} - Starting client.GetAsync");
                        var responseTask = httpClient.GetAsync("https://icanhazdadjoke.com/");
                        responseTask.Wait(1000);
                        if (responseTask.IsCompleted)
                        {
                            var jokeReaderTask = responseTask.Result.Content.ReadAsStringAsync();
                            jokeReaderTask.Wait();
                            var joke = jokeReaderTask.Result;

                            StoreJoke(joke);
                            var lastStoredJoke = GetLastJoke();

                            Console.WriteLine($"Joke: {lastStoredJoke}");
                        }

                        Console.WriteLine($"App {AppDomainIndex} - Finished client.GetAsync");
                    }
                    finally
                    {
                        lock (CallLock)
                        {
                            CurrentCallCount--;
                        }

                        TotalCallCount++;
                    }
                }
            }

            public string GetLastJoke()
            {
                using (var connection = (DbConnection)new SqlConnection(_connectionString))
                using (var command = connection.CreateCommand())
                {
                    Console.WriteLine($"Reading last joke from SQL for instance #{AppDomainIndex}");
                    command.CommandText = $"SELECT TOP 1 Text FROM {_jokeTable} ORDER BY Id DESC;";
                    connection.Open();
                    var reader = command.ExecuteReader();
                    reader.Read();
                    var result = reader[0];
                    return result.ToString();
                }
            }

            public void StoreJoke(string joke)
            {
                joke = joke.Replace("'", "`"); // horrible sanitization :)
                using (var connection = new SqlConnection(_connectionString))
                using (var command = new SqlCommand())
                {
                    Console.WriteLine($"Inserting joke into SQL for instance #{AppDomainIndex}");
                    connection.Open();
                    command.Connection = connection;
                    command.CommandText = $"INSERT INTO {_jokeTable} (Text) VALUES ('{joke}')";
                    command.ExecuteNonQuery();
                    connection.Close();
                }
            }

            private void InitializeJokeTable()
            {
                using (var connection = new SqlConnection(_connectionString))
                using (var command = new SqlCommand())
                {
                    connection.Open();
                    command.Connection = connection;
                    command.CommandText = $"IF NOT EXISTS( SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{_jokeTable}') " +
                                          $"CREATE TABLE {_jokeTable} (Id int identity(1,1),Text VARCHAR(500))";
                    command.ExecuteNonQuery();
                    connection.Close();
                    Console.WriteLine($"Created joke table for instance #{AppDomainIndex}");
                }
            }
        }
    }
}
