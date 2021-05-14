using System;
using System.Linq;
using System.Messaging;

namespace Samples.Msmq
{
    public class Program
    {
        const string PrivateTransactionalQueuePath = ".\\Private$\\private-transactional-queue";
        const string PrivateNonTransactionalQueuePath = ".\\Private$\\private-nontransactional-queue";
        public static void Main(string[] args)
        {
            Console.WriteLine("first arg " + args.First());
            Console.WriteLine("second arg " + args[1]);

            var transactionalScenarioCount = int.TryParse(args[0], out var transactionalScenarioC) ? transactionalScenarioC : 5;
            var nonTransactionalScenarioCount = int.TryParse(args[1], out var nonTransactionalScenarioC) ? nonTransactionalScenarioC : 5;

            var transactionalQueue = Create(PrivateTransactionalQueuePath, true);
            var nonTransactionalQueue = Create(PrivateNonTransactionalQueuePath, false);
            var counter = Math.Max(transactionalScenarioCount, nonTransactionalScenarioCount);
            Console.WriteLine("counter " + counter);
            for (var i = 0; i < counter; i++)
            {
                if (transactionalScenarioCount-- > 0)
                {
                    SendWithinTransaction(transactionalQueue);
                    ReceivePeekOnce(transactionalQueue, i);
                }
                if (nonTransactionalScenarioCount-- > 0)
                {
                    SendWithoutTransaction(nonTransactionalQueue);
                    ReceivePeekOnce(nonTransactionalQueue, i);
                }
                if (transactionalScenarioCount-- > 0)
                {
                    SendWithTransactionType(transactionalQueue);
                    ReceivePeekOnce(transactionalQueue, i);
                }
            }

            transactionalQueue.Purge();
            nonTransactionalQueue.Purge();
        }

        private static void ReceivePeekOnce(MessageQueue queue, int i)
        {
            if (i == 0)
                queue.Peek(TimeSpan.FromSeconds(1)); //test peek once
            queue.Receive(TimeSpan.FromSeconds(1));
        }

        private static MessageQueue Create(string privateQueuePath, bool transactional)
        {
            if (MessageQueue.Exists(privateQueuePath))
            {
                MessageQueue.Delete(privateQueuePath);
            }
            return MessageQueue.Create(privateQueuePath, transactional);
        }

        private static MessageQueueTransaction SendWithinTransaction(MessageQueue queue)
        {
            using var transQ = new MessageQueueTransaction();
            transQ.Begin();
            queue.Send($"Message no {Guid.NewGuid()}", GetLabel(), transQ);
            transQ.Commit();
            return transQ;
        }

        private static void SendWithTransactionType(MessageQueue queue) => queue.Send($"Message no {Guid.NewGuid()}", GetLabel(), MessageQueueTransactionType.Single);

        private static void SendWithoutTransaction(MessageQueue queue) => queue.Send("Non transactional message", GetLabel());

        private static string GetLabel() => "label-" + Guid.NewGuid();
    }
}
