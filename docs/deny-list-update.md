# What is the deny-list?

With SSI, we have introduced deny-list mechanisms to avoid injecting certain processes. Part of the deny list is handled in the injector itself.

Then there are language-specific deny-lists built into the libraries (which are loaded by the tracer), loaded via `requirements.json`. These are only run for processes which are detected as the given language
The `requirements.json` file specifies two things: 
- Which architectures and glibc/musl combination the tracer version supports
- Additional processes to deny

The deny lists in `requirements.json` block based on a combination of
- executable path (glob match)
- command arguments (pretty flexible, some limitations)
- environment variables (we should be very wary of these as we've seen)

Finally, the libraries themselves have built-in deny-lists that apply in all cases, i.e. including outside of SSI. The implementations of these vary significantly by library

# How to block additional processes?

Directly add an entry in the `/tracer/build/artifacts/requiremements.json`. The entry has to be added in the `deny` json array. Refer to the RFC for more details of the content.   
From there, you just have to open a PR.