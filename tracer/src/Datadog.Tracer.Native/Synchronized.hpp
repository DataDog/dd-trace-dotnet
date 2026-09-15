#pragma once
#include <mutex>
#include <optional>

template <class T>
class Synchronized
{
public:
    class Scope
    {
    public:
        Scope(Synchronized& synchronized)
            : _lockGuard(synchronized._mutex), _obj(synchronized._obj)
        {
        }

        T* operator->()
        {
            return &_obj;
        }

        T& Ref()
        {
            return _obj;
        }

    private:
        std::lock_guard<std::mutex> _lockGuard;
        T& _obj;
    };

    Synchronized() : _obj{}
    {
    }

    Scope Get()
    {
        return {*this};
    }

    // Short exclusive copy for readers that must not keep the mutex across a wait.
    T Copy()
    {
        std::lock_guard<std::mutex> lock(_mutex);
        return _obj;
    }

    std::optional<Scope> TryGet()
    {
        try
        {
            return {*this};
        }
        catch (...)
        {
            return std::nullopt;
        }
    }

private:
    T _obj;
    std::mutex _mutex;
};
