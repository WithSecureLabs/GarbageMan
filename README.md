# GarbageMan

## What is GarbageMan?

`GarbageMan` is a set of tools designed for .NET heap analysis. These tools offer the following benefits for malware researchers: 

- Ability to extract clear-text payload (PE Images etc.) from .NET heaps quickly. 
- Easy analysis of encrypted network protocols, signs of data exfiltration, and similar. 
- Ability to overcome malware anti-dumping techniques (`psnotify`)


More detailed description, background information and usage instructions can be found in WithSecure Labs tools: https://labs.withsecure.com/tools/garbageman/

More details about the `psnotify` can be found here: [psnotify/README.md](psnotify).


## How to get it?

Download the latest release from the "Releases". You will probably need `psnotify` in addition to GarbageMan release.

For running GarbageMan, you need to install .NET 5.0 desktop runtime for amd64 and x86 (yes, both). On the first run, Windows probably offers download link automatically. Just make sure to install `desktop` runtimes.

Note that GarbageMan and psnotify were developed and tested only in x64 Windows 10. It won't probably run on any other Windows version.


## How to use it?

Crash course:

- Extract the release archive, run GarbageMan.exe
- You can attach to running process, or execute a new process, or open minidump from File menu
- You can also open existing GarbageMan database

If you need to use `psnotify` for dumping, you need to extract it to `C:\psnotify` (yes, that's fixed for now). Just run `psnotify.exe` and stop it with `Ctrl+C` when done. It will create minidumps in `C:\dumps`. You can later then analyze those dumps with GarbageMan.


## How to compile the GUI tool

For compiling a release, you need to install:

- MS Visual Studio 2019 or 2022 (pick up .NET desktop dev workload)
- .NET 5.0 Runtime (as a part of VS, with VS2022 you need pick it up from "Individual components")

After installing the VS, run "Developer PowerShell for VS" and then run `build.bat`.

Please do note that `build.bat` refers to `compress.ps1` which is probably not going to run properly until your policy allows running powershell scripts. 

If everything goes well, this should produce `GarbageMan-X.X.X.zip` where X.X.X is the version of your build, as defined in the GarbageMan project assembly properties. If the PS script fails, the GarbageMan release build should still be available in directory `rel`.



