// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#pragma pack(1)

struct GCBulkNodeValue
{
    uintptr_t Address;
    uint64_t Size;
    uint64_t TypeID;
    uint64_t EdgeCount;
};
struct GCBulkNodePayload
{
    uint32_t Index;
    uint32_t Count;
    uint16_t ClrInstanceID;

    // this is followed by an array of Count GCBulkNodeValue structures
};

struct GCBulkEdgeValue
{
    uintptr_t Value;
    uint32_t ReferencingFieldID;
};
struct GCBulkEdgePayload
{
    uint32_t Index;
    uint32_t Count;
    uint16_t ClrInstanceID;

    // this is followed by an array of Count GCBulkEdgeValue structures
};

#pragma pack()


class IGCDumpListener
{
public:
    virtual void OnBulkNodes(
        uint32_t Index,
        uint32_t Count,
        GCBulkNodeValue* pNodes) = 0;

    virtual void OnBulkEdges(
        uint32_t Index,
        uint32_t Count,
        GCBulkEdgeValue* pEdges) = 0;

    virtual ~IGCDumpListener() = default;
};