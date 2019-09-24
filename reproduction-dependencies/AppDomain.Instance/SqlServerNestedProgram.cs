using System;
using System.Data.Common;
using System.Data.SqlClient;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;

namespace AppDomain.Instance
{
    public class SqlServerNestedProgram : NestedProgram
    {
        public object CallLock { get; } = new object();
        public int TotalCallCount { get; set; }
        public int CurrentCallCount { get; set; }
        public bool DenyAllCalls { get; set; }

        private readonly string _connectionString = @"Server=(localdb)\MSSQLLocalDB;Integrated Security=true;Connection Timeout=30";
        private readonly string _jokeTable = "GreatJoke";

        public SqlServerNestedProgram()
        {
            InitializeJokeTable();
        }

        public override void Run()
        {
            // Act like we're doing some continuing work
            while (true)
            {
                Thread.Sleep(500);
                MakeSomeCall();

                if (TotalCallCount > 3)
                {
                    // Meh, call it quits
                    break;
                }
            }
        }

        private void MakeSomeCall()
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

        private string GetLastJoke()
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

        private void StoreJoke(string joke)
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
