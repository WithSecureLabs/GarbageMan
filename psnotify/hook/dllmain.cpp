
#include <Windows.h>
#include <string>
#include <sstream>
#include <fstream>
#include <list>
#include <DbgHelp.h>
#include <detours.h>

BOOL bHook = TRUE;
BOOL GCHooked = FALSE;
BOOL OldGCHooked = FALSE;

WCHAR wcModulePath[MAX_PATH];


VOID PrintMessage(const WCHAR* msg)
{
    std::wstring dbgString(L"Hook: ");
    dbgString += wcModulePath;
    dbgString += L": ";
    dbgString += msg;
    OutputDebugString(dbgString.c_str());
}




BOOL Initialize()
{
    GetModuleFileNameW(NULL, wcModulePath, MAX_PATH);

    std::list<std::wstring> whitelisted;

    // Read in whitelisted processes
    std::wfstream fs_file;

    fs_file.open("C:\\psnotify\\whitelist.txt", std::ios::in);
    if (fs_file.is_open())
    {
        std::wstring line;
        while (std::getline(fs_file, line))
        {
            whitelisted.push_back(line);
        }
        fs_file.close();
    }

    // Check if we should hook this
    std::wstring libName(wcModulePath);
    for (std::list<std::wstring>::iterator it = whitelisted.begin(); it != whitelisted.end(); ++it)
    {
        if (libName.find(*it) != std::string::npos)
        {
            PrintMessage(L"whitelisted");
            bHook = FALSE;
            return FALSE;
        }
    }
    return TRUE;
}



static VOID(WINAPI* TrueCollect)(INT c1, INT c2);

VOID WINAPI CollectHook(INT c1, INT c2)
{
    PrintMessage(L"CollectHook: GCInterface::Collect");
    // Just return
    return;
}

static VOID(WINAPI* TrueCollectGeneration)(INT c1, INT c2);

VOID WINAPI CollectGenerationHook(INT c1, INT c2)
{
    PrintMessage(L"CollectGenerationHook: GCInterface::CollectGeneration");
    // Just return
    return;
}

BOOL ResolveGCCollectGeneration()
{
    HANDLE hProcess;
    hProcess = GetCurrentProcess();

    SymSetOptions(SYMOPT_UNDNAME | SYMOPT_DEFERRED_LOADS | SYMOPT_DEBUG);
    if (!SymInitializeW(hProcess, L"c:\\symbols", TRUE))
    {
        return FALSE;
    }

    ULONG64 buffer[(sizeof(SYMBOL_INFOW) +
        MAX_SYM_NAME * sizeof(TCHAR) +
        sizeof(ULONG64) - 1) /
        sizeof(ULONG64)];
    PSYMBOL_INFOW pSymbol = (PSYMBOL_INFOW)buffer;

    pSymbol->SizeOfStruct = sizeof(SYMBOL_INFOW);
    pSymbol->MaxNameLen = MAX_SYM_NAME;

    std::wstring szSymbolName(L"mscorwks!GCInterface::CollectGeneration");
    HMODULE hMod = GetModuleHandleW(L"mscorwks.dll");
    if (hMod != NULL)
    {
        CHAR path[MAX_PATH];
        if (GetModuleFileNameA(hMod, path, MAX_PATH))
        {
            std::wstringstream ss(L"CLR: ");
            ss << path;
            PrintMessage(ss.str().c_str());
        }
        if (SymFromNameW(hProcess, szSymbolName.c_str(), pSymbol))
        {
            PrintMessage(L"SUCCESS: Found mscorwks!GCInterface::CollectGeneration");
            TrueCollectGeneration = (VOID(WINAPI*)(INT, INT))pSymbol->Address;
            return TRUE;
        }
        else
        {
            PrintMessage(L"ERROR: Could not find mscorwks!GCInterface::CollectGeneration");
            return FALSE;
        }
    }
    return FALSE;
}

