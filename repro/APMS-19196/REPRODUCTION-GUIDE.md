# APMS-19196 InvalidProgramException Crash Reproduction

## 🎯 **SUCCESSFULLY REPRODUCED!**

This reproduction demonstrates the exact `InvalidProgramException` crash described in APMS-19196.

## 📁 **Essential Files**

### **Core Application Files**
- `Program.cs` - ASP.NET Core application setup
- `ReproApp.csproj` - Project file
- `Middlewares/ExtremeEHMiddleware.cs` - **THE CRASH TRIGGER** - Contains complex EH patterns that trigger the bug

### **Reproduction Script**
- `reproduce-crash.sh` - **Single script that reproduces the crash**

### **Documentation**
- `handoff issue` - Detailed technical analysis of the root cause
- `README.md` - Original README
- `Dockerfile` - Docker setup (alternative to WSL)

## 🚀 **Quick Reproduction**

### **Prerequisites**
- WSL2 with .NET 9.0+ installed
- Must run on **native x86_64 Linux** (WSL2, not ARM64/emulation)

### **Run the Reproduction**
```bash
# In WSL terminal:
cd /mnt/c/repositories/dd-trace-dotnet/repro/APMS-19196
bash ./reproduce-crash.sh
```

### **Test the Crash**
```bash
# In another terminal, once app starts:
curl http://localhost:5000/
```

## 🔥 **Expected Result**
Every HTTP request will crash with:
```
System.InvalidProgramException: Common Language Runtime detected an invalid program.
   at ReproApp.ExtremeEHMiddleware.DeepNestedChainAsync(HttpContext context, Int32 id, Int32 depth)
```

## 🔑 **Critical Success Factors**
1. **Native WSL execution** (no Docker overhead)
2. **`DD_TRACE_DEBUG=false`** (debug logs mask the race condition!)
3. **`DD_INTERNAL_DEBUGGER_INSTRUMENT_ALL=true`** (instruments at JIT time)  
4. **Tracer v3.41.0** (the buggy version)
5. **ExtremeEHMiddleware** (creates the specific EH clause patterns that trigger the bug)

## 🐛 **Root Cause**
- **Bug Location**: `Datadog.Tracer.Native/il_rewriter.cpp` EH clause sorting algorithm
- **Trigger**: Complex "try-in-handler" nesting in async state machine IL  
- **Result**: Malformed IL causing CLR `InvalidProgramException`

## ✅ **Verification**
This reproduction was successfully tested and confirms the exact crash described in APMS-19196.