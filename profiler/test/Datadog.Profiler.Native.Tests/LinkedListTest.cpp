// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

// Our linked list implementation allows the allocator to return nullptr. This is ok in our case
// because, we would use a ringbuffer-based allocator which might return nullptr.
// To avoid UBSAN from failing our test, we deactivate the attribute nonnull-returns found
// in memory_resource file (linux only)
#pragma clang attribute push(__attribute__((no_sanitize("returns-nonnull-attribute"))), apply_to = function)
#include "LinkedList.hpp"
#pragma clang attribute pop

#include "gtest/gtest.h"

#include "shared/src/native-src/dd_memory_resource.hpp"

TEST(LinkedListTest, PushBack)
{
    LinkedList<int> x(shared::pmr::get_default_resource());

    ASSERT_EQ(x.Size(), 0);

    ASSERT_TRUE(x.Append(42));

    ASSERT_EQ(x.Size(), 1);

    ASSERT_EQ(*(x.begin()), 42);
}

TEST(LinkedListTest, ForEach)
{
    LinkedList<std::string> x(shared::pmr::get_default_resource());

    ASSERT_TRUE(x.Append("1"));
    ASSERT_TRUE(x.Append("2"));
    ASSERT_TRUE(x.Append("3"));

    int i = 1;

    for (auto const& s : x)
    {
        ASSERT_EQ(std::to_string(i++), s);
    }

    ASSERT_EQ(x.Size(), 3);
}

struct Person
{
    std::string Name;
    std::uint8_t Age;
};

TEST(LinkedListTest, Move)
{
    LinkedList<Person> l(shared::pmr::get_default_resource());

    l.Append({.Name = "Georges", .Age = 42});
    l.Append({.Name = "Ralph", .Age = 101});
    l.Append({.Name = "jordy", .Age = 21});
    l.Append({.Name = "Bob", .Age = 1});

    ASSERT_EQ(l.Size(), 4);

    LinkedList<Person> other = std::move(l);

    ASSERT_EQ(l.Size(), 0);

    ASSERT_EQ(other.Size(), 4);

    auto it = other.begin();

    ASSERT_EQ((*it).Name, "Georges");
    ASSERT_EQ((*it).Age, 42);

    it++;

    ASSERT_EQ((*it).Name, "Ralph");
    ASSERT_EQ((*it).Age, 101);

    it++;

    ASSERT_EQ((*it).Name, "jordy");
    ASSERT_EQ((*it).Age, 21);

    it++;

    ASSERT_EQ((*it).Name, "Bob");
    ASSERT_EQ((*it).Age, 1);
}

TEST(LinkedListTest, Assign)
{
    LinkedList<Person> l(shared::pmr::get_default_resource());

    l.Append({.Name = "Georges", .Age = 42});
    l.Append({.Name = "Ralph", .Age = 101});
    l.Append({.Name = "jordy", .Age = 21});
    l.Append({.Name = "Bob", .Age = 1});

    ASSERT_EQ(l.Size(), 4);

    LinkedList<Person> other(shared::pmr::get_default_resource());

    other = std::move(l);

    ASSERT_EQ(l.Size(), 0);

    auto begin_it = l.begin();
    auto end_it = l.end();
    ASSERT_EQ(begin_it, end_it);

    // check other now
    ASSERT_EQ(other.Size(), 4);

    auto it = other.begin();

    ASSERT_EQ((*it).Name, "Georges");
    ASSERT_EQ((*it).Age, 42);

    ++it;

    ASSERT_EQ((*it).Name, "Ralph");
    ASSERT_EQ((*it).Age, 101);

    ++it;

    ASSERT_EQ((*it).Name, "jordy");
    ASSERT_EQ((*it).Age, 21);

    ++it;

    ASSERT_EQ((*it).Name, "Bob");
    ASSERT_EQ((*it).Age, 1);
}

