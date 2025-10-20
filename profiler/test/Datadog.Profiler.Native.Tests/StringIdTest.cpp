#include "gtest/gtest.h"
#include "StringId.h"

#include "FfiHelper.h"

extern "C" {
    #include "datadog/common.h"
    #include "datadog/profiling.h"
}
namespace libdatadog {

TEST(StringIdTest, MustBeInvalidWhenConstructed)
{
    StringId id;
    ASSERT_FALSE(id);
}

TEST(StringIdTest, MustBeEqualWhenConstructedWithTheSameString)
{
    ddog_prof_ProfilesDictionaryHandle dictionary;
    auto status = ddog_prof_ProfilesDictionary_new(&dictionary);
    ASSERT_EQ(status.flags, 0);

    StringId id1;
    status = ddog_prof_ProfilesDictionary_insert_str(reinterpret_cast<ddog_prof_StringId*>(&id1), dictionary, libdatadog::to_char_slice("test"), DDOG_PROF_UTF8_OPTION_VALIDATE);
    ASSERT_EQ(status.flags, 0);

    ASSERT_TRUE(id1);

    ddog_prof_ProfilesDictionary_drop(&dictionary);
}

} // namespace libdatadog