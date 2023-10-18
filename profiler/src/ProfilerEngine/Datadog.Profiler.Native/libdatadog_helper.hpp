#pragma
#include <memory>

namespace libdatadog {
template <typename T>
std::unique_ptr<typename T::type, typename T::deleter> make_unique(typename T::pointer arg)
{
    return std::unique_ptr<typename T::type, typename T::deleter>(arg);
}

template <typename T, typename... Args>
std::unique_ptr<typename T::type, typename T::deleter> make_unique(Args... args)
{
    return std::make_unique<typename T::type, typename T::deleter>(std::forward(args)...);
}

} // namespace libdatadog