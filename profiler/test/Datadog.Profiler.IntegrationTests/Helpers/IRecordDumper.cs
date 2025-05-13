// <copyright file="IRecordDumper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace Datadog.Profiler.IntegrationTests
{
    public interface IRecordDumper
    {
        public abstract void DumpRecord(byte[] record, int recordSize);
    }
}
