using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;

namespace Samples.Kafka;

internal abstract class ConsumerBase : IDisposable
{
    private readonly IConsumer<string, string> _consumer;
    public static int TotalAsyncMessages = 0;
    public static int TotalSyncMessages = 0;
    public static int TotalTombstones = 0;
    
    protected ConsumerBase(ConsumerConfig config, string topic, string consumerName)
    {
        ConsumerName = consumerName;
        _consumer = new ConsumerBuilder<string, string>(config).Build();
        _consumer.Subscribe(topic);
    }

    protected string ConsumerName { get; }

    public bool Consume(int retries, int timeoutMilliSeconds)
    {
        try
        {
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    // will block until a message is available
                    // on 1.5.3 this will throw if the topic doesn't exist
                    var consumeResult = _consumer.Consume(timeoutMilliSeconds);
                    if (consumeResult is null)
                    {
                        Console.WriteLine($"{ConsumerName}: Null consume result");
                        return true;
                    }

                    if (consumeResult.IsPartitionEOF)
                    {
                        Console.WriteLine($"{ConsumerName}: Reached EOF");
                        return true;
                    }
                    else
                    {
                        HandleMessage(consumeResult);
                        return true;
                    }
                }
                catch (ConsumeException ex)
                {
                    Console.WriteLine($"Consume Exception in manual consume: {ex}");
                }

                Task.Delay(500);
            }
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine($"{ConsumerName}: Cancellation requested, exiting.");
        }

        return false;
    }

    public void Consume(CancellationToken cancellationToken = default)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // will block until a message is available
                var consumeResult = _consumer.Consume(cancellationToken);

                if (consumeResult.IsPartitionEOF)
                {
                    Console.WriteLine($"{ConsumerName}: Reached EOF");
                }
                else
                {
                    HandleMessage(consumeResult);
                }
            }
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine($"{ConsumerName}: Cancellation requested, exiting.");
        }
    }

    public void ConsumeWithExplicitCommit(int commitEveryXMessages, CancellationToken cancellationToken = default, bool useCommitAll = false)
    {
        ConsumeResult<string, string> consumeResult = null;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // will block until a message is available
                consumeResult = _consumer.Consume(cancellationToken);

                if (consumeResult.IsPartitionEOF)
                {
                    Console.WriteLine($"{ConsumerName}: Reached EOF");
                }
                else
                {
                    HandleMessage(consumeResult);
                }

                if (consumeResult.Offset % commitEveryXMessages == 0)
                {
                    try
                    {
                        Console.WriteLine($"{ConsumerName}: committing...");
                        if (useCommitAll)
                        {
                            _consumer.Commit(); 
                        }
                        else
                        {
                            _consumer.Commit(consumeResult);
                        }
                        
                    }
                    catch (KafkaException e)
                    {
                        Console.WriteLine($"{ConsumerName}: commit error: {e.Error.Reason}");
                    }
                }
            }
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine($"{ConsumerName}: Cancellation requested, exiting.");
        }

        // As we're doing manual commit, make sure we force a commit now
        if (consumeResult is not null)
        {
            Console.WriteLine($"{ConsumerName}: committing...");
            _consumer.Commit(consumeResult);
        }
    }

    protected abstract void HandleMessage(ConsumeResult<string, string> consumeResult);

    public void Dispose()
    {
        Console.WriteLine($"{ConsumerName}: Closing consumer");
        _consumer?.Close();
        _consumer?.Dispose();
    }

    public static IEnumerable<string> ExtractValues(Headers headers, string name)
    {
        if (headers.TryGetLastBytes(name, out var bytes))
        {
            try
            {
                return new[] { Encoding.UTF8.GetString(bytes) };
            }
            catch (Exception)
            {
                // ignored
            }
        }

        return Enumerable.Empty<string>();
    }
}
