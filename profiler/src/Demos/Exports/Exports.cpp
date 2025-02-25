// Exports.cpp : Defines the exported functions for the DLL.
//

#include "pch.h"
#include "framework.h"
#include "Exports.h"

// This is an example of an exported variable
EXPORTS_API int nExports=0;

// This is an example of an exported function.
EXPORTS_API int fnExports(void)
{
    return 0;
}

// This is the constructor of a class that has been exported.
CExports::CExports()
{
    return;
}
