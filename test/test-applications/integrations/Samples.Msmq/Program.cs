using System;
using System.Messaging;
using System.Threading;

namespace Samples.Msmq
{
    internal class Program
    {
        const string PrivateQueuePath = ".\\Private$\\myQueue";
        public static void Main(string[] args)
        {
            var queue = DeleteIfExistsAndCreate(PrivateQueuePath);
            void Receive()
            {
                var rec = queue.Receive();
                Console.WriteLine("received");

            }
            var receiveThread = new Thread(Receive);
            receiveThread.Start();

            // sending is not thread safe
            void SendDifferentWays()
            {
                var transQ = SendWithinTransaction(queue);
                SendWithTransactionType(queue);
                SendWithoutTransaction(queue);
                Console.WriteLine("sent within transaction");
            }

            var sendDifferentWaysThread = new Thread(SendDifferentWays);
            sendDifferentWaysThread.Start();

            sendDifferentWaysThread.Join();
            receiveThread.Join();

            queue.Purge();

            Console.WriteLine("finish");
        }

        private static MessageQueue DeleteIfExistsAndCreate(string privateQueuePath)
        {
            if (MessageQueue.Exists(privateQueuePath))
            {
                MessageQueue.Delete(privateQueuePath);
            }
            var queue = MessageQueue.Create(privateQueuePath, true);
            return queue;
        }

        private static MessageQueueTransaction SendWithinTransaction(MessageQueue queue)
        {
            var transQ = new MessageQueueTransaction();
            transQ.Begin();
            queue.Send(3, "label3", transQ);
            transQ.Commit();
            return transQ;
        }

        private static void SendWithTransactionType(MessageQueue queue)
        {
            queue.Send("a message with transaction type", "label2", MessageQueueTransactionType.Single);
        }

        private static void SendWithoutTransaction(MessageQueue queue)
        {
            queue.Send("a message", "label");
        }
    }
}
