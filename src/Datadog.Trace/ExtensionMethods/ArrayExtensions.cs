using System;

namespace Datadog.Trace.ExtensionMethods
{
    internal static class ArrayExtensions
    {
        public static T[] Concat<T>(this T[] array, params T[] newElements)
        {
            var destination = new T[array.Length + newElements.Length];

            Array.Copy(array, 0, destination, 0, array.Length);
            Array.Copy(newElements, 0, destination, array.Length, destination.Length);

            return destination;
        }
    }
}
