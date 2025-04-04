#pragma once

#include <memory>
#include <string>

struct StringId;

struct InternedStringView
{
public:
    std::string_view _s;
    std::shared_ptr<StringId> _impl;
    operator std::string_view() const
    {
        return _s;
    }

    bool operator==(InternedStringView other) const
    {
        return _s == other._s;
    }

    

    bool operator!=(InternedStringView other) const
    {
        return _s != other._s;
    }
};

class InternedString
{
public:
    // InternableString ??
    InternedString();
    InternedString(std::string s);
    InternedString(const char* s);
    ~InternedString();

    operator InternedStringView()
    {
        return InternedStringView{._s = _s, ._impl = _impl};
    }

    operator InternedStringView() const
    {
        return InternedStringView{._s = _s, ._impl = _impl};
    }

private:
    std::string _s;
    std::shared_ptr<StringId> _impl;
};