The files here were copied from https://github.com/dotnet/runtime/tree/v7.0.0/src/coreclr/src.

This is to allow using the runtime's Platform Adaptation Layer.

Commented #define statements because there is naming conflicts when compiling with the stdlibc++ 8 (+ C++17)
in `dotnet-runtime-coreclr\pal\inc\rt\sal.h`
l.2612    // commented because it conflicts with stdlibc++ 8
l.2613    //#define __valid

and

l.2622    // commented because it conflicts with stdlibc++ 8
l.2623    //#define __pre