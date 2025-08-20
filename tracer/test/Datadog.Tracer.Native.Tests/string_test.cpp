#include "pch.h"

#include "../../../shared/src/native-src/string.h"

using namespace shared;

#define ASSERT_EQ_TOSTR(x) EXPECT_EQ(ToString(WStr(x)), x);
#define ASSERT_EQ_TOWSTR(x) EXPECT_EQ(ToWSTRING(x), WStr(x));

TEST(StringTests, ToString)
{
    ASSERT_EQ_TOSTR("Hello World");
    ASSERT_EQ_TOSTR("C:\\MyPath\\_path1\\a.txt");
    ASSERT_EQ_TOSTR("/MyPath/_path1/a.txt");
    ASSERT_EQ_TOSTR("/my_path/_path1/a.txt");
    ASSERT_EQ_TOSTR("~/my_path/_path1/a.txt");
    ASSERT_EQ_TOSTR("Asterix comics swearing $%&/#@|)(!\"'[]{}");
    ASSERT_EQ_TOSTR("2025-08-19 12:57:49.297 +02:00 [DBG] Logger retrieved for: Datadog.Trace.RemoteConfigurationManagement.RcmSubscriptionManager, Datadog.Trace, Version=3.24.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb  { MachineName: \".\", Process: \"[6520 Samples.Security.AspNetCore5]\", AppDomain: \"[1 Samples.Security.AspNetCore5]\", AssemblyLoadContext: \"\"\" Datadog.Trace.ClrProfiler.Managed.Loader.ManagedProfilerAssemblyLoadContext #2\", TracerVersion: \"3.24.0.0\"}");
    EXPECT_EQ(ToString(WStr("C:\\Users\\用户\\文档\\我的文件.txt")), "C:\\Users\\\xE7\x94\xA8\xE6\x88\xB7\\\xE6\x96\x87\xE6\xA1\xA3\\\xE6\x88\x91\xE7\x9A\x84\xE6\x96\x87\xE4\xBB\xB6.txt");
    EXPECT_EQ(ToString(WStr("~/my_path/_path1/a.txt🙈")), "~/my_path/_path1/a.txt\xF0\x9F\x99\x88");
}

TEST(StringTests, ToWString)
{
    ASSERT_EQ_TOWSTR("Hello World");
    ASSERT_EQ_TOWSTR("C:\\MyPath\\_path1\\a.txt");
    ASSERT_EQ_TOWSTR("/MyPath/_path1/a.txt");
    ASSERT_EQ_TOWSTR("/my_path/_path1/a.txt");
    ASSERT_EQ_TOWSTR("~/my_path/_path1/a.txt");
    ASSERT_EQ_TOWSTR("Asterix comics swearing $%&/#@|)(!\"'[]{}");
    ASSERT_EQ_TOWSTR("2025-08-19 12:57:49.297 +02:00 [DBG] Logger retrieved for: Datadog.Trace.RemoteConfigurationManagement.RcmSubscriptionManager, Datadog.Trace, Version=3.24.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb  { MachineName: \".\", Process: \"[6520 Samples.Security.AspNetCore5]\", AppDomain: \"[1 Samples.Security.AspNetCore5]\", AssemblyLoadContext: \"\"\" Datadog.Trace.ClrProfiler.Managed.Loader.ManagedProfilerAssemblyLoadContext #2\", TracerVersion: \"3.24.0.0\"}");
    EXPECT_EQ(ToWSTRING("C:\\Users\\\xE7\x94\xA8\xE6\x88\xB7\\\xE6\x96\x87\xE6\xA1\xA3\\\xE6\x88\x91\xE7\x9A\x84\xE6\x96\x87\xE4\xBB\xB6.txt"), WStr("C:\\Users\\用户\\文档\\我的文件.txt"));
    EXPECT_EQ(ToWSTRING("~/my_path/_path1/a.txt\xF0\x9F\x99\x88"), WStr("~/my_path/_path1/a.txt🙈"));
}

TEST(StringTests, SurrogatesAndTruncation)
{
    // helper: build WSTRING from raw UTF-16 code units
    auto w16 = [](std::initializer_list<char16_t> u) { return WSTRING((const WCHAR*) u.begin(), u.size()); };

    WSTRING high_only = w16({0xD83D});     // unpaired high
    WSTRING low_only = w16({0xDC00});      // unpaired low
    WSTRING poop = w16({0xD83D, 0xDCA9});  // U+1F4A9
    WSTRING maxcp = w16({0xDBFF, 0xDFFF}); // U+10FFFF
    // (ptr,len) truncated in the middle of surrogate pair
    EXPECT_NO_FATAL_FAILURE((void) ToString((const WCHAR*) poop.data(), 1));
    EXPECT_NO_FATAL_FAILURE((void) ToString((const WCHAR*) high_only.data(), high_only.size()));
    EXPECT_NO_FATAL_FAILURE((void) ToString((const WCHAR*) low_only.data(), low_only.size()));
    // Must not crash; compare against replacement char UTF-8 if your policy does replacement
}

TEST(StringTests, EmbeddedNulls)
{
    const char16_t data[] = u"A\0B🙈C";
    auto w = WSTRING((const WCHAR*) data, std::size(data) - 1); // include the NULs
    auto s = ToString((const WCHAR*) w.data(), w.size());
    // Expect "A\0B\xF0\x9F\x99\x88C" length 6 bytes; ensure no early termination
    EXPECT_EQ(s.size(), 1 + 1 + 1 + 4 + 1);
    EXPECT_EQ(s[0], 'A');
    EXPECT_EQ(s[1], '\0');
    EXPECT_EQ(s[2], 'B');
}

TEST(StringTests, LargeStrings)
{
    std::u16string u(300000, u'α'); // BMP non-ASCII
    u.append({0xD83D, 0xDE80});     // 🚀 sprinkled
    WSTRING w((const WCHAR*) u.data(), u.size());
    auto s = ToString((const WCHAR*) w.data(), w.size());
    EXPECT_GT(s.size(), 300000); // sanity; should not crash
}

TEST(StringTests, Controls) 
{
    const char* utf8 = "A\xE2\x80\x8E" "B"; // A LRM B
    EXPECT_EQ(ToString(ToWSTRING(utf8)), utf8);
}

TEST(StringTests, InvalidUtf8ToWString)
{
    EXPECT_NO_FATAL_FAILURE((void) ToWSTRING(std::string("\x80", 1)));
    EXPECT_NO_FATAL_FAILURE((void) ToWSTRING(std::string("\xC0\xAF", 2)));
    EXPECT_NO_FATAL_FAILURE((void) ToWSTRING(std::string("\xF0\x9F\x92", 3)));
}

TEST(StringTests, NullAndEmpty)
{
    EXPECT_EQ(ToString((const WCHAR*) nullptr), "");
    WSTRING empty;
    EXPECT_EQ(ToString(empty), "");
}
