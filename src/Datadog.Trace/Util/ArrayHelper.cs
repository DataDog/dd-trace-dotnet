namespace Datadog.Trace.Util
{
    internal static class ArrayHelper
    {
        public static T[] Empty<T>()
        {
#if NET45
            return EmptyArray<T>.Value;
#else
            return System.Array.Empty<T>();
#endif
        }

#if NET45
        private static class EmptyArray<T>
        {
            internal static readonly T[] Value = new T[0];
        }
#endif
    }
}
