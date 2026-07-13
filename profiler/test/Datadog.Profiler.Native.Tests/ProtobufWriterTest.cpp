// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "ProtobufWriter.h"

#include <cstdint>
#include <vector>

using Bytes = std::vector<uint8_t>;

TEST(ProtobufWriterTest, WriteVarintEncodesSingleByteValues)
{
    Bytes buffer;
    protobuf::WriteVarint(buffer, 0);
    protobuf::WriteVarint(buffer, 1);
    protobuf::WriteVarint(buffer, 127);

    ASSERT_EQ(buffer, (Bytes{0x00, 0x01, 0x7f}));
}

TEST(ProtobufWriterTest, WriteVarintEncodesMultiByteValues)
{
    Bytes buffer;
    protobuf::WriteVarint(buffer, 128);
    ASSERT_EQ(buffer, (Bytes{0x80, 0x01}));

    buffer.clear();
    protobuf::WriteVarint(buffer, 300);
    ASSERT_EQ(buffer, (Bytes{0xac, 0x02}));
}

TEST(ProtobufWriterTest, WriteVarintFieldEmitsTagThenValue)
{
    Bytes buffer;
    protobuf::WriteVarintField(buffer, 1, 1);
    // tag = (1 << 3) | 0 == 0x08
    ASSERT_EQ(buffer, (Bytes{0x08, 0x01}));
}

TEST(ProtobufWriterTest, WriteStringFieldEmitsTagLengthAndBytes)
{
    Bytes buffer;
    protobuf::WriteStringField(buffer, 6, "");
    // tag = (6 << 3) | 2 == 0x32, length 0
    ASSERT_EQ(buffer, (Bytes{0x32, 0x00}));

    buffer.clear();
    protobuf::WriteStringField(buffer, 6, "AB");
    ASSERT_EQ(buffer, (Bytes{0x32, 0x02, 'A', 'B'}));
}

TEST(ProtobufWriterTest, WritePackedVarintsEmitsLengthDelimitedBlock)
{
    Bytes buffer;
    Bytes scratch;
    uint64_t values[] = {1, 2, 300};
    protobuf::WritePackedVarints(buffer, scratch, 2, values, 3);
    // tag = (2 << 3) | 2 == 0x12, length 4, then 01 02 ac 02
    ASSERT_EQ(buffer, (Bytes{0x12, 0x04, 0x01, 0x02, 0xac, 0x02}));
}

TEST(ProtobufWriterTest, WritePackedVarintsEmitsNothingWhenEmpty)
{
    Bytes buffer;
    Bytes scratch;
    protobuf::WritePackedVarints(buffer, scratch, 2, nullptr, 0);
    ASSERT_TRUE(buffer.empty());
}

TEST(ProtobufWriterTest, WriteMessageFieldEmbedsSubMessage)
{
    Bytes child;
    protobuf::WriteVarintField(child, 1, 1); // {0x08, 0x01}

    Bytes buffer;
    protobuf::WriteMessageField(buffer, 3, child);
    // tag = (3 << 3) | 2 == 0x1a, length 2, then child bytes
    ASSERT_EQ(buffer, (Bytes{0x1a, 0x02, 0x08, 0x01}));
}
