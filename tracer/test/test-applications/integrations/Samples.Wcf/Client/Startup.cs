using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using ActivitySampleHelper;

namespace Samples.Wcf.Client
{
    public class Startup
    {
        private static readonly ActivitySourceHelper _sampleHelpers = new("Samples.Wcf");

        public static async Task InvokeCalculatorService(Binding binding, Uri baseAddress, int expectedExceptionCount)
        {
            var calculatorServiceBaseAddress = new Uri(baseAddress, "CalculatorService");
            var address = new EndpointAddress(calculatorServiceBaseAddress);
            int exceptionsSeen = 0;
            using (var calculator = new CalculatorClient(binding, address))
            {
                // Add the CustomEndpointBehavior / ClientMessageInspector to add headers on calls to the service
                calculator.ChannelFactory.Endpoint.EndpointBehaviors.Add(new CustomEndpointBehavior());

                exceptionsSeen += await Invoke_ServerSyncAdd_Endpoints(calculator);
                exceptionsSeen += await Invoke_ServerTaskAdd_Endpoints(calculator);
                exceptionsSeen += await Invoke_ServerAsyncAdd_Endpoints(calculator);
            }

            if (exceptionsSeen != expectedExceptionCount)
            {
                throw new Exception($"The test encountered an unexpected number of exceptions: {expectedExceptionCount} expected, {exceptionsSeen} actual");
            }
            else
            {
                LoggingHelper.WriteLineWithDate($"[Client] The test encountered the expected number of exceptions: {expectedExceptionCount}");
            }
        }

        /// <summary>
        /// WCF clients and servers can handle messages in three ways:
        ///     1. Synchronous
        ///     2. Task-Based Asynchronous Pattern
        ///     3. IAsyncResult Asynchronous Pattern
        ///
        /// In this method, the server's handling is held constant (synchronous endpoint) but the client
        /// accesses the server using each of the 3 ways.
        /// </summary>
        /// <param name="calculator">The calculator client</param>
        /// <returns></returns>
        private static async Task<int> Invoke_ServerSyncAdd_Endpoints(CalculatorClient calculator)
        {
            int exceptionsSeen = 0;


            try
            {
                using var scope = SampleHelpers.CreateScope("ServerEmptyActionAdd");
                calculator.DatadogScope = scope;
                Console.WriteLine();
                LoggingHelper.WriteLineWithDate($"[Client] Invoke: ServerEmptyActionAdd(1, 2)");
                double result = calculator.ServerEmptyActionAdd(1, 2);
                LoggingHelper.WriteLineWithDate($"[Client] Result: {result}");
            }
            catch (Exception ex)
            {
                LoggingHelper.WriteLineWithDate($"[Client] Message resulted in an exception. Exception message: {ex}");
                exceptionsSeen++;
            }

            try
            {
                using var scope = SampleHelpers.CreateScope("Sync_ServerSyncAdd");
                calculator.DatadogScope = scope;
                Console.WriteLine();
                LoggingHelper.WriteLineWithDate($"[Client] Invoke: Sync_ServerSyncAdd(1, 2)");
                double result = calculator.Sync_ServerSyncAdd(1, 2);
                LoggingHelper.WriteLineWithDate($"[Client] Result: {result}");
            }
            catch (Exception ex)
            {
                LoggingHelper.WriteLineWithDate($"[Client] Message resulted in an exception. Exception message: {ex}");
                exceptionsSeen++;
            }

            try
            {
                using var scope = SampleHelpers.CreateScope("BeginEnd_ServerSyncAdd");
                calculator.DatadogScope = scope;
                Console.WriteLine();
                LoggingHelper.WriteLineWithDate($"[Client] Invoke: Begin_ServerSyncAdd(1, 2)");
                IAsyncResult asyncResult = calculator.Begin_ServerSyncAdd(1, 2, null, null);
                LoggingHelper.WriteLineWithDate($"[Client] Invoke: End_ServerSyncAdd(asyncResult)");
                double result = calculator.End_ServerSyncAdd(asyncResult);
                LoggingHelper.WriteLineWithDate($"[Client] Result: {result}");
            }
            catch (Exception ex)
            {
                LoggingHelper.WriteLineWithDate($"[Client] Message resulted in an exception. Exception message: {ex}");
                exceptionsSeen++;
            }

            try
            {
                using var scope = SampleHelpers.CreateScope("Task_ServerSyncAdd");
                calculator.DatadogScope = scope;
                Console.WriteLine();
                LoggingHelper.WriteLineWithDate($"[Client] Invoke: Task_ServerSyncAdd(1, 2)");
                double result = await calculator.Task_ServerSyncAdd(1, 2);
                LoggingHelper.WriteLineWithDate($"[Client] Result: {result}");
            }
            catch (Exception ex)
            {
                LoggingHelper.WriteLineWithDate($"[Client] Message resulted in an exception. Exception message: {ex}");
                exceptionsSeen++;
            }


            return exceptionsSeen;
        }

