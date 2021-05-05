using System;
using System.Diagnostics;
using System.Messaging;
using System.Threading;

namespace Samples.Msmq
{
    internal class Program
    {
        const string PrivateQueuePath = ".\\Private$\\myQueue3";
        public static void Main(string[] args)
        {
            var queue = GetOrCreate(PrivateQueuePath);
            SendWithTransactionType(queue);
            var message = queue.Receive();
            Console.WriteLine($"received {message}");

            //void Receive()
            //{
            //    queue.ReceiveCompleted += Queue_ReceiveCompleted;
            //    var rec = queue.Receive();
            //    Console.WriteLine($"received {rec}");

            //}
            //var receiveThread = new Thread(Receive);
            //receiveThread.Start();

            //// sending is not thread safe
            //void SendDifferentWays()
            //{
            //    var transQ = SendWithinTransaction(queue);
            //    SendWithTransactionType(queue);
            //    SendWithoutTransaction(queue);
            //    Console.WriteLine("sent within transaction");
            //}

            //var sendDifferentWaysThread = new Thread(SendDifferentWays);
            //sendDifferentWaysThread.Start();

            //sendDifferentWaysThread.Join();
            //receiveThread.Join();

            queue.Purge();

        }

        private static void Queue_ReceiveCompleted(object sender, ReceiveCompletedEventArgs e)
        {
            Debugger.Break();
        }

        private static MessageQueue GetOrCreate(string privateQueuePath) => MessageQueue.Exists(privateQueuePath) ? new MessageQueue(privateQueuePath) : MessageQueue.Create(privateQueuePath, true);

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
