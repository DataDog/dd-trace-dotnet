#include "Exception.h"

namespace libdatadog {
Exception::Exception(std::string message) :
    _message(std::move(message))
{
}
char const* Exception::what() const noexcept
{
    return _message.c_str();
}
} // namespace libdatadog