        /// <summary>
        /// WCF clients and servers can handle messages in three ways:
        ///     1. Synchronous
        ///     2. Task-Based Asynchronous Pattern
        ///     3. IAsyncResult Asynchronous Pattern
        ///
        /// In this method, the server's handling is held constant (Task-based endpoint) but the client
        /// accesses the server using each of the 3 ways.
        /// </summary>
        /// <param name="calculator">The calculator client</param>
        /// <returns></returns>
        private static async Task<int> Invoke_ServerTaskAdd_Endpoints(CalculatorClient calculator)
        {
            int exceptionsSeen = 0;

            try
            {
                using var scope = SampleHelpers.CreateScope("Sync_ServerTaskAdd");
                calculator.DatadogScope = scope;
                Console.WriteLine();
                LoggingHelper.WriteLineWithDate($"[Client] Invoke: Sync_ServerTaskAdd(1, 2)");
                double result = calculator.Sync_ServerTaskAdd(1, 2);
                LoggingHelper.WriteLineWithDate($"[Client] Result: {result}");
            }
            catch (Exception ex)
            {
                LoggingHelper.WriteLineWithDate($"[Client] Message resulted in an exception. Exception message: {ex}");
                exceptionsSeen++;
            }

            try
            {
                using var scope = SampleHelpers.CreateScope("BeginEnd_ServerTaskAdd");
                calculator.DatadogScope = scope;
                Console.WriteLine();
                LoggingHelper.WriteLineWithDate($"[Client] Invoke: Begin_ServerTaskAdd(1, 2)");
                IAsyncResult asyncResult = calculator.Begin_ServerTaskAdd(1, 2, null, null);
                LoggingHelper.WriteLineWithDate($"[Client] Invoke: End_ServerTaskAdd(asyncResult)");
                double result = calculator.End_ServerTaskAdd(asyncResult);
                LoggingHelper.WriteLineWithDate($"[Client] Result: {result}");
            }
            catch (Exception ex)
            {
                LoggingHelper.WriteLineWithDate($"[Client] Message resulted in an exception. Exception message: {ex}");
                exceptionsSeen++;
            }

            try
            {
                using var scope = SampleHelpers.CreateScope("Task_ServerTaskAdd");
                calculator.DatadogScope = scope;
                Console.WriteLine();
                LoggingHelper.WriteLineWithDate($"[Client] Invoke: Task_ServerTaskAdd(1, 2)");
                double result = await calculator.Task_ServerTaskAdd(1, 2);
                LoggingHelper.WriteLineWithDate($"[Client] Result: {result}");
            }
            catch (Exception ex)
            {
                LoggingHelper.WriteLineWithDate($"[Client] Message resulted in an exception. Exception message: {ex}");
                exceptionsSeen++;
            }

            return exceptionsSeen;
        }

