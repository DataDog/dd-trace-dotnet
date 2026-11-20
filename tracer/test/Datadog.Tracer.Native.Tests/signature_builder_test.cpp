#include "pch.h"

#include "../../src/Datadog.Tracer.Native/signature_builder.h"

#include <type_traits>
#include <vector>

// SignatureBuilder::STACK_BUFFER_SIZE is private, so mirror it here: the builder starts on an
// inline buffer of this size and moves to the heap once an append no longer fits.
static constexpr size_t kStackBufferSize = 1000;

// The builder holds a pointer into its own inline buffer, so copying or moving one would leave it
// pointing at the original object.
static_assert(!std::is_copy_constructible<SignatureBuilder>::value, "SignatureBuilder must not be copyable");
static_assert(!std::is_copy_assignable<SignatureBuilder>::value, "SignatureBuilder must not be copyable");
static_assert(!std::is_move_constructible<SignatureBuilder>::value, "SignatureBuilder must not be movable");
static_assert(!std::is_move_assignable<SignatureBuilder>::value, "SignatureBuilder must not be movable");

// Appends `count` bytes of a deterministic pattern, continuing from `startValue`.
static std::vector<COR_SIGNATURE> AppendPattern(SignatureBuilder& builder, size_t count, size_t startValue = 0)
{
    std::vector<COR_SIGNATURE> expected;
    expected.reserve(count);

    for (size_t i = 0; i < count; i++)
    {
        const auto value = static_cast<COR_SIGNATURE>((startValue + i) % 256);
        builder.Append(value);
        expected.push_back(value);
    }

    return expected;
}

static void AssertSignatureEquals(const SignatureBuilder& builder, const std::vector<COR_SIGNATURE>& expected)
{
    ASSERT_EQ(expected.size(), builder.Size());

    const auto* signature = builder.GetSignature();
    for (size_t i = 0; i < expected.size(); i++)
    {
        ASSERT_EQ(expected[i], signature[i]) << "signature differs at index " << i;
    }
}

TEST(SignatureBuilderTest, EmptyBuilderHasNoContent)
{
    SignatureBuilder builder;

    ASSERT_EQ(0u, builder.Size());
    ASSERT_NE(nullptr, builder.GetSignature());
}

TEST(SignatureBuilderTest, AppendSingleElement)
{
    SignatureBuilder builder;
    builder.Append(static_cast<COR_SIGNATURE>(IMAGE_CEE_CS_CALLCONV_LOCAL_SIG));

    ASSERT_EQ(1u, builder.Size());
    ASSERT_EQ(IMAGE_CEE_CS_CALLCONV_LOCAL_SIG, builder.GetSignature()[0]);
}

TEST(SignatureBuilderTest, AppendBuffer)
{
    const COR_SIGNATURE elements[] = {0x01, 0x02, 0x03, 0x04};

    SignatureBuilder builder;
    builder.Append(elements, sizeof(elements));

    AssertSignatureEquals(builder, {0x01, 0x02, 0x03, 0x04});
}

TEST(SignatureBuilderTest, AppendBufferOfLengthZeroKeepsContent)
{
    const COR_SIGNATURE elements[] = {0x99};

    SignatureBuilder builder;
    builder.Append(static_cast<COR_SIGNATURE>(0x42));
    builder.Append(elements, 0);

    AssertSignatureEquals(builder, {0x42});
}

TEST(SignatureBuilderTest, MixesSingleAndBufferAppends)
{
    const COR_SIGNATURE elements[] = {0x20, 0x21};

    SignatureBuilder builder;
    builder.Append(static_cast<COR_SIGNATURE>(0x10));
    builder.Append(elements, sizeof(elements));
    builder.Append(static_cast<COR_SIGNATURE>(0x30));

    AssertSignatureEquals(builder, {0x10, 0x20, 0x21, 0x30});
}

TEST(SignatureBuilderTest, StaysOnInlineBufferBelowCapacity)
{
    SignatureBuilder builder;
    const auto* inlineBuffer = builder.GetSignature();

    const auto expected = AppendPattern(builder, kStackBufferSize - 1);

    ASSERT_EQ(inlineBuffer, builder.GetSignature()) << "should not have allocated yet";
    AssertSignatureEquals(builder, expected);
}

TEST(SignatureBuilderTest, MovesToHeapAtCapacity)
{
    SignatureBuilder builder;
    const auto* inlineBuffer = builder.GetSignature();

    // The growth check is `_offset + size >= _length`, so the last inline byte is never used and
    // filling the whole inline buffer already allocates.
    const auto expected = AppendPattern(builder, kStackBufferSize);

    ASSERT_NE(inlineBuffer, builder.GetSignature()) << "should have moved to the heap";
    AssertSignatureEquals(builder, expected);
}

TEST(SignatureBuilderTest, PreservesContentWhenGrowingPastInlineBuffer)
{
    SignatureBuilder builder;
    auto expected = AppendPattern(builder, kStackBufferSize - 1);

    // Cross the boundary one element at a time so the copy happens with a full inline buffer.
    for (size_t i = 0; i < 10; i++)
    {
        const auto value = static_cast<COR_SIGNATURE>(0xA0 + i);
        builder.Append(value);
        expected.push_back(value);
    }

    AssertSignatureEquals(builder, expected);
}

// The new capacity is `max(_length * 2, _offset + size)`. This append needs 3001 bytes while
// doubling would only give 2000, so the requested size wins over doubling.
TEST(SignatureBuilderTest, GrowsForSingleAppendLargerThanInlineBuffer)
{
    std::vector<COR_SIGNATURE> elements;
    for (size_t i = 0; i < kStackBufferSize * 3; i++)
    {
        elements.push_back(static_cast<COR_SIGNATURE>(i % 256));
    }

    SignatureBuilder builder;
    builder.Append(static_cast<COR_SIGNATURE>(0x7F));
    builder.Append(elements.data(), elements.size());

    std::vector<COR_SIGNATURE> expected{0x7F};
    expected.insert(expected.end(), elements.begin(), elements.end());

    AssertSignatureEquals(builder, expected);
}

// Same "requested size wins over doubling" path, but growing from a heap buffer rather than from
// the inline one, so the content is copied heap-to-heap.
TEST(SignatureBuilderTest, GrowsForSingleAppendLargerThanDoubledHeapBuffer)
{
    SignatureBuilder builder;

    // Move off the inline buffer first; capacity is 2000 once this append reallocates.
    auto expected = AppendPattern(builder, kStackBufferSize);

    std::vector<COR_SIGNATURE> elements;
    for (size_t i = 0; i < kStackBufferSize * 5; i++)
    {
        elements.push_back(static_cast<COR_SIGNATURE>((i * 3) % 256));
    }

    builder.Append(elements.data(), elements.size());
    expected.insert(expected.end(), elements.begin(), elements.end());

    ASSERT_EQ(kStackBufferSize * 6, builder.Size());
    AssertSignatureEquals(builder, expected);
}

TEST(SignatureBuilderTest, PreservesContentAcrossRepeatedGrowths)
{
    SignatureBuilder builder;
    std::vector<COR_SIGNATURE> expected;

    // Enough rounds to reallocate several times, covering the heap-to-heap copy.
    for (size_t round = 0; round < 40; round++)
    {
        const auto appended = AppendPattern(builder, 250, expected.size());
        expected.insert(expected.end(), appended.begin(), appended.end());
    }

    ASSERT_EQ(10000u, builder.Size());
    AssertSignatureEquals(builder, expected);
}
