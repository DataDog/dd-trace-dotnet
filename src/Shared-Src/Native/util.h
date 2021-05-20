#ifndef DD_CLR_PROFILER_UTIL_H_
#define DD_CLR_PROFILER_UTIL_H_

#include <algorithm>
#include <condition_variable>
#include <mutex>
#include <queue>
#include <sstream>
#include <string>
#include <thread>
#include <vector>

#include "string.h"

namespace shared
{
    template <typename Out>
    void Split(const WSTRING& s, wchar_t delim, Out result);

    // Split splits a string by the given delimiter.
    std::vector<WSTRING> Split(const WSTRING& s, wchar_t delim);

    // Trim removes space from the beginning and end of a string.
    WSTRING Trim(const WSTRING& str);

    // GetEnvironmentValue returns the environment variable value for the given
    // name. Space is trimmed.
    WSTRING GetEnvironmentValue(const WSTRING& name);

    // GetEnvironmentValues returns environment variable values for the given name
    // split by the delimiter. Space is trimmed and empty values are ignored.
    std::vector<WSTRING> GetEnvironmentValues(const WSTRING& name,
        const wchar_t delim);

    // GetEnvironmentValues calls GetEnvironmentValues with a semicolon delimiter.
    std::vector<WSTRING> GetEnvironmentValues(const WSTRING& name);


    constexpr char HexMap[] = { '0', '1', '2', '3', '4', '5', '6', '7',
                               '8', '9', 'a', 'b', 'c', 'd', 'e', 'f' };

    // Convert Hex to string
    template <typename T>
    std::string HexStr(const T value)
    {
        const unsigned char* data = (unsigned char*)&value;
        int len = sizeof(T);
        std::string s(len * 2, ' ');
        for (int i = 0; i < len; i++)
        {
            s[(2 * (len - i)) - 2] = HexMap[(data[i] & 0xF0) >> 4];
            s[(2 * (len - i)) - 1] = HexMap[data[i] & 0x0F];
        }
        return s;
    }

    template <typename T>
    WSTRING WHexStr(const T value)
    {
        const unsigned char* data = (unsigned char*)&value;
        int len = sizeof(T);
        WSTRING s(len * 2, ' ');
        for (int i = 0; i < len; i++)
        {
            s[(2 * (len - i)) - 2] = HexMap[(data[i] & 0xF0) >> 4];
            s[(2 * (len - i)) - 1] = HexMap[data[i] & 0x0F];
        }
        return s;
    }

    WSTRING WHexStr(const void* pData, int len);

    template <class Container>
    bool Contains(const Container& items, const typename Container::value_type& value)
    {
        return std::find(items.begin(), items.end(), value) != items.end();
    }

    // Singleton definition
    class UnCopyable
    {
    protected:
        UnCopyable() {};
        ~UnCopyable() {};

    private:
        UnCopyable(const UnCopyable&) = delete;
        UnCopyable(const UnCopyable&&) = delete;
        UnCopyable& operator=(const UnCopyable&) = delete;
        UnCopyable& operator=(const UnCopyable&&) = delete;
    };

    template <typename T>
    class Singleton : public UnCopyable
    {
    public:
        static T* Instance()
        {
            static T instance_obj;
            return &instance_obj;
        }
    };

    template <typename T>
    class BlockingQueue : public UnCopyable
    {
    private:
        std::queue<T> queue_;
        mutable std::mutex mutex_;
        std::condition_variable condition_;

    public:
        T pop()
        {
            std::unique_lock<std::mutex> mlock(mutex_);
            while (queue_.empty())
            {
                condition_.wait(mlock);
            }
            T value = queue_.front();
            queue_.pop();
            return value;
        }
        void push(const T& item)
        {
            {
                std::lock_guard<std::mutex> guard(mutex_);
                queue_.push(item);
            }
            condition_.notify_one();
        }
    };

}  // namespace trace

#endif  // DD_CLR_PROFILER_UTIL_H_
