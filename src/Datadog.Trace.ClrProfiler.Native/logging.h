#ifndef DD_CLR_PROFILER_LOGGING_H_
#define DD_CLR_PROFILER_LOGGING_H_

#include <spdlog/spdlog.h>
#include <codecvt>
#include <iostream>
#include <locale>

namespace trace {
std::shared_ptr<spdlog::logger> GetLogger();
}  // namespace trace

// allow wstrings:

namespace fmt {
template <>
struct formatter<std::wstring> {
  template <typename ParseContext>
  constexpr auto parse(ParseContext& ctx) {
    return ctx.begin();
  }
  template <typename FormatContext>
  auto format(const std::wstring& wstr, FormatContext& ctx) {
    std::wstring_convert<std::codecvt_utf8<wchar_t>, wchar_t> converter;
    const auto str = converter.to_bytes(wstr);
    return format_to(ctx.begin(), "{}", std::string_view(str));
  }
};
}  // namespace fmt

#endif  // DD_CLR_PROFILER_LOGGING_H_
