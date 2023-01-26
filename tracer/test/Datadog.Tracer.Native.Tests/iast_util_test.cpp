#include "pch.h"

#include "../../src/Datadog.Tracer.Native/iast/iast_util.h"

using namespace trace;

TEST(IastIntegrationTests, GetVersionInfo)
{
    auto versionInfo = iast::GetVersionInfo("V11.22.33");
    EXPECT_EQ(11, versionInfo.major);
    EXPECT_EQ(22, versionInfo.minor);
    EXPECT_EQ(33, versionInfo.build);
    EXPECT_EQ(0, versionInfo.rev);

    versionInfo = iast::GetVersionInfo("1.2.3");
    EXPECT_EQ(1, versionInfo.major);
    EXPECT_EQ(2, versionInfo.minor);
    EXPECT_EQ(3, versionInfo.build);
    EXPECT_EQ(0, versionInfo.rev);

    versionInfo = iast::GetVersionInfo("1.2.3.4");
    EXPECT_EQ(1, versionInfo.major);
    EXPECT_EQ(2, versionInfo.minor);
    EXPECT_EQ(3, versionInfo.build);
    EXPECT_EQ(4, versionInfo.rev);

    versionInfo = iast::GetVersionInfo("1.2.3.alpha");
    EXPECT_EQ(1, versionInfo.major);
    EXPECT_EQ(2, versionInfo.minor);
    EXPECT_EQ(3, versionInfo.build);
    EXPECT_EQ(0, versionInfo.rev);
}

TEST(IastIntegrationTests, VersionInfo_ToString)
{
    auto versionInfo = iast::GetVersionInfo("V11.22.33");
    EXPECT_EQ(WStr("11.22.33.0"), versionInfo.ToString());
}

TEST(IastIntegrationTests, Match)
{
    EXPECT_EQ(iast::MatchResult::Exact, iast::IsMatch("Match", "Match"));
    EXPECT_EQ(iast::MatchResult::Wildcard, iast::IsMatch("Ma*", "Match"));
    EXPECT_EQ(iast::MatchResult::Wildcard, iast::IsMatch("*ch", "Match"));
    EXPECT_EQ(iast::MatchResult::NoMatch, iast::IsMatch("Matc", "Match"));

    const std::vector<shared::WSTRING> include = 
    {
        WStr("Match11*"),
        WStr("Match2*"),
        WStr("Match3.Match3"),
    };
    EXPECT_EQ(iast::MatchResult::NoMatch, iast::IsMatch(include, WStr("Match")));
    EXPECT_EQ(iast::MatchResult::Wildcard, iast::IsMatch(include, WStr("Match112")));
    EXPECT_EQ(iast::MatchResult::Wildcard, iast::IsMatch(include, WStr("Match20")));
    EXPECT_EQ(iast::MatchResult::Exact, iast::IsMatch(include, WStr("Match3.Match3")));

    const std::vector<shared::WSTRING> exclude = {
        WStr("Match22*"),
        WStr("Match112"),
        WStr("Match3*"),
    };
    EXPECT_TRUE(iast::IsExcluded(include, exclude, WStr("Match112")));
    EXPECT_TRUE(iast::IsExcluded(include, exclude, WStr("Match220")));
    EXPECT_FALSE(iast::IsExcluded(include, exclude, WStr("Match20")));
    EXPECT_FALSE(iast::IsExcluded(include, exclude, WStr("Match210")));
    EXPECT_TRUE(iast::IsExcluded(include, exclude, WStr("Match33.Match3")));
    EXPECT_FALSE(iast::IsExcluded(include, exclude, WStr("Match3.Match3")));
}

TEST(IastIntegrationTests, BoolGuard)
{
    bool guarded = false;
    {
        EXPECT_FALSE(guarded);
        iast::BOOLGUARD(guarded)
        EXPECT_TRUE(guarded);
    }
    EXPECT_FALSE(guarded);
}


