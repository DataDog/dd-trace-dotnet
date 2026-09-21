#include "pch.h"

#include "../../src/Datadog.Tracer.Native/runtime_async.h"

#include <vector>

using namespace trace;

namespace
{
// Arbitrary, distinguishable tokens. ParseTaskLikeReturnShape does not resolve them, it only has
// to round-trip them out of the compressed encoding.
const mdToken kOuterTypeToken = TokenFromRid(7, mdtTypeRef);
const mdToken kInnerTypeToken = TokenFromRid(9, mdtTypeRef);

class SigBuilder
{
public:
    SigBuilder& Byte(COR_SIGNATURE b)
    {
        _bytes.push_back(b);
        return *this;
    }

    SigBuilder& Token(mdToken token)
    {
        COR_SIGNATURE buffer[8]{};
        const auto written = CorSigCompressToken(token, buffer);
        for (ULONG i = 0; i < written; i++)
        {
            _bytes.push_back(buffer[i]);
        }
        return *this;
    }

    // Emits `count` filler bytes, so tests can place the return type somewhere other than offset 0
    // and prove the offsets we hand back are relative to pbBase rather than to the return type.
    SigBuilder& Filler(size_t count)
    {
        for (size_t i = 0; i < count; i++)
        {
            _bytes.push_back(0xEE);
        }
        return *this;
    }

    const std::vector<COR_SIGNATURE>& Bytes() const
    {
        return _bytes;
    }

private:
    std::vector<COR_SIGNATURE> _bytes;
};

// Builds a TypeSignature covering everything from `offset` to the end of the blob.
TypeSignature Sig(const std::vector<COR_SIGNATURE>& bytes, ULONG offset = 0)
{
    return TypeSignature{offset, static_cast<ULONG>(bytes.size()) - offset, bytes.data()};
}
} // namespace

TEST(RuntimeAsyncTest, IsMiAsyncMatchesTheAsyncBit)
{
    EXPECT_EQ(0x2000u, static_cast<unsigned>(miAsync));

    EXPECT_TRUE(IsMiAsync(miAsync));
    EXPECT_TRUE(IsMiAsync(miAsync | miNoInlining));

    EXPECT_FALSE(IsMiAsync(0));
    EXPECT_FALSE(IsMiAsync(miIL | miManaged));
    EXPECT_FALSE(IsMiAsync(miInternalCall));
    // miUserMask predates .NET 11 and does not cover the async bit
    EXPECT_FALSE(IsMiAsync(miUserMask));
}

TEST(RuntimeAsyncTest, ParsesNonGenericTaskShape)
{
    // Task
    const auto bytes = SigBuilder().Byte(ELEMENT_TYPE_CLASS).Token(kOuterTypeToken).Bytes();

    mdToken openTypeToken = mdTokenNil;
    bool isGenericInst = true;
    bool isValueTypeShape = true;
    TypeSignature typeArg{};

    EXPECT_EQ(S_OK, ParseTaskLikeReturnShape(Sig(bytes), openTypeToken, isGenericInst, isValueTypeShape, typeArg));
    EXPECT_EQ(kOuterTypeToken, openTypeToken);
    EXPECT_FALSE(isGenericInst);
    EXPECT_FALSE(isValueTypeShape);
}

TEST(RuntimeAsyncTest, ParsesNonGenericValueTaskShape)
{
    // ValueTask - a struct, so ELEMENT_TYPE_VALUETYPE. This is why the caller must discriminate on
    // the resolved type name rather than on the element type.
    const auto bytes = SigBuilder().Byte(ELEMENT_TYPE_VALUETYPE).Token(kOuterTypeToken).Bytes();

    mdToken openTypeToken = mdTokenNil;
    bool isGenericInst = true;
    bool isValueTypeShape = false;
    TypeSignature typeArg{};

    EXPECT_EQ(S_OK, ParseTaskLikeReturnShape(Sig(bytes), openTypeToken, isGenericInst, isValueTypeShape, typeArg));
    EXPECT_EQ(kOuterTypeToken, openTypeToken);
    EXPECT_FALSE(isGenericInst);
    EXPECT_TRUE(isValueTypeShape);
}

