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

// Root event structures
enum class GCRootKind : uint8_t {
    Stack = 0,
    Finalizer = 1,
    Handle = 2,
    Other = 3,  // defined as "Older" in ClrEtwAll.man

    // These values do no seem to be used in the CLR...
    SizedRef = 4,
    Overflow = 5,
    DependentHandle = 6,
    NewFQ = 7,
    Steal = 8,
    BGC = 9
};

enum class GCRootFlags : uint32_t {
    None = 0,
    Pinning = 1,
    WeakRef = 2,
    Interior = 4,
    RefCounted = 8
};

struct GCBulkRootEdgeValue
{
    uintptr_t RootedNodeAddress;    // Address of the rooted object
    GCRootKind Kind;                // Type of root
    GCRootFlags Flags;              // Additional flags
    uintptr_t GCRootID;             // Unique ID for this root
};

struct GCBulkRootEdgePayload
{
    uint32_t Index;
    uint32_t Count;
    uint16_t ClrInstanceID;

    // this is followed by an array of Count GCBulkRootEdgeValue structures
};

struct GCBulkRootStaticVarValue
{
    uint64_t GCRootID;              // Root identifier
    uint64_t ObjectID;              // Address of the object
    uint64_t TypeID;                // Type of the static variable
    uint32_t Flags;                 // Flags = 1 if ThreadStatic

    // this is followed by the name of the static field as a null-terminated UTF-16 string
    // TODO: need to take it into account when parsing the payload before moving to the next root
};

struct GCBulkRootStaticVarPayload
{
    uint32_t Count;
    uint64_t AppDomainID;
    uint16_t ClrInstanceID;

    // this is followed by an array of Count GCBulkRootStaticVarValue structures
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

    virtual void OnBulkRootEdges(
        uint32_t Index,
        uint32_t Count,
        GCBulkRootEdgeValue* pRoots) = 0;

    virtual void OnBulkRootStaticVar(
        const GCBulkRootStaticVarValue& root) = 0;

    virtual ~IGCDumpListener() = default;
};