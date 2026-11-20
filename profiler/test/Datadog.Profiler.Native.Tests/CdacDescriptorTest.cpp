// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "CdacDescriptorTypes.h"
#include "CdacTarget.h"
#include "ContractDescriptorParser.h"
#include "EEHeapTestHelpers.h"
#include "LogicalDescriptor.h"

#include <string>
#include <vector>

using namespace cdac;

// --- ContractDescriptorParser (compact format) --------------------------------------------------

TEST(CdacDescriptorParserTest, ParsesTypesGlobalsContractsAndSubDescriptors)
{
    RawContractDescriptor raw;
    raw.PointerSize = 8;
    raw.IsLittleEndian = true;
    raw.PointerData = {0xAAAA, 0xBBBB};
    raw.Json = R"({
        "types": { "TypeA": { "Field1": 4, "Field2": [8, "SomeType"], "!": 16 } },
        "globals": {
            "Direct": 42,
            "Indirect": [0],
            "DirectTyped": [99, "int"],
            "IndirectTyped": [[1], "pointer"]
        },
        "contracts": { "GC": 1, "Loader": 2 },
        "subDescriptors": { "GC": [[0], "pointer"] }
    })";

    ParsedDescriptor parsed = ContractDescriptorParser::Parse(raw);

    // Types: bare-int field, [offset, "type"] field, and the "!" size entry.
    ASSERT_TRUE(parsed.Types.count("TypeA") == 1);
    const TypeInfo& typeA = parsed.Types.at("TypeA");
    ASSERT_TRUE(typeA.Size.has_value());
    EXPECT_EQ(typeA.Size.value(), 16);
    EXPECT_EQ(typeA.Fields.at("Field1").Offset, 4);
    EXPECT_FALSE(typeA.Fields.at("Field1").TypeName.has_value());
    EXPECT_EQ(typeA.Fields.at("Field2").Offset, 8);
    ASSERT_TRUE(typeA.Fields.at("Field2").TypeName.has_value());
    EXPECT_EQ(typeA.Fields.at("Field2").TypeName.value(), "SomeType");

    // The four global shapes.
    EXPECT_EQ(parsed.Globals.at("Direct").NumericValue, 42u);
    EXPECT_EQ(parsed.Globals.at("Indirect").NumericValue, 0xAAAAu);          // [value] -> pointer_data[value]
    EXPECT_EQ(parsed.Globals.at("DirectTyped").NumericValue, 99u);           // [value, "type"]
    EXPECT_EQ(parsed.Globals.at("DirectTyped").TypeName.value(), "int");
    EXPECT_EQ(parsed.Globals.at("IndirectTyped").NumericValue, 0xBBBBu);     // [[index], "type"]
    EXPECT_EQ(parsed.Globals.at("IndirectTyped").TypeName.value(), "pointer");

    // Contracts.
    EXPECT_EQ(parsed.Contracts.at("GC"), "1");
    EXPECT_EQ(parsed.Contracts.at("Loader"), "2");

    // Sub-descriptor slot: resolved against pointer_data ([[index], "type"]).
    ASSERT_EQ(parsed.SubDescriptorSlots.size(), 1u);
    EXPECT_EQ(parsed.SubDescriptorSlots[0].Name, "GC");
    EXPECT_EQ(parsed.SubDescriptorSlots[0].Slot, 0xAAAAu);
}

TEST(CdacDescriptorParserTest, EmptyOrInvalidJsonYieldsEmptyDescriptor)
{
    RawContractDescriptor raw;
    raw.PointerSize = 8;
    raw.Json = "not-json";

    ParsedDescriptor parsed = ContractDescriptorParser::Parse(raw);
    EXPECT_TRUE(parsed.Types.empty());
    EXPECT_TRUE(parsed.Globals.empty());
    EXPECT_TRUE(parsed.Contracts.empty());
    EXPECT_TRUE(parsed.SubDescriptorSlots.empty());
}

// --- LogicalDescriptor::Build (reads a synthetic in-memory blob) ---------------------------------

TEST(CdacLogicalDescriptorTest, BuildReadsRootDescriptorFromMemory)
{
    FakeMemoryReader reader(8);
    const std::string json = R"({
        "types": { "Module": { "ThunkHeap": 16, "!": 24 } },
        "globals": { "G": 7 },
        "contracts": { "Loader": 1 }
    })";
    uintptr_t root = InstallDescriptor(reader, json, /*pointerData*/ {});

    LogicalDescriptor descriptor;
    ASSERT_TRUE(descriptor.Build(reader, root));
    EXPECT_EQ(descriptor.PointerSize, 8);
    EXPECT_TRUE(descriptor.Types.count("Module") == 1);
    EXPECT_EQ(descriptor.Globals.at("G").NumericValue, 7u);
    EXPECT_EQ(descriptor.Contracts.at("Loader"), "1");
}

