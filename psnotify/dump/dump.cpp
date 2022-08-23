

#include <Windows.h>
#include <iostream>
#include <sstream>
#include <fstream>
#include <Psapi.h>
#include <DbgHelp.h>
#include <list>

int main(int argc, char** argv)
{
    if (argc < 2)
    {
        std::cout << "Usage: " << argv[0] << " <pid> [-a]\n";
        return 1;
    }

    // Read in whitelisted processes
    std::list<std::wstring> whitelisted;
    std::wfstream fs_file;
    fs_file.open("whitelist.txt", std::ios::in);
    if (fs_file.is_open())
    {
        std::wstring line;
        while (std::getline(fs_file, line))
        {
            whitelisted.push_back(line);
        }
        fs_file.close();
    }

    int pid = atoi(argv[1]);
    HANDLE hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, FALSE, pid);
    if (hProcess == NULL)
    {
        std::cout << "Error: cannot open process\n";
        return 1;
    }

    // Enumerate loaded modules in the process for detecting .NET runtimes
    HMODULE hMods[1024];
    DWORD cbNeeded;
    unsigned int i;
    BOOL bIsDotNet = FALSE;
    if (EnumProcessModules(hProcess, hMods, sizeof(hMods), &cbNeeded))
    {
        for (i = 0; i < (cbNeeded / sizeof(HMODULE)); i++)
        {
            TCHAR szModName[MAX_PATH];
            if (GetModuleFileNameEx(hProcess, hMods[i], szModName,
                sizeof(szModName) / sizeof(TCHAR)))
            {
                // Check if this module indicates .NET
                std::wstring modName(szModName);
                if ((modName.find(L"clrjit.dll") != std::string::npos) || (modName.find(L"mscorwks.dll") != std::string::npos))
                {
                    bIsDotNet = TRUE;
                    break;
                }
            }
        }
    }
    if ((bIsDotNet == FALSE) && (argc < 3))
    {
        CloseHandle(hProcess);
        return 0;
    }

    // Create dump directory
    std::wstring dirName(L"C:\\dumps");
    CreateDirectory(dirName.c_str(), NULL);

    // Get the base name of process executable
    std::wstringstream ss;
    WCHAR moduleName[MAX_PATH];
    if (!GetModuleBaseName(hProcess, NULL, moduleName, MAX_PATH - 1))
    {
        ss << dirName << L"\\dump." << pid << L".dmp";
    }
    else
    {
        ss << dirName << L"\\" << moduleName << L"." << pid << L".dmp";
    }

    // Check of this process is whitelisted
    for (std::list<std::wstring>::iterator it = whitelisted.begin(); it != whitelisted.end(); ++it)
    {
        if (*it == moduleName)
        {
            CloseHandle(hProcess);
            return 0;
        }
    }

    // Ready for dump!

    std::wcout << L"Dumping process to " << ss.str().c_str() << std::endl;
    HANDLE hFile = CreateFile(ss.str().c_str(),                // name of the write
        GENERIC_WRITE,          // open for writing
        0,                      // do not share
        NULL,                   // default security
        CREATE_ALWAYS,          // create always
        FILE_ATTRIBUTE_NORMAL,  // normal file
        NULL);                  // no attr. template

    if (hFile == NULL)
    {
        std::cout << "Error: cannot open dump file\n";
        CloseHandle(hProcess);
        return 1;
    }

    BOOL s = MiniDumpWriteDump(
        hProcess,
        pid,
        hFile,
        MiniDumpWithFullMemory,
        NULL,
        NULL,
        NULL
    );

    if (!s)
    {
        std::cout << "Error: cannot dump process: " << GetLastError() << std::endl;
        CloseHandle(hProcess);
        CloseHandle(hFile);
        return 1;
    }

    CloseHandle(hProcess);
    CloseHandle(hFile);
    return 0;
}