enum class TestEnum
{
    NONE,
    SOURCE,
    SINK,
    PROPAGATION
};

BEGIN_ENUM_PARSE(TestEnum)
ENUM_VALUE(TestEnum, NONE)
ENUM_VALUE(TestEnum, SOURCE)
ENUM_VALUE(TestEnum, SINK)
END_ENUM_PARSE(TestEnum)

TEST(IastIntegrationTests, EnumParse)
{
    EXPECT_EQ(TestEnum::NONE, ParseTestEnum("NONE"));
    EXPECT_EQ(TestEnum::SOURCE, ParseTestEnum("SOURCE"));
    EXPECT_EQ(TestEnum::SINK, ParseTestEnum("SINK"));
    EXPECT_EQ((TestEnum)0, ParseTestEnum("OTHER"));
}

TEST(IastIntegrationTests, Enum_ToString)
{
    EXPECT_EQ("NONE", ToString(TestEnum::NONE));
    EXPECT_EQ("SOURCE", ToString(TestEnum::SOURCE));
    EXPECT_EQ("SINK", ToString(TestEnum::SINK));
}


TEST(IastIntegrationTests, Contains)
{
    const std::vector<std::string> stringVector = {"String1", "String2", "String3"};
    EXPECT_TRUE(iast::Contains(stringVector, "String2"));
    EXPECT_FALSE(iast::Contains(stringVector, "String4"));

    const std::set<std::string> stringSet = {"String1", "String2", "String3"};
    EXPECT_TRUE(iast::Contains(stringVector, "String1"));
    EXPECT_FALSE(iast::Contains(stringVector, "String5"));

    const std::unordered_map<std::string, std::string> stringMap = {
        {"String1", "1"}, {"String2", "2"}, {"String3", "3"}};
    EXPECT_TRUE(iast::Contains(stringVector, "String3"));
    EXPECT_FALSE(iast::Contains(stringVector, "String0"));
}

TEST(IastIntegrationTests, Vector_AddRange)
{
    std::vector<std::string> stringVector1 = {"String1", "String2", "String3"};
    std::vector<std::string> stringVector2 = {"String4", "String5"};
    auto added = iast::AddRange(stringVector1, stringVector2);

    EXPECT_EQ(2, added);
    EXPECT_EQ(5, stringVector1.size());
    EXPECT_EQ(stringVector1[3], stringVector2[0]);
    EXPECT_EQ(stringVector1[4], stringVector2[1]);
}

TEST(IastIntegrationTests, Vector_IndexOf)
{
    const std::vector<std::string> stringVector1 = {"String1", "String2", "String3"};
    std::string txt = "String1";
    EXPECT_EQ(0, iast::IndexOf(stringVector1, txt));
    txt = "String2";
    EXPECT_EQ(1, iast::IndexOf(stringVector1, txt));
    txt = "String3";
    EXPECT_EQ(2, iast::IndexOf(stringVector1, txt));
    txt = "String4";
    EXPECT_EQ(-1, iast::IndexOf(stringVector1, txt));
}

TEST(IastIntegrationTests, Set_Add)
{
    std::set<std::string> stringSet1 = {"String1", "String2", "String3"};
    std::set<std::string> stringSet2 = {"String1", "String5"};
    auto addedSet = iast::Add(stringSet1, stringSet2);
    EXPECT_EQ(1, addedSet);
    EXPECT_EQ(4, stringSet1.size());
    EXPECT_TRUE(iast::Contains(stringSet1, "String5"));
}

TEST(IastIntegrationTests, Map_Get)
{
    int i1 = 1;
    int i2 = 2;
    int i3 = 3;
    std::unordered_map<std::string, int*> map1 = {{"String1", &i1}, {"String2", &i2}};

    std::string key = "String2";
    EXPECT_EQ(&i2, iast::Get(map1, key));
    key = "String3";
    EXPECT_EQ(nullptr, iast::Get(map1, key));
    key = "String3";
    std::function<int*()> f = [&i3]() { return &i3; };
    EXPECT_EQ(&i3, iast::Get(map1, key, f));
}

