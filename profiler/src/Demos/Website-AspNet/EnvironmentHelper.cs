using System;

namespace Website_AspNet
{
    public static class EnvironmentHelper
    {
        /// <summary>
        /// Get the number of threads the SelfInvoker class will create to
        /// send requests to the index endpoint
        /// </summary>
        public static int GetParallelism(int defaultValue)
        {
            return GetVariable("DD_APPTEST_FIBO_PARALLELISM", defaultValue);
        }

        /// <summary>
        /// Get the value used by the SelfInvoker while sending requests to the index endpoint.
        /// </summary>
        public static int GetFibonacciInput(int defaultValue)
        {
            return GetVariable("DD_APPTEST_FIBO_INPUT", defaultValue);
        }

        /// <summary>
        /// Get the value of an environment variable named <paramref name="name"/>.
        /// If there is null or empty, return the <paramref name="defaultValue"/>.
        /// Otherwise convert the string to <typeparamref name="T"/> and returns it
        /// </summary>
        /// <typeparam name="T">expected type of the return value</typeparam>
        /// <param name="name">Name of the environment variable</param>
        /// <param name="defaultValue">default value</param>
        /// <returns></returns>
        public static T GetVariable<T>(string name, T defaultValue)
        {
            var valueStr = Environment.GetEnvironmentVariable(name);

            if (string.IsNullOrWhiteSpace(valueStr))
            {
                return defaultValue;
            }

            return (T)Convert.ChangeType(name, typeof(T));
        }
    }
}
