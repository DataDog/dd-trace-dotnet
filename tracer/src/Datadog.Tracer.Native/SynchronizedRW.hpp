#pragma once
#include <mutex>
#include <shared_mutex>
#include <optional>

// Template class that provides synchronized access with reader-writer lock.
template <class T>
class SynchronizedRW
{
public:
    // ConstReadScope: provides const (read-only) access.
    class ConstReadScope
    {
    public:
        // Constructor that takes an already acquired shared lock and a const reference to the object.
        ConstReadScope(std::shared_lock<std::shared_mutex>&& lock, const T& obj)
            : _lock(std::move(lock)), _obj(obj)
        {
        }

        // Allows member access to the underlying object in a read-only manner.
        const T* operator->() const
        {
            return &_obj;
        }

        // Returns a const reference to the underlying object.
        const T& Ref() const
        {
            return _obj;
        }

    private:
        std::shared_lock<std::shared_mutex> _lock; // Holds the shared lock.
        const T& _obj; // Const reference to the protected object.
    };

    // WriteScope: provides exclusive (write) access.
    class WriteScope
    {
    public:
        // Constructor that takes an already acquired unique lock and a reference to the object.
        WriteScope(std::unique_lock<std::shared_mutex>&& lock, T& obj)
            : _lock(std::move(lock)), _obj(obj)
        {
        }

        // Allows member access to the underlying object.
        T* operator->()
        {
            return &_obj;
        }

        // Returns a reference to the underlying object.
        T& Ref()
        {
            return _obj;
        }

    private:
        std::unique_lock<std::shared_mutex> _lock; // Holds the exclusive lock.
        T& _obj; // Reference to the protected object.
    };

    // Constructor that initializes the protected object.
    SynchronizedRW() : _obj{}
    {
    }

    // Const version: Acquires a shared (read) lock and returns a ConstReadScope object.
    ConstReadScope GetRead() const
    {
        std::shared_lock<std::shared_mutex> lock(_mutex); // Acquire shared lock.
        return ConstReadScope(std::move(lock), _obj);
    }

    // Const try-lock variant for read access.
    std::optional<ConstReadScope> TryGetRead() const
    {
        std::shared_lock<std::shared_mutex> lock(_mutex, std::try_to_lock);
        if (!lock.owns_lock())
            return std::nullopt;
        return ConstReadScope(std::move(lock), _obj);
    }

    // Acquires an exclusive (write) lock and returns a WriteScope object.
    WriteScope GetWrite()
    {
        std::unique_lock<std::shared_mutex> lock(_mutex); // Acquire exclusive lock.
        return WriteScope(std::move(lock), _obj);
    }

    // Try-lock variant for write access.
    std::optional<WriteScope> TryGetWrite()
    {
        std::unique_lock<std::shared_mutex> lock(_mutex, std::try_to_lock);
        if (!lock.owns_lock())
            return std::nullopt;
        return WriteScope(std::move(lock), _obj);
    }

private:
    T _obj;                           // The protected resource.
    mutable std::shared_mutex _mutex; // The reader-writer mutex, mutable to allow locking in const methods.
};