TEST(IastIntegrationTests, String_Split)
{
    auto res1 = iast::Split(WStr("a1,a2,a3"), WStr(","));
    EXPECT_EQ(3, res1.size());
    EXPECT_EQ(WStr("a1"), res1[0]);
    EXPECT_EQ(WStr("a2"), res1[1]);
    EXPECT_EQ(WStr("a3"), res1[2]);

    auto res2 = iast::Split(WStr("b1;b2,b3;b4"), WStr(",;"));
    EXPECT_EQ(4, res2.size());
    EXPECT_EQ(WStr("b1"), res2[0]);
    EXPECT_EQ(WStr("b2"), res2[1]);
    EXPECT_EQ(WStr("b3"), res2[2]);
    EXPECT_EQ(WStr("b4"), res2[3]);
}

TEST(IastIntegrationTests, String_SplitParams)
{
    auto res1 = iast::SplitParams("p0,p1(),\"p2,p2\",<p3,p3,p3>,[p4]>");
    EXPECT_EQ(5, res1.size());
    EXPECT_EQ("p0", res1[0]);
    EXPECT_EQ("p1()", res1[1]);
    EXPECT_EQ("p2,p2", res1[2]);
    EXPECT_EQ("p3,p3,p3", res1[3]);
    EXPECT_EQ("p4", res1[4]);
}

TEST(IastIntegrationTests, String_SplitType)
{
    WSTRING assembliesPart = EmptyWStr;
    WSTRING targetMethodType = EmptyWStr;
    WSTRING targetMethod = EmptyWStr;
    WSTRING targetMethodName = EmptyWStr;
    WSTRING targetMethodParams = EmptyWStr;

    iast::SplitType(WStr("System.String::Concat(System.String,System.String)"), &assembliesPart, &targetMethodType, &targetMethodName, &targetMethodParams);
    EXPECT_EQ(EmptyWStr, assembliesPart);
    EXPECT_EQ(WStr("System.String"), targetMethodType);
    EXPECT_EQ(WStr("Concat"), targetMethodName);
    EXPECT_EQ(WStr("(System.String,System.String)"), targetMethodParams);

    iast::SplitType(WStr("assembly1,assembly2|System.StringBuilder::.ctor(System.String)"), &assembliesPart, &targetMethodType, &targetMethodName, &targetMethodParams);
    EXPECT_EQ(WStr("assembly1,assembly2"), assembliesPart);
    EXPECT_EQ(WStr("System.StringBuilder"), targetMethodType);
    EXPECT_EQ(WStr(".ctor"), targetMethodName);
    EXPECT_EQ(WStr("(System.String)"), targetMethodParams);
}

TEST(IastIntegrationTests, String_Trim)
{
    EXPECT_EQ("Test", iast::Trim("  Test  "));
    EXPECT_EQ("Test", iast::Trim(" *Test* ", " *"));
}

TEST(IastIntegrationTests, String_TrimStart)
{
    EXPECT_EQ("Test  ", iast::TrimStart("  Test  "));
    EXPECT_EQ("Test* ", iast::TrimStart(" *Test* ", " *"));
}

TEST(IastIntegrationTests, String_TrimEnd)
{
    EXPECT_EQ("  Test", iast::TrimEnd("  Test  "));
    EXPECT_EQ(" *Test", iast::TrimEnd(" *Test* ", " *"));
}

TEST(IastIntegrationTests, String_TryParseInt)
{
    int res;
    EXPECT_TRUE(iast::TryParseInt("5", &res));
    EXPECT_EQ(5, res);
    EXPECT_TRUE(iast::TryParseInt("-5", &res));
    EXPECT_EQ(-5, res);
    EXPECT_FALSE(iast::TryParseInt("Five", &res));
}

