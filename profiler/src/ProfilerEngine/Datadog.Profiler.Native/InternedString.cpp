#include "InternedString.h"
#include "StringId.hpp"

extern "C"
{
#include "datadog/profiling.h"
}

InternedString::InternedString() : InternedString("")
{
}

InternedString::InternedString(std::string s) :
    _impl{std::make_shared<StringId>(StringId{.Str = std::move(s), .Id = {}, .IsInitialized = false})}
{
}

InternedString::InternedString(const char* s) :
    InternedString(std::string(s))
{
}

InternedString::~InternedString() = default;


InternedString::operator std::string_view() const
{
    return _impl->Str;
}

std::shared_ptr<StringId>& InternedString::Id()
{
    return _impl;
}

bool InternedString::operator==(InternedString const& other) const
{
    return _impl->Str == other._impl->Str &&
        ddog_prof_Profile_generations_are_equal(_impl->Id.generation, other._impl->Id.generation);
}