        /// <summary>
        /// WCF clients and servers can handle messages in three ways:
        ///     1. Synchronous
        ///     2. Task-Based Asynchronous Pattern
        ///     3. IAsyncResult Asynchronous Pattern
        ///
        /// In this method, the server's handling is held constant (IASyncResult-based endpoint) but the client
        /// accesses the server using each of the 3 ways.
        /// </summary>
        /// <param name="calculator">The calculator client</param>
        /// <returns></returns>
        private static async Task<int> Invoke_ServerAsyncAdd_Endpoints(CalculatorClient calculator)
        {
            int exceptionsSeen = 0;

            try
            {
                using var scope = SampleHelpers.CreateScope("Sync_ServerAsyncAdd");
                calculator.DatadogScope = scope;
                Console.WriteLine();
                LoggingHelper.WriteLineWithDate($"[Client] Invoke: Sync_ServerAsyncAdd(1, 2, false, false)");
                double result = calculator.Sync_ServerAsyncAdd(1, 2, false, false);
                LoggingHelper.WriteLineWithDate($"[Client] Result: {result}");
            }
            catch (Exception ex)
            {
                LoggingHelper.WriteLineWithDate($"[Client] Message resulted in an exception. Exception message: {ex}");
                exceptionsSeen++;
            }

            try
            {
                using var scope = SampleHelpers.CreateScope("Begin_ServerAsyncAdd_false_false");
                calculator.DatadogScope = scope;
                Console.WriteLine();
                LoggingHelper.WriteLineWithDate($"[Client] Invoke: Begin_ServerAsyncAdd(1, 2, false, false)");
                IAsyncResult asyncResult = calculator.Begin_ServerAsyncAdd(1, 2, false, false, null, null);
                LoggingHelper.WriteLineWithDate($"[Client] Invoke: End_ServerAsyncAdd(asyncResult)");
                double result = calculator.End_ServerAsyncAdd(asyncResult);
                LoggingHelper.WriteLineWithDate($"[Client] Result: {result}");
            }
            catch (Exception ex)
            {
                LoggingHelper.WriteLineWithDate($"[Client] Message resulted in an exception. Exception message: {ex}");
                exceptionsSeen++;
            }

            try
            {
                using var scope = SampleHelpers.CreateScope("Begin_ServerAsyncAdd_true_false");
                calculator.DatadogScope = scope;
                Console.WriteLine();
                LoggingHelper.WriteLineWithDate($"[Client] Invoke: Begin_ServerAsyncAdd(1, 2, true, false)");
                IAsyncResult asyncResult = calculator.Begin_ServerAsyncAdd(1, 2, true, false, null, null);
                LoggingHelper.WriteLineWithDate($"[Client] Invoke: End_ServerAsyncAdd(asyncResult)");
                double result = calculator.End_ServerAsyncAdd(asyncResult);
                LoggingHelper.WriteLineWithDate($"[Client] Result: {result}");
            }
            catch (Exception ex)
            {
                LoggingHelper.WriteLineWithDate($"[Client] Message resulted in an exception. Exception message: {ex.Message}");
                exceptionsSeen++;
            }

            try
            {
                using var scope = SampleHelpers.CreateScope("Begin_ServerAsyncAdd_false_true");
                calculator.DatadogScope = scope;
                Console.WriteLine();
                LoggingHelper.WriteLineWithDate($"[Client] Invoke: Begin_ServerAsyncAdd(1, 2, false, true)");
                IAsyncResult asyncResult = calculator.Begin_ServerAsyncAdd(1, 2, false, true, null, null);
                LoggingHelper.WriteLineWithDate($"[Client] Invoke: End_ServerAsyncAdd(asyncResult)");
                double result = calculator.End_ServerAsyncAdd(asyncResult);
                LoggingHelper.WriteLineWithDate($"[Client] Result: {result}");
            }
            catch (Exception ex)
            {
                LoggingHelper.WriteLineWithDate($"[Client] Message resulted in an exception. Exception message: {ex.Message}");
                exceptionsSeen++;
            }

            try
            {
                using var scope = SampleHelpers.CreateScope("Begin_ServerAsyncAdd_true_true");
                calculator.DatadogScope = scope;
                Console.WriteLine();
                LoggingHelper.WriteLineWithDate($"[Client] Invoke: Begin_ServerAsyncAdd(1, 2, true, true)");
                IAsyncResult asyncResult = calculator.Begin_ServerAsyncAdd(1, 2, true, true, null, null);
                LoggingHelper.WriteLineWithDate($"[Client] Invoke: End_ServerAsyncAdd(asyncResult)");
                double result = calculator.End_ServerAsyncAdd(asyncResult);
                LoggingHelper.WriteLineWithDate($"[Client] Result: {result}");
            }
            catch (Exception ex)
            {
                LoggingHelper.WriteLineWithDate($"[Client] Message resulted in an exception. Exception message: {ex.Message}");
                exceptionsSeen++;
            }

            try
            {
                using var scope = SampleHelpers.CreateScope("Task_ServerAsyncAdd");
                calculator.DatadogScope = scope;
                Console.WriteLine();
                LoggingHelper.WriteLineWithDate($"[Client] Invoke: Task_ServerAsyncAdd(1, 2, false, false)");
                double result = await calculator.Task_ServerAsyncAdd(1, 2, false, false);
                LoggingHelper.WriteLineWithDate($"[Client] Result: {result}");
            }
            catch (Exception ex)
            {
                LoggingHelper.WriteLineWithDate($"[Client] Message resulted in an exception. Exception message: {ex}");
                exceptionsSeen++;
            }

            return exceptionsSeen;
        }
    }
}
