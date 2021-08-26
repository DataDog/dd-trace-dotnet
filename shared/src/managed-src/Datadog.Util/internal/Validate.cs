using System;
using System.Runtime.CompilerServices;

namespace Datadog.Util
{
    internal static class Validate
    {
        private const string FallbackParameterName = "specified parameter";

        /// <summary>
        /// Parameter check for Null.
        /// </summary>
        /// <param name="value">Value to be checked.</param>
        /// <param name="name">Name of the parameter being checked.</param>
        /// <exception cref="ArgumentNullException">If the value is null.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void NotNull(object value, string name)
        {
            if (value == null)
            {
                throw new ArgumentNullException(name ?? Validate.FallbackParameterName);
            }
        }

        /// <summary>
        /// String parameter check with a more informative exception that specifies whether
        /// the problem was that the string was null or empty.
        /// </summary>
        /// <param name="value">Value to be checked.</param>
        /// <param name="name">Name of the parameter being checked.</param>
        /// <exception cref="ArgumentNullException">If the value is null.</exception>
        /// <exception cref="ArgumentException">If the value is an empty string.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void NotNullOrEmpty(string value, string name)
        {
            if (value == null)
            {
                throw new ArgumentNullException(name ?? Validate.FallbackParameterName);
            }

            if (value.Length == 0)
            {
                throw new ArgumentException((name ?? Validate.FallbackParameterName) + " may not be empty.");
            }
        }

        /// <summary>
        /// String parameter check with a more informative exception that specifies whether
        /// the problem was that the string was null, empty or whitespace only.
        /// </summary>
        /// <param name="value">Value to be checked.</param>
        /// <param name="name">Name of the parameter being checked.</param>
        /// <exception cref="ArgumentNullException">If the value is null.</exception>
        /// <exception cref="ArgumentException">If the value is an empty string or a string containing whitespaces only;
        /// the message describes which of these two applies.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void NotNullOrWhitespace(string value, string name)
        {
            NotNullOrEmpty(value, name);

            if (String.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException((name ?? Validate.FallbackParameterName) + " may not be whitespace only.");
            }
        }
    }
}
