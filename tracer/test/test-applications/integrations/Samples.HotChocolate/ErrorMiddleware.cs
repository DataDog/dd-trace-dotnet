using System.Collections.Generic;
using HotChocolate;

namespace Samples.HotChocolate
{
    public class ErrorMiddleware : IErrorFilter
    {
        public IError OnError(IError error)
        {
            // Create extensions dictionary with all the test data
            var extensions = new Dictionary<string, object>
            {
                { "int", 1 },
                { "float", 1.1f },
                { "str", "1" },
                { "bool", true },
                { "other", new object[] { 1, "foo" } },
                { "sbyte", (sbyte)-42 },
                { "byte", (byte)42 },
                { "short", (short)-1000 },
                { "ushort", (ushort)1000 },
                { "uint", (uint)4294967295 },
                { "long", (long)-9223372036854775808 },
                { "ulong", (ulong)18446744073709551615 },
                { "decimal", (decimal)3.1415926535897932384626433833 },
                { "double", 3.1415926535897932384626433833 },
                { "char", 'A' },
                { "not_captured", "This should not be captured" }
            };

            // For testing IndexerPathSegment: if this is the throwException error,
            // modify the path so the error is directly at an array index
            // This creates a path ["books", 1] where the error's Path property IS an IndexerPathSegment
            if (error.Path != null && error.Message.Equals("Invalid index Exception", System.StringComparison.OrdinalIgnoreCase))
            {
                var modifiedPath = Path.Root.Append("books").Append(1);
                error = error.WithPath(modifiedPath);
            }

            // Add the extensions to the error
            return error.WithExtensions(extensions);
        }
    }
} 