BOOL ResolveGCCollect()
{
    HANDLE hProcess;
    hProcess = GetCurrentProcess();

    SymSetOptions(SYMOPT_UNDNAME | SYMOPT_DEFERRED_LOADS | SYMOPT_DEBUG);
    if (!SymInitializeW(hProcess, L"c:\\symbols", TRUE))
    {
        return FALSE;
    }

    ULONG64 buffer[(sizeof(SYMBOL_INFOW) +
        MAX_SYM_NAME * sizeof(TCHAR) +
        sizeof(ULONG64) - 1) /
        sizeof(ULONG64)];
    PSYMBOL_INFOW pSymbol = (PSYMBOL_INFOW)buffer;

    pSymbol->SizeOfStruct = sizeof(SYMBOL_INFOW);
    pSymbol->MaxNameLen = MAX_SYM_NAME;

    std::wstring szSymbolName(L"clr!GCInterface::Collect");
    HMODULE hMod = GetModuleHandleW(L"clr.dll");
    if (hMod != NULL)
    {
        CHAR path[MAX_PATH];
        if (GetModuleFileNameA(hMod, path, MAX_PATH))
        {
            std::wstringstream ss(L"CLR: ");
            ss << path;
            PrintMessage(ss.str().c_str());
        }
        if (SymFromNameW(hProcess, szSymbolName.c_str(), pSymbol))
        {
            PrintMessage(L"SUCCESS: Found clr!GCInterface::Collect");
            TrueCollect = (VOID(WINAPI*)(INT, INT))pSymbol->Address;
            return TRUE;
        }
        else
        {
           PrintMessage(L"ERROR: Could not find clr!GCInterface::Collect");
            return FALSE;
        }
    }

    std::wstring szSymbolNameCore(L"coreclr!GCInterface::Collect");
    hMod = GetModuleHandleW(L"coreclr.dll");
    if (hMod != NULL)
    {
        CHAR path[MAX_PATH];
        if (GetModuleFileNameA(hMod, path, MAX_PATH))
        {
            std::wstringstream ss(L"CLR: ");
            ss << path;
            PrintMessage(ss.str().c_str());
        }
        if (SymFromNameW(hProcess, szSymbolNameCore.c_str(), pSymbol))
        {
            PrintMessage(L"SUCCESS: Found coreclr!GCInterface::Collect");
            TrueCollect = (VOID(WINAPI*)(INT, INT))pSymbol->Address;
            return TRUE;
        }
        else
        {
            PrintMessage(L"ERROR: Could not find coreclr!GCInterface::Collect");
        }
    }
    return FALSE;
}



static HMODULE(WINAPI* TrueLoadLibraryExW)(LPCWSTR lpLibFileName, HANDLE hFile, DWORD dwFlags) = LoadLibraryExW;

HMODULE WINAPI LoadLibraryExWHook(LPCWSTR lpLibFileName, HANDLE hFile, DWORD dwFlags)
{
    if (GCHooked != TRUE)
    {
        std::wstring libName(lpLibFileName ? lpLibFileName : L"");
        if (libName.find(L"clrjit.dll") != std::string::npos)
        {
            if (ResolveGCCollect())
            {
                DetourRestoreAfterWith();
                DetourTransactionBegin();
                DetourUpdateThread(GetCurrentThread());
                DetourAttach(&(PVOID&)TrueCollect, CollectHook);
                DetourTransactionCommit();
                GCHooked = TRUE;
            }
        }
    }

    if (OldGCHooked != TRUE)
    {
        std::wstring libName(lpLibFileName ? lpLibFileName : L"");
        if (libName.find(L"mscorwks.dll") != std::string::npos)
        {
            HMODULE hMod = TrueLoadLibraryExW(lpLibFileName, hFile, dwFlags);
            if (ResolveGCCollectGeneration())
            {
                DetourRestoreAfterWith();
                DetourTransactionBegin();
                DetourUpdateThread(GetCurrentThread());
                DetourAttach(&(PVOID&)TrueCollectGeneration, CollectGenerationHook);
                DetourTransactionCommit();
                OldGCHooked = TRUE;
            }
            return hMod;
        }
    }

    return TrueLoadLibraryExW(lpLibFileName, hFile, dwFlags);
}



BOOL APIENTRY DllMain( HMODULE hModule,
                       DWORD  ul_reason_for_call,
                       LPVOID lpReserved
                     )
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        if (!Initialize())
            break;
        DetourRestoreAfterWith();
        DetourTransactionBegin();
        DetourUpdateThread(GetCurrentThread());
        DetourAttach(&(PVOID&)TrueLoadLibraryExW, LoadLibraryExWHook);
        DetourTransactionCommit();
        break;
    case DLL_PROCESS_DETACH:
        if (!bHook)
            break;
        DetourTransactionBegin();
        DetourUpdateThread(GetCurrentThread());
        DetourDetach(&(PVOID&)TrueLoadLibraryExW, LoadLibraryExWHook);
        if (GCHooked == TRUE)
            DetourDetach(&(PVOID&)TrueCollect, CollectHook);
        if (OldGCHooked == TRUE)
            DetourDetach(&(PVOID&)TrueCollectGeneration, CollectGenerationHook);
        DetourTransactionCommit();
        break;
    }
    return TRUE;
}