TEST(CdacLogicalDescriptorTest, BuildFailsWhenMagicIsInvalid)
{
    FakeMemoryReader reader(8);
    uintptr_t root = InstallDescriptor(reader, "{}", {}, 0x100000, 0x200000, 0x300000, /*corruptMagic*/ true);

    LogicalDescriptor descriptor;
    EXPECT_FALSE(descriptor.Build(reader, root));
}

TEST(CdacLogicalDescriptorTest, BuildFailsWhenRootAddressIsUnmapped)
{
    FakeMemoryReader reader(8);
    LogicalDescriptor descriptor;
    EXPECT_FALSE(descriptor.Build(reader, 0xDEADBEEF));
}

TEST(CdacLogicalDescriptorTest, MergesSubDescriptorAndIsCycleSafe)
{
    FakeMemoryReader reader(8);

    // Root descriptor with a "GC" sub-descriptor slot resolved via pointer_data[0].
    const std::string rootJson = R"({
        "globals": { "RootGlobal": 1 },
        "subDescriptors": { "GC": [[0], "pointer"] }
    })";
    const uintptr_t rootSlot = 0x301000; // pointer_data[0] points here; *rootSlot -> sub-descriptor root
    const uintptr_t subRoot = 0x400000;
    uintptr_t root = InstallDescriptor(reader, rootJson, /*pointerData*/ {rootSlot},
                                       0x100000, 0x200000, 0x300000);
    reader.AddPointerAt(rootSlot, subRoot);

    // Sub-descriptor adds a global and points its own sub-descriptor back at the root (a cycle).
    const std::string subJson = R"({
        "globals": { "GCRegion": 42 },
        "subDescriptors": { "Back": [[0], "pointer"] }
    })";
    const uintptr_t subSlot = 0x601000; // sub pointer_data[0]; *subSlot -> root (cycle)
    InstallDescriptor(reader, subJson, /*pointerData*/ {subSlot},
                      subRoot, 0x500000, 0x600000);
    reader.AddPointerAt(subSlot, root);

    LogicalDescriptor descriptor;
    ASSERT_TRUE(descriptor.Build(reader, root));

    // Both root and sub globals are present, and the back-edge to root did not recurse forever.
    EXPECT_EQ(descriptor.Globals.at("RootGlobal").NumericValue, 1u);
    EXPECT_EQ(descriptor.Globals.at("GCRegion").NumericValue, 42u);
    EXPECT_EQ(descriptor.PendingSubDescriptorCount(), 0);
}

// --- Target (globals + type/field offsets over the fake reader) ----------------------------------

TEST(CdacTargetTest, ExposesGlobalsTypesFieldsAndPointerReads)
{
    FakeMemoryReader reader(8);
    const std::string json = R"({
        "types": { "Module": { "ThunkHeap": 16, "LoaderAllocator": [8, "pointer"], "!": 24 } },
        "globals": { "AppDomain": [[0], "pointer"] }
    })";
    uintptr_t root = InstallDescriptor(reader, json, /*pointerData*/ {0x1234});

    LogicalDescriptor descriptor;
    ASSERT_TRUE(descriptor.Build(reader, root));
    Target target(reader, std::move(descriptor));

    EXPECT_EQ(target.PointerSize(), 8);

    // Types / fields.
    EXPECT_TRUE(target.HasType("Module"));
    EXPECT_FALSE(target.HasType("DoesNotExist"));
    EXPECT_TRUE(target.HasField("Module", "ThunkHeap"));
    EXPECT_FALSE(target.HasField("Module", "Missing"));
    EXPECT_EQ(target.GetFieldOffset("Module", "ThunkHeap"), 16);
    EXPECT_EQ(target.GetFieldOffset("Module", "Missing"), -1);

    int size = 0;
    ASSERT_TRUE(target.TryGetTypeSize("Module", size));
    EXPECT_EQ(size, 24);

    // FieldAddress = base + offset (inline-struct distinction).
    const uintptr_t base = 0x800000;
    EXPECT_EQ(target.FieldAddress(base, "Module", "ThunkHeap"), base + 16);
    EXPECT_EQ(target.FieldAddress(base, "Module", "Missing"), 0u);

    // ReadFieldPointer dereferences the pointer stored AT the field address.
    const uintptr_t pointee = 0xCAFEF00D;
    reader.AddPointerAt(base + 8, pointee);
    EXPECT_EQ(target.ReadFieldPointer(base, "Module", "LoaderAllocator"), pointee);

    // Globals.
    EXPECT_TRUE(target.HasGlobal("AppDomain"));
    EXPECT_FALSE(target.HasGlobal("Nope"));
    EXPECT_EQ(target.ReadGlobalPointer("AppDomain"), 0x1234u);
}
