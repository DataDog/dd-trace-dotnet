#include "pch.h"

#include "../../../shared/src/native-src/util.h"

using namespace shared;

// === Helper to compare arrays ===
template <typename T>
void assertArrayEquals(const T* arr, const T* expected, size_t count)
{
    for (size_t i = 0; i < count; i++)
    {
        ASSERT_EQ(expected[i], arr[i]);
    }
}

// === Test cases ===
TEST(UtilTests, ArrayInsert_Middle)
{
    int arr[10] = {1, 2, 3, 4, 5};
    size_t count = 5;

    Insert(arr, count, 2, 99);
    int expected[] = {1, 2, 99, 3, 4, 5};

    ASSERT_EQ(6, count);
    assertArrayEquals(arr, expected, count);
}

TEST(UtilTests, ArrayInsert_Beginning)
{
    int arr[10] = {10, 20, 30};
    size_t count = 3;

    Insert(arr, count, 0, 5);
    int expected[] = {5, 10, 20, 30};

    ASSERT_EQ(4, count);
    assertArrayEquals(arr, expected, count);
}

TEST(UtilTests, ArrayInsert_End)
{
    int arr[10] = {7, 8, 9};
    size_t count = 3;

    Insert(arr, count, 3, 99);
    int expected[] = {7, 8, 9, 99};

    ASSERT_EQ(4, count);
    assertArrayEquals(arr, expected, count);
}

TEST(UtilTests, ArrayInsert_OutOfRange)
{
    int arr[5] = {1, 2, 3};
    size_t count = 3;

    bool thrown = false;
    try
    {
        Insert(arr, count, 5, 99); // invalid position
    }
    catch (const std::out_of_range&)
    {
        thrown = true;
    }
    ASSERT_TRUE(thrown);
}

TEST(UtilTests, ArrayInsert_FullArray)
{
    int arr[3] = {1, 2, 3};
    size_t count = 3;

    bool thrown = false;
    try
    {
        Insert(arr, count, 1, 99); // array is already full
    }
    catch (const std::out_of_range&)
    {
        thrown = true;
    }
    ASSERT_TRUE(thrown);
}