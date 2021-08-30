#ifndef DD_CLR_PROFILER_STRING_H_
#define DD_CLR_PROFILER_STRING_H_

#include <corhlpr.h>
#include <sstream>
#include <string>

#ifdef _WIN32
#define WStr(value) L##value
#define WStrLen(value) (size_t) wcslen(value)
#else
#define WStr(value) u##value
#define WStrLen(value) (size_t) std::char_traits<char16_t>::length(value)
#endif

namespace shared {

	typedef std::basic_string<WCHAR> WSTRING;

#ifndef MACOS
	typedef std::basic_stringstream<WCHAR> WSTRINGSTREAM;
#endif

	std::string ToString(const std::string& str);
	std::string ToString(const char* str);
	std::string ToString(const uint64_t i);
	std::string ToString(const WSTRING& wstr);

	WSTRING ToWSTRING(const std::string& str);
	WSTRING ToWSTRING(const uint64_t i);

	template <typename TChar>
	std::basic_string<TChar> ReplaceString(std::basic_string<TChar> subject, const std::basic_string<TChar>& search, const std::basic_string<TChar>& replace) {
		size_t pos = 0;
		while ((pos = subject.find(search, pos)) != std::basic_string<TChar>::npos) {
			subject.replace(pos, search.length(), replace);
			pos += replace.length();
		}
		return subject;
	}

}  // namespace trace

#endif  // DD_CLR_PROFILER_STRING_H_
