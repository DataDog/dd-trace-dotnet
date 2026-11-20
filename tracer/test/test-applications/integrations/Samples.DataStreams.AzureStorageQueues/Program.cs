using System;
using System.Text.Json;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;

namespace Samples.DataStreams.AzureStorageQueues;

public static class Program
{
    private const string QueueName = "dsm-test-queue";

    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("AZURE_STORAGE_QUEUES_CONNECTION_STRING")
        ?? @"DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;QueueEndpoint=http://azurite:10001/devstoreaccount1;";

    public static async Task Main(string[] args)
    {
        var queue = new QueueClient(ConnectionString, QueueName);
        queue.CreateIfNotExists();

        var payload = new { Id = "message-1", Name = "test", Description = "sent by the sample" };
        var json = JsonSerializer.Serialize(payload);

        await queue.SendMessageAsync(json);
        await queue.SendMessageAsync(json);
        await queue.SendMessageAsync("plain-text-message");

        QueueMessage[] received;
        do
        {
            var response = await queue.ReceiveMessagesAsync(maxMessages: 2);
            received = response.Value;
            foreach (var message in received)
            {
                Console.WriteLine($"Received '{Truncate(message.MessageText ?? string.Empty, 220)}'");
                await queue.DeleteMessageAsync(message.MessageId, message.PopReceipt);
            }
        }
        while (received is { Length: > 0 });
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value.Substring(0, max) + "...";
}