TEST(LinkedListTest, ObjectDtorCalled)
{
    // Dummy class is moveable-only.
    // We make sure that we do not set _dtorCalled field to true when destroying temporary instances
    // but only one instance.
    class Dummy
    {
    public:
        Dummy(bool* dtorCalled) :
            _dtorCalled{dtorCalled}
        {
        }

        ~Dummy()
        {
            if (_dtorCalled != nullptr)
                (*_dtorCalled) = true;
        }

        Dummy(Dummy const&) = delete;
        Dummy& operator=(Dummy const&) = delete;

        // We have to write the move ctor and assignement operator to make sure
        // that we set the field on the single instance and not on temporary instances
        Dummy(Dummy&& other) noexcept
        {
            *this = std::move(other);
        }

        Dummy& operator=(Dummy&& other) noexcept
        {
            if (this == &other)
            {
                return *this;
            }

            // temporary instance has _dtorCalled field set to nullptr
            _dtorCalled = std::exchange(other._dtorCalled, nullptr);

            return *this;
        }

    private:
        bool* _dtorCalled;
    };

    bool dtorCalled = false;
    {
        LinkedList<Dummy> l(shared::pmr::get_default_resource());

        l.Append({&dtorCalled});

        ASSERT_EQ(l.Size(), 1);
    }

    ASSERT_TRUE(dtorCalled);
}

TEST(LinkedListTest, CannotAllocate)
{
    class null_memory_resource : public shared::pmr::memory_resource
    {
    private:
        void* do_allocate(size_t _Bytes, size_t _Align) override
        {
            return nullptr;
        }

        void do_deallocate(void* _Ptr, size_t _Bytes, size_t _Align) override
        {
        }

        bool do_is_equal(const memory_resource& _That) const noexcept override
        {
            return false;
        }
    };

    null_memory_resource mr;
    LinkedList<int> l(&mr);

    ASSERT_FALSE(l.Append(42));

    ASSERT_EQ(l.Size(), 0);
}

TEST(LinkedListTest, EnsureMoveAssignementOperatorDoesNotLeakOrLeavesTheCollectionInInconsitentState)
{
    {
        LinkedList<int> l2(shared::pmr::get_default_resource());
        l2.Append(21);

        LinkedList<int> ll(shared::pmr::get_default_resource());
        EXPECT_NO_THROW(ll = std::move(l2));

        ASSERT_EQ(l2.Size(), 0);
        ASSERT_EQ(ll.Size(), 1);
        ASSERT_EQ(21, *ll.begin());
    }

    {
        LinkedList<int> l2(shared::pmr::get_default_resource());

        LinkedList<int> ll(shared::pmr::get_default_resource());
        ll.Append(21);

        EXPECT_NO_THROW(ll = std::move(l2));

        ASSERT_EQ(l2.Size(), 1);
        ASSERT_EQ(ll.Size(), 0);
        ASSERT_EQ(21, *l2.begin());
    }

    {
        LinkedList<int> l2(shared::pmr::get_default_resource());
        LinkedList<int> ll(shared::pmr::get_default_resource());

        EXPECT_NO_THROW(ll = std::move(l2));

        ASSERT_EQ(l2.Size(), 0);
        ASSERT_EQ(ll.Size(), 0);
    }
}

__declspec(noinline) void SwapWithContent(LinkedList<int>& ll)
{
    LinkedList<int> l(shared::pmr::get_default_resource());
    l.Append(41);
    l.Append(43);
    l.Swap(ll);
}

TEST(LinkedListTest, SwapAll)
{
    {
        LinkedList<int> l(shared::pmr::get_default_resource());

        LinkedList<int> ll(shared::pmr::get_default_resource());
        ll.Append(21);

        ll.Swap(l);

        ASSERT_EQ(l.Size(), 1);
        ASSERT_EQ(ll.Size(), 0);
    }
    {
        LinkedList<int> l(shared::pmr::get_default_resource());

        LinkedList<int> ll(shared::pmr::get_default_resource());
        ll.Append(21);

        l.Swap(ll);

        ASSERT_EQ(l.Size(), 1);
        ASSERT_EQ(ll.Size(), 0);
    }
    {
        LinkedList<int> l(shared::pmr::get_default_resource());
        l.Append(1);
        l.Append(2);
        l.Append(3);

        LinkedList<int> ll(shared::pmr::get_default_resource());
        ll.Append(21);

        ll.Swap(l);
        ll.Swap(l);
        ASSERT_EQ(ll.Size(), 1);
        ASSERT_EQ(l.Size(), 3);
    }

    {
        LinkedList<int> l(shared::pmr::get_default_resource());
        SwapWithContent(l);

        ASSERT_EQ(l.Size(), 2);

        l.Append(21);
        ASSERT_EQ(l.Size(), 3);
    }
    {
        LinkedList<double> l(shared::pmr::get_default_resource());
        LinkedList<double> ll(shared::pmr::get_default_resource());

        EXPECT_NO_THROW(l.Swap(ll));
    }
}
