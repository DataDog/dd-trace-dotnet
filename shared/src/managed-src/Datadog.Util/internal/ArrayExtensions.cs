using System;

namespace Datadog.Util
{
    internal static class ArrayExtensions
    {
        /// <summary>
        /// Compare two arrays for equality by making sure each element pair in order are equal in respect to .Equals(..).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="arr1"></param>
        /// <param name="arr2"></param>
        /// <returns></returns>
        public static bool IsEqual<T>(this T[] arr1, T[] arr2) where T : IEquatable<T>
        {
            if (arr1 == arr2)
            {
                return true;
            }

            if (arr1 == null || arr2 == null || arr1.Length != arr2.Length)
            {
                return false;
            }

            for (int i = arr1.Length - 1; i >= 0; i--)
            {
                if (false == arr1[i].Equals(arr2[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
