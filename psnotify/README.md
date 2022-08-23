# psnotify

`psnotify` is a POC tool to fight .NET anti-dumping tricks. It does this with two specific techniques:

- Dump process image to disk on exit
- Prevent user-initiated garbage collection

The key functionality of `psnotify` is based on registering `PsSetCreateProcessNotifyRoutineEx` with a driver that notifies the user-mode component for any process creation and termination. That's where the name comes from.

For new processes, the program injects a hook DLL that tries to prevent garbage collection. On process exit, the program dumps its full memory on disk as MS minidump image.


## Installation, running and configuration

Installation is very simple: just extract the release archive as `C:\psnotify`. The directory is hard-coded in the POC.

Running is a matter of executing `psnotify.exe`, it will take care of all the service handling. By default, it dumps only .NET processes. This can be changed with command line option `-a`. Hook DLL is injected to all processes, since managed functionality can in theory also appear dynamically.

**Note**: `psnotify.sys` is signed by test certificate. You need to sign it yourself or put the machine in test sigining mode (`bcdedit /set testsigning on`).

Minidumps are written to `C:\dumps`. These files are straight out compatible with `GarbageMan`.

There are two things to configure:
- Whitelisting. Just add program names to `whitelist.txt` for preventing dumping. For example there's GarbageMan and some other common non-interesting programs
- Symbols are needed by the user-mode hook component in `c:\symbols` (more on that below). 


## GC hooking and symbols

This feature is based on inline hooking the garbage collection routine in .NET runtime. Prototype of the routine looks like this:

```
static VOID (WINAPI* Collect)(INT c1, INT c2);
```

Bypassing garbage collection can be achieved by just inserting return. We use the [Detours](https://github.com/microsoft/Detours) library for inline hooking.

This routine is not exported symbol, so it needs to be resolved from debug symbol file. If the symbols are not available in `C:\symbols`, hooking cannot be done.

Hook routine logs some details using debug printing, so you need to first make that visible with [dbgview](https://docs.microsoft.com/en-us/sysinternals/downloads/debugview).

Failure to install hook looks like this in debug log:
```
[4564] Hook: C:\Users\turkja\Test.exe: C:\Windows\Microsoft.NET\Framework\v4.0.30319\clr.dll
[4564] Hook: C:\Users\turkja\Test.exe: ERROR: Could not find clr!GCInterface::Collect
```

Most simple method for downlowding the symbols is a tool like [PDB-Downlowder](https://github.com/rajkumar-rangaraj/PDB-Downloader). Just take the DLL path from debug log to get the PDB to default location (`C:\symbols`). It might be a good idea to add `PDBDownlader.exe` to `whitelist.txt`, if you use it to get symbols.

Successful hooking looks like this:
```
[4032] Hook: C:\Users\turkja\Test.exe: C:\Windows\Microsoft.NET\Framework\v4.0.30319\clr.dll
[4032] Hook: C:\Users\turkja\Test.exe: SUCCESS: Found clr!GCInterface::Collect
```

Note that you need to download symbols for each .NET runtime. In practice, this needs to be done 32 -and 64-bit .NET Framework 4.x, .NET 5.0 etc. Failure to install hooks doesn't prevent anything else besides the user-initiated GC. Progams run and gets dumped on disk anyways.


## How to compile

For compiling user-mode and kernel-mode components, you need to install:

- MS Visual Studio 2019 or 2022 (with C++ dev workload)
- WDK for Windows 10 (for driver)

There are separate solution files for user-mode (`exe/psnotify.sln`) and driver (`sys/psnotify.sln`). First can used to compile all user-mode components. Note that you need both 32 and 64-bit builds for `dump`, `hook` and `inject`.

When compiling the driver, you probably need to deal with driver signing, which is turned off by the solution. The solution file refers to test-signing certificate `test.pfx` which you need to create, or then compile without signature and sign manually.

Also note that we use ad-hoc altitude value for the driver, you might need to deal with this as well: https://docs.microsoft.com/en-us/windows-hardware/drivers/ifs/load-order-groups-and-altitudes-for-minifilter-drivers





