// <copyright file="OperationCode.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Couchbase
{
    /// <summary>
    /// Couchbase OperationCode copy
    /// </summary>
    internal enum OperationCode : byte
    {
        Get = 0x00,
        Set = 0x01,
        Add = 0x02,
        Replace = 0x03,
        Delete = 0x04,
        Increment = 0x05,
        Decrement = 0x06,
        Quit = 0x07,
        Flush = 0x08,
        GetQ = 0x09,
        NoOp = 0x0A,
        Version = 0x0B,
        GetK = 0x0C,

        // ReSharper disable once InconsistentNaming
        GetKQ = 0x0D,

        Append = 0x0E,
        Prepend = 0x0F,
        Stat = 0x10,
        SetQ = 0x11,
        AddQ = 0x12,
        ReplaceQ = 0x13,
        DeleteQ = 0x14,
        IncrementQ = 0x15,
        DecrementQ = 0x16,
        QuitQ = 0x17,
        FlushQ = 0x18,
        AppendQ = 0x19,
        PrependQ = 0x1A,

        Touch = 0x1c,
        GAT = 0x1d,
        GATQ = 0x1e,

        // SASL authentication op-codes
        SaslList = 0x20,
        SaslStart = 0x21,
        SaslStep = 0x22,

        // CCCP
        GetClusterConfig = 0xb5,

        // Durability constraints
        Observe = 0x92,

        // couchbase only
        GetL = 0x94,
        Unlock = 0x95,

        // "Dirty" reads
        ReplicaRead = 0x83,

        // used with RBAC to verify credentials with username / password
        SelectBucket = 0x89,

        // request a server error map
        GetErrorMap = 0xfe,

        // Enhanced durability
        ObserveSeqNo = 0x91,

        Hello = 0x1f,

        SubGet = 0xc5,
        SubExist = 0xc6,
        SubDictAdd = 0xc7,
        SubDictUpsert = 0xc8,
        SubDelete = 0xc9,
        SubReplace = 0xca,
        SubArrayPushLast = 0xcb,
        SubArrayPushFirst = 0xcc,
        SubArrayInsert = 0xcd,
        SubArrayAddUnique = 0xce,
        SubCounter = 0xcf,
        MultiLookup = 0xd0,
        SubMultiMutation = 0xd1,
        SubGetCount = 0xd2,
        GetCollectionsManifest = 0xBA,
        GetCidByName = 0xbb,
        GetSidByName = 0xBC,
        GetMeta = 0xa0
    }
}