TEST(RuntimeAsyncTest, ParsesGenericTaskShapeAndSlicesTheTypeArgument)
{
    // Task`1<int>
    const auto bytes = SigBuilder()
                           .Byte(ELEMENT_TYPE_GENERICINST)
                           .Byte(ELEMENT_TYPE_CLASS)
                           .Token(kOuterTypeToken)
                           .Byte(1)
                           .Byte(ELEMENT_TYPE_I4)
                           .Bytes();

    mdToken openTypeToken = mdTokenNil;
    bool isGenericInst = false;
    bool isValueTypeShape = true;
    TypeSignature typeArg{};

    EXPECT_EQ(S_OK, ParseTaskLikeReturnShape(Sig(bytes), openTypeToken, isGenericInst, isValueTypeShape, typeArg));
    EXPECT_EQ(kOuterTypeToken, openTypeToken);
    EXPECT_TRUE(isGenericInst);
    EXPECT_FALSE(isValueTypeShape);

    EXPECT_EQ(bytes.data(), typeArg.pbBase);
    EXPECT_EQ(bytes.size() - 1, typeArg.offset);
    EXPECT_EQ(1u, typeArg.length);
    EXPECT_EQ(ELEMENT_TYPE_I4, typeArg.pbBase[typeArg.offset]);
}

TEST(RuntimeAsyncTest, ParsesGenericValueTaskOverAMethodGenericParameter)
{
    // ValueTask`1<!!0> - generic method parameters are a normal thing to see here
    const auto bytes = SigBuilder()
                           .Byte(ELEMENT_TYPE_GENERICINST)
                           .Byte(ELEMENT_TYPE_VALUETYPE)
                           .Token(kOuterTypeToken)
                           .Byte(1)
                           .Byte(ELEMENT_TYPE_MVAR)
                           .Byte(0)
                           .Bytes();

    mdToken openTypeToken = mdTokenNil;
    bool isGenericInst = false;
    bool isValueTypeShape = false;
    TypeSignature typeArg{};

    EXPECT_EQ(S_OK, ParseTaskLikeReturnShape(Sig(bytes), openTypeToken, isGenericInst, isValueTypeShape, typeArg));
    EXPECT_TRUE(isGenericInst);
    EXPECT_TRUE(isValueTypeShape);
    EXPECT_EQ(2u, typeArg.length);
    EXPECT_EQ(ELEMENT_TYPE_MVAR, typeArg.pbBase[typeArg.offset]);
}

TEST(RuntimeAsyncTest, ParsesNestedGenericTypeArgument)
{
    // Task`1<Task`1<string>> - the type argument is itself a generic instantiation, so the slice
    // length has to come from a real type walk rather than from assuming a single byte.
    const auto bytes = SigBuilder()
                           .Byte(ELEMENT_TYPE_GENERICINST)
                           .Byte(ELEMENT_TYPE_CLASS)
                           .Token(kOuterTypeToken)
                           .Byte(1)
                           .Byte(ELEMENT_TYPE_GENERICINST)
                           .Byte(ELEMENT_TYPE_CLASS)
                           .Token(kInnerTypeToken)
                           .Byte(1)
                           .Byte(ELEMENT_TYPE_STRING)
                           .Bytes();

    mdToken openTypeToken = mdTokenNil;
    bool isGenericInst = false;
    bool isValueTypeShape = true;
    TypeSignature typeArg{};

    EXPECT_EQ(S_OK, ParseTaskLikeReturnShape(Sig(bytes), openTypeToken, isGenericInst, isValueTypeShape, typeArg));
    EXPECT_EQ(kOuterTypeToken, openTypeToken);
    EXPECT_TRUE(isGenericInst);

    // The inner Task`1<string>: GENERICINST CLASS <token> 01 STRING
    const auto innerLength = bytes.size() - typeArg.offset;
    EXPECT_EQ(innerLength, typeArg.length);
    EXPECT_EQ(ELEMENT_TYPE_GENERICINST, typeArg.pbBase[typeArg.offset]);
    EXPECT_EQ(ELEMENT_TYPE_STRING, typeArg.pbBase[typeArg.offset + typeArg.length - 1]);
}

