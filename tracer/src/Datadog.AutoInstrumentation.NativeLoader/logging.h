#pragma once
#include <iostream>
#include <mutex>

#include "string.h"

template <typename Arg>
std::string LogToString(Arg const& arg)
{
    return ToString(arg);
}

template <typename... Args>
std::string LogToString(Args const&... args)
{
    std::ostringstream oss;
    int a[] = {0, ((void) (oss << LogToString(args)), 0)...};
    return oss.str();
}

template <typename... Args>
void Debug(const Args... args)
{
    // std::cout << "[DBG] : " << LogToString(args...) << std::endl;
}

template <typename... Args>
void Info(const Args... args)
{
    std::cout << "[INF] : " << LogToString(args...) << std::endl;
}

template <typename... Args>
void Warn(const Args... args)
{
    std::cout << "[WRN] : " << LogToString(args...) << std::endl;
}