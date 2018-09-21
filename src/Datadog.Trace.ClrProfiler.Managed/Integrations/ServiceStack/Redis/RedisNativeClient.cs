using System;
using System.Linq;
using System.Text;

namespace Datadog.Trace.ClrProfiler.Integrations.ServiceStack.Redis
{
    /// <summary>
    /// Wraps a RedisNativeClient.
    /// </summary>
    public static class RedisNativeClient
    {
        private static Func<object, byte[][], string> _sendExpectCode;
        private static Func<object, byte[][], object> _sendExpectComplexResponse;
        private static Func<object, byte[][], byte[]> _sendExpectData;
        private static Func<object, byte[][], object[]> _sendExpectDeeplyNestedMultiData;
        private static Func<object, byte[][], double> _sendExpectDouble;
        private static Func<object, byte[][], long> _sendExpectLong;
        private static Func<object, byte[][], byte[][]> _sendExpectMultiData;
        private static Action<object, byte[][]> _sendExpectSuccess;
        private static Action<object, byte[][]> _sendWithoutRead;

        /// <summary>
        /// called when a method enters
        /// </summary>
        /// <param name="arguments">the arguments passed to the method</param>
        /// <returns>data that will be passed to OnMethodExit</returns>
        public static object OnMethodEnter(object[] arguments)
        {
            Console.WriteLine($"ON METHOD ENTER {arguments}");
            return null;
        }

        /// <summary>
        /// called when a method exits
        /// </summary>
        /// <param name="enter">the object returned by OnMethodEnter</param>
        /// <param name="exception">a possible exception that was thrown during the execution of the method</param>
        /// <param name="result">the result of the execution of the method</param>
        public static void OnMethodExit(object enter, ref Exception exception, ref object result)
        {
            Console.WriteLine($"ON METHOD EXIT {enter} {exception} {result}");
        }

        /// <summary>
        /// Traces SendExpectCode.
        /// </summary>
        /// <param name="redisNativeClient">The redis native client.</param>
        /// <param name="cmdWithBinaryArgs">The command with arguments.</param>
        /// <returns>the original result.</returns>
        public static string SendExpectCode(object redisNativeClient, byte[][] cmdWithBinaryArgs)
        {
            if (_sendExpectCode == null)
            {
                _sendExpectCode = DynamicMethodBuilder.CreateMethodCallDelegate<Func<object, byte[][], string>>(redisNativeClient.GetType(), "SendExpectCode");
            }

            return Trace(redisNativeClient, cmdWithBinaryArgs, _sendExpectCode);
        }

        /// <summary>
        /// Traces SendExpectComplexResponse.
        /// </summary>
        /// <param name="redisNativeClient">The redis native client.</param>
        /// <param name="cmdWithBinaryArgs">The command with arguments.</param>
        /// <returns>the original result.</returns>
        public static object SendExpectComplexResponse(object redisNativeClient, byte[][] cmdWithBinaryArgs)
        {
            if (_sendExpectComplexResponse == null)
            {
                _sendExpectComplexResponse = DynamicMethodBuilder.CreateMethodCallDelegate<Func<object, byte[][], object>>(redisNativeClient.GetType(), "SendExpectComplexResponse");
            }

            return Trace(redisNativeClient, cmdWithBinaryArgs, _sendExpectComplexResponse);
        }

        /// <summary>
        /// Traces SendExpectData.
        /// </summary>
        /// <param name="redisNativeClient">The redis native client.</param>
        /// <param name="cmdWithBinaryArgs">The command with arguments.</param>
        /// <returns>the original result.</returns>
        public static byte[] SendExpectData(object redisNativeClient, byte[][] cmdWithBinaryArgs)
        {
            if (_sendExpectData == null)
            {
                _sendExpectData = DynamicMethodBuilder.CreateMethodCallDelegate<Func<object, byte[][], byte[]>>(redisNativeClient.GetType(), "SendExpectData");
            }

            return Trace(redisNativeClient, cmdWithBinaryArgs, _sendExpectData);
        }

        /// <summary>
        /// Traces SendExpectDeeplyNestedMultiData.
        /// </summary>
        /// <param name="redisNativeClient">The redis native client.</param>
        /// <param name="cmdWithBinaryArgs">The command with arguments.</param>
        /// <returns>the original result.</returns>
        public static object[] SendExpectDeeplyNestedMultiData(object redisNativeClient, byte[][] cmdWithBinaryArgs)
        {
            if (_sendExpectDeeplyNestedMultiData == null)
            {
                _sendExpectDeeplyNestedMultiData = DynamicMethodBuilder.CreateMethodCallDelegate<Func<object, byte[][], object[]>>(redisNativeClient.GetType(), "SendExpectDeeplyNestedMultiData");
            }

            return Trace(redisNativeClient, cmdWithBinaryArgs, _sendExpectDeeplyNestedMultiData);
        }

