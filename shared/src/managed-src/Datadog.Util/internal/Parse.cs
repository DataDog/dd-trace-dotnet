using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Datadog.Util
{
    internal static class Parse
    {
        public static bool TryBoolean(string stringToParse, bool defaultValue, out bool parsedValue)
        {
            bool canParse = TryBoolean(stringToParse, out parsedValue);
            if (!canParse)
            {
                parsedValue = defaultValue;
            }

            return canParse;
        }

        public static bool TryBoolean(string stringToParse, out bool parsedValue)
        {
            if (stringToParse != null)
            {
                stringToParse = stringToParse.Trim();

                if (stringToParse.Equals("false", StringComparison.OrdinalIgnoreCase)
                        || stringToParse.Equals("no", StringComparison.OrdinalIgnoreCase)
                        || stringToParse.Equals("n", StringComparison.OrdinalIgnoreCase)
                        || stringToParse.Equals("f", StringComparison.OrdinalIgnoreCase)
                        || stringToParse.Equals("0", StringComparison.OrdinalIgnoreCase))
                {
                    parsedValue = false;
                    return true;
                }

                if (stringToParse.Equals("true", StringComparison.OrdinalIgnoreCase)
                        || stringToParse.Equals("yes", StringComparison.OrdinalIgnoreCase)
                        || stringToParse.Equals("y", StringComparison.OrdinalIgnoreCase)
                        || stringToParse.Equals("t", StringComparison.OrdinalIgnoreCase)
                        || stringToParse.Equals("1", StringComparison.OrdinalIgnoreCase))
                {
                    parsedValue = true;
                    return true;
                }
            }

            parsedValue = default(bool);
            return false;
        }


        public static bool TryInt32(string stringToParse, int defaultValue, out int parsedValue)
        {
            bool canParse = TryInt32(stringToParse, out parsedValue);
            if (!canParse)
            {
                parsedValue = defaultValue;
            }

            return canParse;
        }

        public static bool TryInt32(string stringToParse, out int parsedValue)
        {
            if (stringToParse != null)
            {
                return Int32.TryParse(stringToParse, out parsedValue);
            }
            else
            {
                parsedValue = default(int);
                return false;
            }
        }
    }
}
