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

    EXPECT_TRUE("42" == ToString(42));

    EXPECT_TRUE("LPTSTR String" == ToString(TEXT("LPTSTR String")));
    EXPECT_TRUE("\tLPTSTR String\0" == ToString(TEXT("\tLPTSTR String\0")));
}

TEST(string, ToWSTRING)
{
    EXPECT_TRUE(WStr("Normal String") == ToWSTRING(std::string("Normal String")));
    EXPECT_TRUE(WStr("\tNormal String\0") == ToWSTRING(std::string("\tNormal String\0")));

    EXPECT_TRUE(WStr("42") == ToWSTRING(42));
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