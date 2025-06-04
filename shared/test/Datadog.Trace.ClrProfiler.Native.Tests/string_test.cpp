#include "gtest/gtest.h"
#include "../../../shared/src/native-src/string.h"
#include "../../../shared/src/native-src/util.h"

using namespace shared;

TEST(string, ToString)
{
    EXPECT_TRUE("Normal String" == ToString(std::string("Normal String")));
    EXPECT_TRUE("\tNormal String\0" == ToString(std::string("\tNormal String\0")));

    EXPECT_TRUE("Char* String" == ToString("Char* String"));
    EXPECT_TRUE("\tChar* String\0" == ToString("\tChar* String\0"));

    EXPECT_TRUE("Wide String" == ToString(WStr("Wide String")));
    EXPECT_TRUE("\tWide String\0" == ToString(WStr("\tWide String\0")));

    EXPECT_TRUE(std::string(1000, 'a') == ToString(WSTRING(1000, L'a')));
    
    EXPECT_TRUE("42" == ToString(42));

#ifndef LINUX
    EXPECT_TRUE("LPTSTR String" == ToString(TEXT("LPTSTR String")));
    EXPECT_TRUE("\tLPTSTR String\0" == ToString(TEXT("\tLPTSTR String\0")));
#endif

    const GUID guid01 = {0x846f5f1c, 0xf9ae, 0x4b07, {0x96, 0x9e, 0x5, 0xc2, 0x6b, 0xc0, 0x60, 0xd8}};
    const GUID guid02 = {0xBD1A650D, 0xAC5D, 0x4896, {0xB6, 0x4F, 0xD6, 0xFA, 0x25, 0xD6, 0xB2, 0x6A}};
    EXPECT_TRUE("{846F5F1C-F9AE-4B07-969E-05C26BC060D8}" == shared::ToString(guid01));
    EXPECT_TRUE("{BD1A650D-AC5D-4896-B64F-D6FA25D6B26A}" == shared::ToString(guid02));
}

TEST(string, ToWSTRING)
{
    EXPECT_TRUE(WStr("Normal String") == ToWSTRING(std::string("Normal String")));
    EXPECT_TRUE(WStr("\tNormal String\0") == ToWSTRING(std::string("\tNormal String\0")));
    EXPECT_TRUE(WSTRING(1000, 'a') == ToWSTRING(std::string(1000, 'a')));

    EXPECT_EQ(WStr("42"), ToWSTRING(42));
}

TEST(string, Trim)
{
    EXPECT_TRUE(WStr("WideString") == Trim(WStr("               WideString")));
    EXPECT_TRUE(WStr("WideString") == Trim(WStr("WideString               ")));
    EXPECT_TRUE(WStr("WideString") == Trim(WStr("               WideString               ")));

    EXPECT_TRUE(WStr("Wide String") == Trim(WStr("               Wide String")));
    EXPECT_TRUE(WStr("Wide String") == Trim(WStr("Wide String               ")));
    EXPECT_TRUE(WStr("Wide String") == Trim(WStr("               Wide String               ")));

    EXPECT_TRUE(WStr ("Wide String") == Trim(WStr(" Wide String \n ")));

    //

    EXPECT_TRUE("NormalString" == Trim("               NormalString"));
    EXPECT_TRUE("NormalString" == Trim("NormalString               "));
    EXPECT_TRUE("NormalString" == Trim("               NormalString               "));

    EXPECT_TRUE("Normal String" == Trim("               Normal String"));
    EXPECT_TRUE("Normal String" == Trim("Normal String               "));
    EXPECT_TRUE("Normal String" == Trim("               Normal String               "));

    EXPECT_TRUE("Normal String" == Trim(" Normal String \n "));
}

TEST(string, Split)
{
    std::vector<std::string> res = Split("A;B;C", ';');
    EXPECT_EQ(3, res.size());
    EXPECT_TRUE("A" == res[0]);
    EXPECT_TRUE("B" == res[1]);
    EXPECT_TRUE("C" == res[2]);

    res = Split(" A ; B ; C ", ';');
    EXPECT_EQ(3, res.size());
    EXPECT_TRUE(" A " == res[0]);
    EXPECT_TRUE(" B " == res[1]);
    EXPECT_TRUE(" C " == res[2]);


    std::vector<WSTRING> wres = Split(WStr("A;B;C"), ';');
    EXPECT_EQ(3, wres.size());
    EXPECT_TRUE(WStr("A") == wres[0]);
    EXPECT_TRUE(WStr("B") == wres[1]);
    EXPECT_TRUE(WStr("C") == wres[2]);

    wres = Split(WStr(" A ; B ; C "), ';');
    EXPECT_EQ(3, wres.size());
    EXPECT_TRUE(WStr(" A ") == wres[0]);
    EXPECT_TRUE(WStr(" B ") == wres[1]);
    EXPECT_TRUE(WStr(" C ") == wres[2]);
}

TEST(string, PadLeft)
{
    EXPECT_TRUE("   PadLeft" == PadLeft("PadLeft", 10));
    EXPECT_TRUE("PadLeft" == PadLeft("PadLeft", 5));
    EXPECT_TRUE("0000000A" == PadLeft("A", 8, '0'));
}

TEST(string, Hex)
{
    EXPECT_TRUE("0x00000000" == Hex(0));
    EXPECT_TRUE("0x0000000A" == Hex(10));
    EXPECT_TRUE("0xFFFFFFFF" == Hex(-1));
    EXPECT_TRUE("0x00000001" == Hex(S_FALSE));
    EXPECT_TRUE("0x80004005" == Hex(E_FAIL));
}
