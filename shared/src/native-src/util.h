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

#include <corhlpr.h>
#include <corprof.h>
#include <functional>
#include <utility>
#include <set>

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



    const ULONG kEnumeratorMax = 256;
    template <typename T>
    class EnumeratorIterator;

    template <typename T>
    class Enumerator {
    private:
        const std::function<HRESULT(HCORENUM*, T[], ULONG, ULONG*)> callback_;
        const std::function<void(HCORENUM)> close_;
        mutable HCORENUM ptr_;

    public:
        Enumerator(std::function<HRESULT(HCORENUM*, T[], ULONG, ULONG*)> callback,
            std::function<void(HCORENUM)> close)
            : callback_(callback), close_(close), ptr_(nullptr) {}

        Enumerator(const Enumerator& other) = default;

        Enumerator& operator=(const Enumerator& other) = default;

        ~Enumerator() { close_(ptr_); }

        EnumeratorIterator<T> begin() const {
            return EnumeratorIterator<T>(this, S_OK);
        }

        EnumeratorIterator<T> end() const {
            return EnumeratorIterator<T>(this, S_FALSE);
        }

        HRESULT Next(T arr[], ULONG max, ULONG* cnt) const {
            return callback_(&ptr_, arr, max, cnt);
        }
    };

    template <typename T>
    class EnumeratorIterator {
    private:
        const Enumerator<T>* enumerator_;
        HRESULT status_ = S_FALSE;
        T arr_[kEnumeratorMax]{};
        ULONG idx_ = 0;
        ULONG sz_ = 0;

    public:
        EnumeratorIterator(const Enumerator<T>* enumerator, HRESULT status)
            : enumerator_(enumerator) {
            if (status == S_OK) {
                status_ = enumerator_->Next(arr_, kEnumeratorMax, &sz_);
                if (status_ == S_OK && sz_ == 0) {
                    status_ = S_FALSE;
                }
            }
            else {
                status_ = status;
            }
        }

        bool operator!=(EnumeratorIterator const& other) const {
            return enumerator_ != other.enumerator_ ||
                (status_ == S_OK) != (other.status_ == S_OK);
        }

        T const& operator*() const { return arr_[idx_]; }

        EnumeratorIterator<T>& operator++() {
            if (idx_ < sz_ - 1) {
                idx_++;
            }
            else {
                idx_ = 0;
                status_ = enumerator_->Next(arr_, kEnumeratorMax, &sz_);
                if (status_ == S_OK && sz_ == 0) {
                    status_ = S_FALSE;
                }
            }
            return *this;
        }
    };



}  // namespace trace

#endif  // DD_CLR_PROFILER_UTIL_H_
