// <copyright file="RecordDumper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Profiler.IntegrationTests
{
    public class RecordDumper : IRecordDumper
    {
        public void DumpRecord(byte[] record, int recordSize)
        {
            //Console.WriteLine($"> record size = {recordSize}");
        }
    }
}
