// <copyright file="DatadogHttpValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
namespace Datadog.Trace.HttpOverStreams
{
    internal static class DatadogHttpValues
    {
        public const char CarriageReturn = '\r';
        public const char LineFeed = '\n';
        public const string CrLf = "\r\n";

        public const string Boundary = "faa0a896-8bc8-48f3-b46d-016f2b15a884";

        public static class MultipartBytes
        {
            // $"{CrLf}--{Boundary}{CrLf}"
            public static byte[] BoundarySeparator { get; } =
            [
                13, 10, 45, 45, 102, 97, 97, 48, 97, 56, 57, 54, 45, 56, 98, 99, 56, 45, 52, 56, 102, 51, 45, 98, 52, 54, 100, 45, 48, 49, 54, 102, 50, 98, 49, 53, 97, 56, 56, 52, 13, 10,
            ];

            // $"{CrLf}--{Boundary}--{CrLf}"
            public static byte[] BoundaryTrailer { get; } =
            [
                13, 10, 45, 45, 102, 97, 97, 48, 97, 56, 57, 54, 45, 56, 98, 99, 56, 45, 52, 56, 102, 51, 45, 98, 52, 54, 100, 45, 48, 49, 54, 102, 50, 98, 49, 53, 97, 56, 56, 52, 45, 45, 13, 10
            ];
        }
    }
}
