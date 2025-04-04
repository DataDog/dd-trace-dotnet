#include "InternedString.h"
#include "StringId.hpp"

// TODO meehhee
InternedString::InternedString() : InternedString("")
{
}

InternedString::InternedString(std::string s) :
    _s{std::move(s)},
    _impl{nullptr}
{
}

InternedString::InternedString(const char* s) :
    InternedString(std::string(s))
{
}

InternedString::~InternedString() = default;
