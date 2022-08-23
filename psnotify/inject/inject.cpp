// inject.cpp : This file contains the 'main' function. Program execution begins and ends there.
//

#include <Windows.h>
#include <iostream>
#include "detours.h"

int main(int argc, char** argv)
{
    if (argc < 2)
    {
        std::cout << "Usage: " << argv[0] << " <pid>\n";
        return 1;
    }

    int pid = atoi(argv[1]);
    HANDLE hProcess = OpenProcess(PROCESS_ALL_ACCESS, FALSE, pid);
    if (hProcess == NULL)
    {
        std::cout << "Error: cannot open process\n";
        return 1;
    }

#if DETOURS_64BIT
    std::string dllPath("C:\\psnotify\\hook64.dll");
#else
    std::string dllPath("C:\\psnotify\\hook32.dll");
#endif
    LPCSTR sz = dllPath.c_str();

    if (!DetourUpdateProcessWithDll(hProcess, &sz, 1))
    {
        printf("DetourUpdateProcessWithDll failed (%d).\n", GetLastError());
    }
    else
    {
        printf("DetourUpdateProcessWithDll success\n");
    }

    CloseHandle(hProcess);
    return 0;
}