using System;
using System.Collections.Specialized;

namespace TinyGet.Config
{
    public static class StringExtensions
    {
        public static NameValueCollection ToNameValueCollection(this String[] array)
        {
            NameValueCollection result = new NameValueCollection();

            if (null != array)
            {
                foreach (string argument in array)
                {
                    String[] parts = argument.Split(new[] {":"}, StringSplitOptions.RemoveEmptyEntries);
                    if (1 == parts.Length)
                    {
                        result.Add(parts[0], String.Empty);
                    }
                    if (2 == parts.Length)
                    {
                        result.Add(parts[0], parts[1]);
                    }
                }
            }

            return result;
        }
    }
}
