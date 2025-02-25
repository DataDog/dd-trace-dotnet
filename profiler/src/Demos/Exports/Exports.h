// The following ifdef block is the standard way of creating macros which make exporting
// from a DLL simpler. All files within this DLL are compiled with the EXPORTS_EXPORTS
// symbol defined on the command line. This symbol should not be defined on any project
// that uses this DLL. This way any other project whose source files include this file see
// EXPORTS_API functions as being imported from a DLL, whereas this DLL sees symbols
// defined with this macro as being exported.
#ifdef EXPORTS_EXPORTS
#define EXPORTS_API __declspec(dllexport)
#else
#define EXPORTS_API __declspec(dllimport)
#endif

// This class is exported from the dll
class EXPORTS_API CExports {
public:
	CExports(void);
	// TODO: add your methods here.
};

extern EXPORTS_API int nExports;

EXPORTS_API int fnExports(void);
