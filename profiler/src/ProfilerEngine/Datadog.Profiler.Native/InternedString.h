#pragma once

#include <iostream>
#include <memory>
#include <string>

struct StringId;

// rename InternableString
class InternedString final
{
public:
    // InternableString ??
    InternedString();
    InternedString(std::string s);
    InternedString(const char* s);
    ~InternedString();

    operator std::string_view() const;

    std::shared_ptr<StringId>& Id();
    bool empty() const;

    bool operator==(InternedString const& other) const;
    bool operator!=(InternedString const& other) const;

private:
    friend std::ostream& operator<<(std::ostream& os, const InternedString& s);
    std::shared_ptr<StringId> _impl;
};