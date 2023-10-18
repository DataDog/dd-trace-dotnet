#pragma once
#include "error_code.h"

namespace libdatadog {
class Exception : public std::exception
{
public:
    Exception(std::string message);

    char const* what() const noexcept override;

private:
    std::string _message;
};
} // namespace libdatadog
