namespace ServiceBus.Minimal.MassTransit
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Threading;
    using System.Threading.Tasks;
    using Components;
    using Components.Activities;
    using Components.Consumers;
    using Components.StateMachines;
    using global::MassTransit;
    using global::MassTransit.EntityFrameworkCoreIntegration;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using ServiceBus.Minimal.MassTransit.Contracts;
    using ServiceBus.Minimal.MassTransit.Contracts.Enums;

    class Program
    {
        static async Task Main(string[] args)
        {
            var connectionString = GetSqlServerConnectionString("sample-batch");

            var services = new ServiceCollection();
            services.AddMassTransit(cfg =>
            {
                cfg.SetKebabCaseEndpointNameFormatter();
                cfg.AddSagaStateMachine<BatchStateMachine, BatchState>(typeof(BatchStateMachineDefinition))
                    .EntityFrameworkRepository(r =>
                    {
                        r.ConcurrencyMode = ConcurrencyMode.Pessimistic;
                        r.ExistingDbContext<SampleBatchDbContext>();

                        // I specified the MsSqlLockStatements because in my State Entities EFCore EntityConfigurations, I changed the column name from CorrelationId, to "BatchId" and "BatchJobId"
                        // Otherwise, I could just use r.UseSqlServer(), which uses the default, which are "... WHERE CorrelationId = @p0"
                        r.LockStatementProvider =
                            new CustomSqlLockStatementProvider("select * from {0}.{1} WITH (UPDLOCK, ROWLOCK) WHERE BatchId = @p0");
                    });

                cfg.AddSagaStateMachine<JobStateMachine, JobState>(typeof(JobStateMachineDefinition))
                    .EntityFrameworkRepository(r =>
                    {
                        r.ConcurrencyMode = ConcurrencyMode.Pessimistic;
                        r.ExistingDbContext<SampleBatchDbContext>();

                        // I specified the MsSqlLockStatements because in my State Entities EFCore EntityConfigurations, I changed the column name from CorrelationId, to "BatchId" and "BatchJobId"
                        // Otherwise, I could just use r.UseSqlServer(), which uses the default, which are "... WHERE CorrelationId = @p0"
                        r.LockStatementProvider =
                            new CustomSqlLockStatementProvider("select * from {0}.{1} WITH (UPDLOCK, ROWLOCK) WHERE BatchJobId = @p0");
                    });

                cfg.AddConsumersFromNamespaceContaining<ConsumerAnchor>();
                cfg.AddActivitiesFromNamespaceContaining<ActivitiesAnchor>();

                cfg.UsingInMemory((x,y) => {
                    var endpointNameFormatter = x.GetRequiredService<IEndpointNameFormatter>();
                    EndpointConvention.Map<ProcessBatchJob>(new Uri($"queue:{endpointNameFormatter.Consumer<ProcessBatchJobConsumer>()}"));

                    y.UseInMemoryScheduler();

                    y.ConfigureEndpoints(x);
                });
            });
            services.AddLogging(logging => 
            {
                logging.SetMinimumLevel(LogLevel.Warning);
                logging.AddConsole();
            });

            services.AddDbContext<SampleBatchDbContext>(
                x => x.UseSqlServer(
                    connectionString,
                    opts => opts.CommandTimeout(60)));

            await using var provider = services.BuildServiceProvider(true);

            EnsureEfDbCreated(provider);

            var busControl = provider.GetRequiredService<IBusControl>();

            var startTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token;
            await busControl.StartAsync(startTokenSource);
            try
            {
                await Task.Run(() => SendMessagesAsync(provider), CancellationToken.None);
            }
            finally
            {
                await busControl.StopAsync(TimeSpan.FromSeconds(30));
            }

            Console.WriteLine("App completed successfully");
        }

        static async Task SendMessagesAsync(IServiceProvider provider, int jobCount = 10, int activeThreshold = 10, int? delayInSeconds = null)
        {
            var clientFactory = provider.GetRequiredService<IClientFactory>();
            var submitBatchClient = clientFactory.CreateRequestClient<SubmitBatch>(TimeSpan.FromSeconds(60));
            var batchStatusClient = clientFactory.CreateRequestClient<BatchStatusRequested>(TimeSpan.FromSeconds(60));

            List<Task> batchCompletedTasks = new();

            for (int i = 0; i < 1; i++)
            {
                var id = NewId.NextGuid();
                var orderIds = new List<Guid>();
                for (int j = 0; j < jobCount; j++)
                {
                    orderIds.Add(NewId.NextGuid());
                }

                var (accepted, rejected) = await submitBatchClient.GetResponse<BatchSubmitted, BatchRejected>(new
                {
                    BatchId = id,
                    InVar.Timestamp,
                    Action = BatchAction.CancelOrders,
                    OrderIds = orderIds.ToArray(),
                    ActiveThreshold = activeThreshold,
                    DelayInSeconds = delayInSeconds
                });

                if (accepted.IsCompleted)
                {
                    Console.WriteLine($"Successfully sent batch {id}. Waiting on completion.");
                    batchCompletedTasks.Add(WaitForCompletion(batchStatusClient, accepted));
                }
                else
                {
                    await rejected;
                }
            }

            Task.WaitAll(batchCompletedTasks.ToArray());
        }

        static async Task WaitForCompletion(IRequestClient<BatchStatusRequested> batchStatusClient, Task<Response<BatchSubmitted>> acceptedTask)
        {
            Response<BatchSubmitted> acceptedResult = await acceptedTask;
            Guid id = acceptedResult.Message.BatchId;

            while (true)
            {
                var (status, notFound) = await batchStatusClient.GetResponse<BatchStatus, BatchNotFound>(new
                {
                    BatchId = id,
                    InVar.Timestamp,
                });

                if (IsCompletedSuccessfully(notFound))
                {
                    continue;
                }

                Response<BatchStatus> response = await status;
                if (response.Message.ProcessingJobCount == 0 &&
                    response.Message.UnprocessedJobCount == 0 &&
                    response.Message.State == "Finished")
                {
                    Console.WriteLine($"Batch {id} completed");
                    return;
                }
            }
        }

        static void EnsureEfDbCreated(IServiceProvider provider)
        {
            using (var scope = provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SampleBatchDbContext>();

                db.Database.EnsureCreated();
            }
        }

        static string GetSqlServerConnectionString(string overrideInitialCatalog = null)
        {
            var connectionString = Environment.GetEnvironmentVariable("SQLSERVER_CONNECTION_STRING") ??
                @"Server=(localdb)\MSSQLLocalDB;Connection Timeout=60";

            var builder = new SqlConnectionStringBuilder(connectionString);
            if (!string.IsNullOrWhiteSpace(overrideInitialCatalog))
            {
                builder.InitialCatalog = overrideInitialCatalog;
            }

            builder.Add("MultipleActiveResultSets", "True");

            return builder.ConnectionString;
        }

        static bool IsCompletedSuccessfully(Task task)
        {
            return task.IsCompleted && !task.IsCanceled && !task.IsFaulted;
        }
    }
}
