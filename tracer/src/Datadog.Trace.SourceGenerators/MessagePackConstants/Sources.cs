// <copyright file="Sources.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Datadog.Trace.SourceGenerators.Helpers;

namespace Datadog.Trace.SourceGenerators.MessagePackConstants
{
    internal static class Sources
    {
        public const string Attribute = Constants.FileHeader +
            @"namespace Datadog.Trace.SourceGenerators;

/// <summary>
/// Used to designate a const string field for pre-serialization to MessagePack bytes.
/// The generator will create a static byte array with the MessagePack-encoded value.
/// Used for source generation.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Field, AllowMultiple = false)]
internal sealed class MessagePackFieldAttribute : System.Attribute
{
}
";

        public static string CreateMessagePackConstants(ImmutableArray<FieldToSerialize> fields)
        {
            var sb = new StringBuilder();
            sb.Append(Constants.FileHeader);
            sb.Append(@"using System;

namespace Datadog.Trace.Agent.MessagePack;

/// <summary>
/// Pre-serialized MessagePack constants generated at compile time.
/// </summary>
internal static class MessagePackConstants
{");

            // Group fields by name to detect duplicates
            var fieldGroups = fields.GroupBy(f => f.FieldName).ToList();

            foreach (var group in fieldGroups)
            {
                if (group.Count() > 1)
                {
                    // Generate comment noting duplicate field names
                    sb.Append(@"
    // Note: Multiple fields with name '")
                      .Append(group.Key)
                      .Append(@"' found. Using first occurrence.");
                }

                var field = group.First();
                var tagByteArray = string.Join(", ", MessagePackHelper.GetValueInRawMessagePackIEnumerable(field.StringValue));

                sb.Append(@"

    // ")
                  .Append(field.FieldName)
                  .Append(@"Bytes = MessagePack.Serialize(""")
                  .Append(field.StringValue)
                  .Append(@""");");

                sb.Append(@"
#if NETCOREAPP
    internal static ReadOnlySpan<byte> ")
                  .Append(field.FieldName)
                  .Append(@"Bytes => new byte[] { ")
                  .Append(tagByteArray)
                  .Append(@" };")
                  .Append(@"
#else
    internal static readonly byte[] ")
                  .Append(field.FieldName)
                  .Append(@"Bytes = new byte[] { ")
                  .Append(tagByteArray)
                  .Append(@" };")
                  .Append(@"
#endif");
            }

            sb.AppendLine(@"
}");

            return sb.ToString();
        }
    }
}