        /// <summary>
        /// Traces SendExpectDouble.
        /// </summary>
        /// <param name="redisNativeClient">The redis native client.</param>
        /// <param name="cmdWithBinaryArgs">The command with arguments.</param>
        /// <returns>the original result.</returns>
        public static double SendExpectDouble(object redisNativeClient, byte[][] cmdWithBinaryArgs)
        {
            if (_sendExpectDouble == null)
            {
                _sendExpectDouble = DynamicMethodBuilder.CreateMethodCallDelegate<Func<object, byte[][], double>>(redisNativeClient.GetType(), "SendExpectDouble");
            }

            return Trace(redisNativeClient, cmdWithBinaryArgs, _sendExpectDouble);
        }

        /// <summary>
        /// Traces SendExpectLong.
        /// </summary>
        /// <param name="redisNativeClient">The redis native client.</param>
        /// <param name="cmdWithBinaryArgs">The command with arguments.</param>
        /// <returns>the original result.</returns>
        public static long SendExpectLong(object redisNativeClient, byte[][] cmdWithBinaryArgs)
        {
            if (_sendExpectLong == null)
            {
                _sendExpectLong = DynamicMethodBuilder.CreateMethodCallDelegate<Func<object, byte[][], long>>(redisNativeClient.GetType(), "SendExpectLong");
            }

            return Trace(redisNativeClient, cmdWithBinaryArgs, _sendExpectLong);
        }

        /// <summary>
        /// Traces SendExpectMultiData.
        /// </summary>
        /// <param name="redisNativeClient">The redis native client.</param>
        /// <param name="cmdWithBinaryArgs">The command with arguments.</param>
        /// <returns>the original result.</returns>
        public static byte[][] SendExpectMultiData(object redisNativeClient, byte[][] cmdWithBinaryArgs)
        {
            if (_sendExpectMultiData == null)
            {
                _sendExpectMultiData = DynamicMethodBuilder.CreateMethodCallDelegate<Func<object, byte[][], byte[][]>>(redisNativeClient.GetType(), "SendExpectMultiData");
            }

            return Trace(redisNativeClient, cmdWithBinaryArgs, _sendExpectMultiData);
        }

        /// <summary>
        /// Traces SendExpectSuccess.
        /// </summary>
        /// <param name="redisNativeClient">The redis native client.</param>
        /// <param name="cmdWithBinaryArgs">The command with arguments.</param>
        public static void SendExpectSuccess(object redisNativeClient, byte[][] cmdWithBinaryArgs)
        {
            if (_sendExpectSuccess == null)
            {
                _sendExpectSuccess = DynamicMethodBuilder.CreateMethodCallDelegate<Action<object, byte[][]>>(redisNativeClient.GetType(), "SendExpectSuccess");
            }

            Trace(redisNativeClient, cmdWithBinaryArgs, _sendExpectSuccess);
        }

        /// <summary>
        /// Traces SendWithoutRead.
        /// </summary>
        /// <param name="redisNativeClient">The redis native client.</param>
        /// <param name="cmdWithBinaryArgs">The command with arguments.</param>
        public static void SendWithoutRead(object redisNativeClient, byte[][] cmdWithBinaryArgs)
        {
            if (_sendWithoutRead == null)
            {
                _sendWithoutRead = DynamicMethodBuilder.CreateMethodCallDelegate<Action<object, byte[][]>>(redisNativeClient.GetType(), "SendWithoutRead");
            }

            Trace(redisNativeClient, cmdWithBinaryArgs, _sendWithoutRead);
        }

        private static int InvokeExample(int x, int y)
        {
            object[] arguments = new object[] { x, y };
            object enter = OnMethodEnter(arguments);
            Exception exception = null;
            object result = null;
            try
            {
                result = 10;
            }
            catch (Exception e)
            {
                exception = e;
            }

            OnMethodExit(enter, ref exception, ref result);
            if (exception != null)
            {
                throw exception;
            }

            return (int)result;
        }

        private static void Trace(dynamic redisNativeClient, byte[][] cmdWithBinaryArgs, Action<object, byte[][]> f)
        {
            using (var scope = Integrations.Redis.CreateScope(GetHost(redisNativeClient), GetPort(redisNativeClient), GetRawCommand(cmdWithBinaryArgs)))
            {
                try
                {
                    f(redisNativeClient, cmdWithBinaryArgs);
                }
                catch (Exception e)
                {
                    scope.Span.SetException(e);
                    throw;
                }
            }
        }

        private static T Trace<T>(dynamic redisNativeClient, byte[][] cmdWithBinaryArgs, Func<object, byte[][], T> f)
        {
            using (var scope = Integrations.Redis.CreateScope(GetHost(redisNativeClient), GetPort(redisNativeClient), GetRawCommand(cmdWithBinaryArgs)))
            {
                try
                {
                    return f(redisNativeClient, cmdWithBinaryArgs);
                }
                catch (Exception e)
                {
                    scope.Span.SetException(e);
                    throw;
                }
            }
        }

        private static string GetHost(dynamic redisNativeClient)
        {
            try
            {
                return redisNativeClient?.Host;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string GetPort(dynamic redisNativeClient)
        {
            try
            {
                return ((int)redisNativeClient?.Port).ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string GetRawCommand(byte[][] cmdWithBinaryArgs)
        {
            return string.Join(" ", cmdWithBinaryArgs.Select(bs =>
            {
                try
                {
                    return Encoding.UTF8.GetString(bs);
                }
                catch
                {
                    return string.Empty;
                }
            }));
        }
    }
}
