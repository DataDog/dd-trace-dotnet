#pragma once
#include "RawSample.h"
class RawExceptionSample : public RawSample
{
public:
    std::string ExceptionMessage;
    std::string ExceptionType;
};