TEST(RuntimeAsyncTest, TypeArgumentOffsetIsRelativeToTheWholeBlob)
{
    // A real return TypeSignature points into the middle of a method signature blob, after the
    // calling convention and parameter count. The slice we hand back must stay relative to pbBase.
    constexpr ULONG fillerLength = 3;
    const auto bytes = SigBuilder()
                           .Filler(fillerLength)
                           .Byte(ELEMENT_TYPE_GENERICINST)
                           .Byte(ELEMENT_TYPE_CLASS)
                           .Token(kOuterTypeToken)
                           .Byte(1)
                           .Byte(ELEMENT_TYPE_I4)
                           .Bytes();

    mdToken openTypeToken = mdTokenNil;
    bool isGenericInst = false;
    bool isValueTypeShape = false;
    TypeSignature typeArg{};

    EXPECT_EQ(S_OK, ParseTaskLikeReturnShape(Sig(bytes, fillerLength), openTypeToken, isGenericInst, isValueTypeShape,
                                             typeArg));
    EXPECT_TRUE(isGenericInst);
    EXPECT_EQ(bytes.data(), typeArg.pbBase);
    EXPECT_EQ(bytes.size() - 1, typeArg.offset);
    EXPECT_EQ(1u, typeArg.length);
    EXPECT_EQ(ELEMENT_TYPE_I4, typeArg.pbBase[typeArg.offset]);
}

TEST(RuntimeAsyncTest, RejectsShapesThatAreNotTaskLike)
{
    // MethodImplAttributes.Async only has an effect on Task/ValueTask returns, but it can be set on
    // a method where it is inert. Those must be declined, not guessed at.
    const std::vector<std::vector<COR_SIGNATURE>> rejected = {
        SigBuilder().Byte(ELEMENT_TYPE_VOID).Bytes(),
        SigBuilder().Byte(ELEMENT_TYPE_I4).Bytes(),
        SigBuilder().Byte(ELEMENT_TYPE_STRING).Bytes(),
        SigBuilder().Byte(ELEMENT_TYPE_OBJECT).Bytes(),
        SigBuilder().Byte(ELEMENT_TYPE_MVAR).Byte(0).Bytes(),
        SigBuilder().Byte(ELEMENT_TYPE_SZARRAY).Byte(ELEMENT_TYPE_I4).Bytes(),
        SigBuilder().Byte(ELEMENT_TYPE_BYREF).Byte(ELEMENT_TYPE_I4).Bytes(),
        SigBuilder().Byte(ELEMENT_TYPE_PTR).Byte(ELEMENT_TYPE_I4).Bytes(),
        // A generic instantiation that is not arity 1 cannot be Task`1 or ValueTask`1
        SigBuilder()
            .Byte(ELEMENT_TYPE_GENERICINST)
            .Byte(ELEMENT_TYPE_CLASS)
            .Token(kOuterTypeToken)
            .Byte(2)
            .Byte(ELEMENT_TYPE_I4)
            .Byte(ELEMENT_TYPE_I4)
            .Bytes(),
        // GENERICINST over something that is neither CLASS nor VALUETYPE
        SigBuilder().Byte(ELEMENT_TYPE_GENERICINST).Byte(ELEMENT_TYPE_I4).Byte(1).Byte(ELEMENT_TYPE_I4).Bytes(),
    };

    for (size_t i = 0; i < rejected.size(); i++)
    {
        mdToken openTypeToken = mdTokenNil;
        bool isGenericInst = false;
        bool isValueTypeShape = false;
        TypeSignature typeArg{};

        EXPECT_EQ(E_FAIL, ParseTaskLikeReturnShape(Sig(rejected[i]), openTypeToken, isGenericInst, isValueTypeShape,
                                                   typeArg))
            << "Signature at index " << i << " should not have been recognised" << std::endl;
    }
}

TEST(RuntimeAsyncTest, RejectsEmptySignatures)
{
    mdToken openTypeToken = mdTokenNil;
    bool isGenericInst = false;
    bool isValueTypeShape = false;
    TypeSignature typeArg{};

    EXPECT_EQ(E_FAIL, ParseTaskLikeReturnShape(TypeSignature{}, openTypeToken, isGenericInst, isValueTypeShape,
                                               typeArg));

    const std::vector<COR_SIGNATURE> bytes = {ELEMENT_TYPE_CLASS};
    EXPECT_EQ(E_FAIL, ParseTaskLikeReturnShape(TypeSignature{0, 0, bytes.data()}, openTypeToken, isGenericInst,
                                               isValueTypeShape, typeArg));
}