TEST(IastIntegrationTests, String_ConvertToInt)
{
    EXPECT_EQ(25, iast::ConvertToInt("25"));
    EXPECT_EQ(-5, iast::ConvertToInt("-5"));
    EXPECT_EQ(0, iast::ConvertToInt("Five"));
}

TEST(IastIntegrationTests, String_ConvertToBool)
{
    EXPECT_TRUE(iast::ConvertToBool("True"));
    EXPECT_TRUE(iast::ConvertToBool("true"));
    EXPECT_TRUE(iast::ConvertToBool("1"));
}

TEST(IastIntegrationTests, String_ConvertToIntVector)
{
    auto res = iast::ConvertToIntVector(WStr("0,1,2,33,4,5"));
    EXPECT_EQ(6, res.size());
    EXPECT_EQ(33, res[3]);
}

TEST(IastIntegrationTests, String_ConvertToBoolVector)
{
    auto res = iast::ConvertToBoolVector(WStr("0,1,1,0"));
    EXPECT_EQ(4, res.size());
    EXPECT_FALSE(res[0]);
    EXPECT_TRUE(res[1]);
    EXPECT_TRUE(res[2]);
    EXPECT_FALSE(res[3]);
}

TEST(IastIntegrationTests, String_Join)
{
    std::vector<std::string> vec = {"0", "1", "2", "3"};
    EXPECT_EQ("0,1,2,3", iast::Join(vec, ","));
    EXPECT_EQ("0;1;2;3", iast::Join(vec));
}

TEST(IastIntegrationTests, String_BeginsWith)
{
    EXPECT_TRUE(iast::BeginsWith("BeginsWith", "Begins"));
    EXPECT_FALSE(iast::BeginsWith("BeginsWith", "With"));
}

TEST(IastIntegrationTests, String_EndsWith)
{
    EXPECT_FALSE(iast::EndsWith("EndsWith", "Ends"));
    EXPECT_TRUE(iast::EndsWith("EndsWith", "With"));
}

TEST(IastIntegrationTests, String_ToUpper)
{
    EXPECT_EQ("TO UPPER!!", iast::ToUpper("To upper!!"));
}

TEST(IastIntegrationTests, String_ToLower)
{
    EXPECT_EQ("to lower!!", iast::ToLower("To LoweR!!"));
}

TEST(IastIntegrationTests, String_EqualsIgnoreCase)
{
    EXPECT_TRUE(iast::EqualsIgnoreCase("Equals ignore CASE!!", "equals IGnore CasE!!"));
    EXPECT_FALSE(iast::EqualsIgnoreCase("Equals ignoreCASE!!", "equals IGnore CasE!!"));
}


TEST(IastIntegrationTests, String_IndexOf)
{
    EXPECT_EQ(5, iast::IndexOf("index, of", ","));
}

TEST(IastIntegrationTests, String_LastIndexOf)
{
    EXPECT_EQ(11, iast::LastIndexOf("last, index, of", ","));
}

TEST(IastIntegrationTests, String_Contains)
{
    EXPECT_TRUE(iast::Contains("Contains string", "ns st"));
    EXPECT_FALSE(iast::Contains("Contains string", "contains"));
}

/*
    WSTRING Sanitize(WSTRING s);
    std::size_t IndexOf(const WSTRING& where, const WSTRING& what, std::size_t* offset = nullptr);
    std::size_t IndexOf(const std::string& where, const std::string& what, std::size_t* offset = nullptr);
    std::size_t LastIndexOf(const WSTRING& where, const WSTRING& what, std::size_t* offset = nullptr);
    std::size_t LastIndexOf(const std::string& where, const std::string& what, std::size_t* offset = nullptr);
    bool Contains(const WSTRING& where, const WSTRING& what);
    bool Contains(const std::string& where, const std::string& what);

    // Base64
    std::string ToBase64(const std::string& in);
    std::string FromBase64(const std::string& in);

    HRESULT GuidFromString(const std::string& str, GUID* pUid);

*/