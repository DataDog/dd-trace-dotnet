#include "error_code.h"

#include "libdatadog_details/error_code.hpp"

namespace libdatadog {

error_code::error_code() :
    error_code(nullptr)
{
}

error_code::error_code(error_code&& o) noexcept
{
    *this = std::move(o);
}

error_code& error_code::operator=(error_code&& o) noexcept
{
    if (this != &o)
    {
        _details = std::move(o._details);
    }
    return *this;
}

error_code::error_code(std::unique_ptr<detail::ErrorImpl> details) :
    _details(std::move(details))
{
}

error_code::~error_code() = default;

std::string error_code::message() const noexcept
{
    return _details->message();
}

} // namespace libdatadog
