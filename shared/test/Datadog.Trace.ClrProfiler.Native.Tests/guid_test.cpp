#include "gtest/gtest.h"
#include "../../../shared/src/native-src/dd_guid.h"


bool compare_guids(const GUID a, const GUID b)
{
    if (a.Data1 != b.Data1) return false;
    if (a.Data2 != b.Data2) return false;
    if (a.Data3 != b.Data3) return false;
    for (int i = 0; i < 8; i++)
    {
        if (a.Data4[i] != b.Data4[i])
            return false;
    }
    return true;
}

TEST(guid_parse, make_guid)
{
    const GUID guid01Expected = {0x846f5f1c, 0xf9ae, 0x4b07, {0x96, 0x9e, 0x5, 0xc2, 0x6b, 0xc0, 0x60, 0xd8}};
    const GUID guid02Expected = {0xBD1A650D, 0xAC5D, 0x4896, {0xB6, 0x4F, 0xD6, 0xFA, 0x25, 0xD6, 0xB2, 0x6A}};

    // long version
    GUID guid01 = guid_parse::make_guid("{846F5F1C-F9AE-4B07-969E-05C26BC060D8}");
    EXPECT_TRUE(compare_guids(guid01, guid01Expected));

    GUID guid02 = guid_parse::make_guid("{BD1A650D-AC5D-4896-B64F-D6FA25D6B26A}");
    EXPECT_TRUE(compare_guids(guid02, guid02Expected));

    // long version - std::string
    guid01 = guid_parse::make_guid(std::string("{846F5F1C-F9AE-4B07-969E-05C26BC060D8}"));
    EXPECT_TRUE(compare_guids(guid01, guid01Expected));

    guid02 = guid_parse::make_guid(std::string("{BD1A650D-AC5D-4896-B64F-D6FA25D6B26A}"));
    EXPECT_TRUE(compare_guids(guid02, guid02Expected));

    // short version
    guid01 = guid_parse::make_guid("846F5F1C-F9AE-4B07-969E-05C26BC060D8");
    EXPECT_TRUE(compare_guids(guid01, guid01Expected));

    guid02 = guid_parse::make_guid("BD1A650D-AC5D-4896-B64F-D6FA25D6B26A");
    EXPECT_TRUE(compare_guids(guid02, guid02Expected));

    // short version - std::string
    guid01 = guid_parse::make_guid(std::string("846F5F1C-F9AE-4B07-969E-05C26BC060D8"));
    EXPECT_TRUE(compare_guids(guid01, guid01Expected));

    guid02 = guid_parse::make_guid(std::string("BD1A650D-AC5D-4896-B64F-D6FA25D6B26A"));
    EXPECT_TRUE(compare_guids(guid02, guid02Expected));
}

TEST(guid_parse, make_guid_errors)
{
    EXPECT_ANY_THROW(guid_parse::make_guid("BD1A650H-AC5D-4896-B64F-D6FA25D6B26A"));
    EXPECT_ANY_THROW(guid_parse::make_guid(std::string("BD1A650H-AC5D-4896-B64F-D6FA25D6B26A")));
    EXPECT_ANY_THROW(guid_parse::make_guid(std::string("BAD")));
}
