using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Datadog.Util
{
    internal static class Format
    {
        private const string NullWord = "null";

        private const string HundredSpaces = "                                                                                                    ";
        private const string TenSpaces = "          ";
        private const char OneSpace = ' ';

        /// <summary>
        /// Returns either the specified <c>str</c> instrance, or the string <c>"null"</c> if <c>str</c> was <c>null</c>.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string SpellIfNull(string str)
        {
            return str ?? NullWord;
        }

        /// <summary>
        /// Returns either the specified <c>val</c> instrance, or the string <c>"null"</c> if <c>val</c> was <c>null</c>.
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object SpellIfNull(object val)
        {
            return val ?? NullWord;
        }

        /// <summary>
        /// If the specified parameter <c>str</c> is <c>null</c>,
        /// returns the string <c>"null"</c> (the quotes (") are delimeters, not actual string contents).
        /// If the specified parameter <c>str</c> is not <c>null</c>,
        /// returns the a string that contains the specified <c>str</c> value pre-fixed and post-fixed with a quotes (") character.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string QuoteOrSpellNull(string str)
        {
            if (str == null)
            {
                return NullWord;
            }

            var builder = new StringBuilder();
            builder.Append('"');
            builder.Append(str);
            builder.Append('"');

            return builder.ToString();
        }

        public static string QuoteIfString<T>(T val)
        {
            if (val == null)
            {
                return NullWord;
            }

            if (val is string valStr)
            {
                return QuoteOrSpellNull(valStr);
            }

            return val.ToString();
        }

        /// <summary>
        /// Determines whether the specified character is a valid lower-case hex digit.
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLowerHexChar(char c)
        {
            return ('0' <= c && c <= '9') || ('a' <= c && c <= 'f');
        }

        /// <summary>
        /// Converts the specified key-value enumeration to a C#-style text notation.
        /// Null values are spelled as "null", strings are quoted (") other objects are converted to strings and left unquoted.
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="table"></param>
        /// <returns></returns>
        public static IEnumerable<string> AsTextLines<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> table)
        {
            if (table == null)
            {
                yield return NullWord;
                yield break;
            }

            foreach (KeyValuePair<TKey, TValue> row in table)
            {
                string rowStr = $"[{QuoteIfString(row.Key)}] = {QuoteIfString(row.Value)}";
                yield return rowStr;
            }
        }


        /// <summary>
        /// Converts the specified value to a string and shortens it to not exceed the specified length.
        /// If the specified length is 5 or more chars, the shortening occurs by removing characters from
        /// the middle of the string and inserting "...".
        /// Otherwise the string is truncated at the end.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="maxLength"></param>
        /// <param name="trim"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string LimitLength(object value, int maxLength, bool trim)
        {
            string valueStr = value?.ToString();
            return LimitLength(valueStr, maxLength, trim);
        }

        /// <summary>
        /// Converts the specified value to a string and shortens it to not exceed the specified length.
        /// If the specified length is 5 or more chars, the shortening occurs by removing characters from
        /// the middle of the string and inserting "...".
        /// Otherwise the string is truncated at the end.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="maxLength"></param>
        /// <param name="trim"></param>
        /// <returns></returns>
        public static string LimitLength(string value, int maxLength, bool trim)
        {
            if (maxLength < 0)
            {
                throw new ArgumentException($"{nameof(maxLength)} may not be smaller than zero, but it was {maxLength}.");
            }

            const string FillStr = "...";
            int fillStrLen = FillStr.Length;

            value = SpellIfNull(value);
            value = trim ? value.Trim() : value;
            int valueLen = value.Length;

            if (valueLen <= maxLength)
            {
                return value;
            }

            if (maxLength < fillStrLen + 2)
            {
                string superShortResult = value.Substring(0, maxLength);
                return superShortResult;
            }

            int postLen = (maxLength - fillStrLen) / 2;
            int preLen = maxLength - fillStrLen - postLen;

            string postStr = value.Substring(valueLen - postLen, postLen);
            string preStr = value.Substring(0, preLen);

            var shortResult = new StringBuilder(preStr, maxLength);
            shortResult.Append(FillStr);
            shortResult.Append(postStr);

            return shortResult.ToString();
        }

        public static string EnsureMinLength(string str, int minLen)
        {
            if (str == null || str.Length >= minLen)
            {
                return str;
            }

            var s = new StringBuilder(str, minLen);
            while (minLen - s.Length > 100)
            {
                s.Append(HundredSpaces);
            }

            while (minLen - s.Length > 10)
            {
                s.Append(TenSpaces);
            }

            while (minLen > s.Length)
            {
                s.Append(OneSpace);
            }

            return s.ToString();
        }

        public static string AsReadablePreciseUnconverted(DateTimeOffset dto)
        {
            return dto.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF (zzz)");
        }

        public static string AsReadablePreciseUtc(DateTimeOffset dto)
        {
            return dto.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF");
        }

        public static string AsReadablePreciseLocal(DateTimeOffset dto)
        {
            return dto.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF");
        }
    }
}
