The files here were copied from https://github.com/dotnet/runtime/tree/v5.0.5/src/coreclr/src.

This is to allow using the runtime's Platform Adaptation Layer.

Add back the definition of g_tkCorEncodeToken in cor.h l.2096:

replace
extern const mdToken g_tkCorEncodeToken[];
by
const mdToken g_tkCorEncodeToken[4] = { mdtTypeDef, mdtTypeRef, mdtTypeSpec, mdtBaseType };

Commented #define statements because there is naming conflicts when compiling with the stdlibc++ 8 (+ C++17)
in `dotnet-runtime-coreclr\pal\inc\rt\sal.h`
l.2612    // commented because it conflicts with stdlibc++ 8
l.2613    //#define __valid

and

l.2622    // commented because it conflicts with stdlibc++ 8
l.2623    //#define __pre