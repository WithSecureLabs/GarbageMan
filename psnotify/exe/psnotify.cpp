#include <windows.h>
#include <stdio.h>
#include <psnotify.h>
#include <fltuser.h>
#include <vector>
#include <sstream>
#include <Dbghelp.h>
#include <list>
#include <mutex>
#include <iostream>

class ProcessNotificationMessageHandler
{
protected:
    const UINT MINIFILTER_MESSAGE_QUEUE_SIZE = 100;
    HANDLE m_hDriver = nullptr;
    HANDLE m_hPort = nullptr;
    HANDLE m_Completion = nullptr;
    std::list<DWORD>m_pids;
    std::mutex m_mutex;
    BOOL m_bDumpAll = FALSE;

    VOID* m_MiniFilterRequestMessages = nullptr;

    struct MINIFILTER_EXCHANGE_REQUEST_MESSAGE 
    {
        FILTER_MESSAGE_HEADER MessageHeader; 
        ProcessNotificationMessage  MessageFrame; 
        OVERLAPPED Ovlp;
    };


public:
    void Close()
    {
        CloseHandle(m_Completion);
        CloseHandle(m_hPort);
        CloseHandle(m_hDriver);
    }

    BOOL Initialize(BOOL bDumpAll)
    {
        m_bDumpAll = bDumpAll;

        m_hDriver = CreateFileW(PsNotifyLinkNameDos,
            GENERIC_READ | GENERIC_WRITE,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            NULL,
            OPEN_EXISTING,
            FILE_ATTRIBUTE_NORMAL | FILE_FLAG_OVERLAPPED,
            NULL);

        if (m_hDriver == INVALID_HANDLE_VALUE)
        {
            printf("failed to open driver %u\n", GetLastError());
            return FALSE;
        }
        printf("opened driver 0x%p\n", m_hDriver);

        HRESULT hr = FilterConnectCommunicationPort(PsNotifyPort,
            0,
            NULL,
            0,
            NULL,
            &m_hPort);
        printf("opened port as 0x%x, 0x%p\n", hr, m_hPort);
        if (hr != ERROR_SUCCESS)
            return FALSE;

        m_Completion = CreateIoCompletionPort(m_hPort, NULL, 0, MINIFILTER_MESSAGE_QUEUE_SIZE);

        if (!m_Completion) {
            printf("failed to make completion\n");
        }

        return m_Completion != NULL;
    }

    void Run()
    {
        MINIFILTER_EXCHANGE_REQUEST_MESSAGE* pMessageArray = new MINIFILTER_EXCHANGE_REQUEST_MESSAGE[MINIFILTER_MESSAGE_QUEUE_SIZE];

        m_MiniFilterRequestMessages = pMessageArray;

        memset(pMessageArray, 0, sizeof(MINIFILTER_EXCHANGE_REQUEST_MESSAGE) * MINIFILTER_MESSAGE_QUEUE_SIZE);

        for (UINT MsgIdx = 0; MsgIdx < MINIFILTER_MESSAGE_QUEUE_SIZE; MsgIdx++)
        {
            HRESULT hr;

            hr = FilterGetMessage(m_hPort,
                &pMessageArray[MsgIdx].MessageHeader,
                FIELD_OFFSET(MINIFILTER_EXCHANGE_REQUEST_MESSAGE, Ovlp),
                &pMessageArray[MsgIdx].Ovlp);

            if (hr != HRESULT_FROM_WIN32(ERROR_IO_PENDING))
            {
                printf("invalid state of message 0x%x", hr);
                return;
            }
        }

        for (UINT MsgIdx = 0; MsgIdx < MINIFILTER_MESSAGE_QUEUE_SIZE; MsgIdx++)
        {
            DWORD threadId = 0;
            CreateThread(NULL, 0, MessageHandlerThreadThread, this, 0, &threadId);
        }
    }
protected:

    static DWORD MessageHandlerThreadThread(PVOID Param)
    {
        auto pThis = static_cast<ProcessNotificationMessageHandler*>(Param);
        pThis->MessageHandlerThread();
        return 0;
    }
    
    void MessageHandlerThread()
    {
        BOOL result;
        DWORD outSize;
        LPOVERLAPPED pOvlp;
        MINIFILTER_EXCHANGE_REQUEST_MESSAGE* pMessage = 0;
        ULONG_PTR key;


        const ULONG ulFrameSize = sizeof(ProcessNotificationMessage);

        do
        {
            if (!m_hPort || !m_Completion) // connection closed
                break;

            result = GetQueuedCompletionStatus(m_Completion, &outSize, &key, &pOvlp, INFINITE);
            if (!result)
            {
                DWORD dwError = GetLastError();
                if (ERROR_OPERATION_ABORTED != dwError && ERROR_ABANDONED_WAIT_0 != dwError)
                {
                    printf("GetQueuedCompletionStatus failed, error %u", dwError);
                }
                break;
            }

            pMessage = CONTAINING_RECORD(pOvlp, MINIFILTER_EXCHANGE_REQUEST_MESSAGE, Ovlp);

            HandleMessage(pMessage->MessageHeader.MessageId, pMessage->MessageFrame);

            memset(&pMessage->Ovlp, 0, sizeof(OVERLAPPED));

            if (!m_hPort || !m_Completion) // connection closed
                break;

            HRESULT hr = FilterGetMessage(m_hPort, &pMessage->MessageHeader, FIELD_OFFSET(MINIFILTER_EXCHANGE_REQUEST_MESSAGE, Ovlp), &pMessage->Ovlp);

            if (hr != HRESULT_FROM_WIN32(ERROR_IO_PENDING))
            {
                printf("wrong status of FilterGetMessage 0x%X", hr);
                break;
            }
        } while (TRUE); 
    }

    void AddPid(DWORD pid)
    {
        std::lock_guard<std::mutex> guard(m_mutex);
        m_pids.push_back(pid);
    }

    void RemovePid(DWORD pid)
    {
        std::lock_guard<std::mutex> guard(m_mutex);
        m_pids.remove(pid);
    }

    BOOL CheckPid(DWORD pid)
    {
        std::lock_guard<std::mutex> guard(m_mutex);
        auto it = std::find(m_pids.begin(), m_pids.end(), pid);
        if (it != m_pids.end())
        {
            return TRUE;
        }
        return FALSE;
    }

    void HandleMessage(ULONGLONG msgId, ProcessNotificationMessage& messageFrame)
    {
        if (messageFrame.msgId == MessageId::OnTerminateRequest)
        {
            printf("Pid is terminated %u\n", messageFrame.onTerminateRequest.pid);

            ProcessNotificationMessage answer;
            answer.msgId = MessageId::OnTerminateResponse;
            answer.onTerminateResponse.status = 0;

            if (CheckPid(messageFrame.onTerminateRequest.pid))
            {
                ReplyMessage(msgId, answer);
                return;
            }

            HANDLE hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, FALSE, messageFrame.onTerminateRequest.pid);
            if (hProcess)
            {
                BOOL bWow64 = FALSE;
                IsWow64Process(hProcess, &bWow64);
                CloseHandle(hProcess);
                std::wstringstream ss;
                if (bWow64)
                {
                    ss << L"dump32.exe ";
                }
                else
                {
                    ss << L"dump64.exe ";
                }
                ss << messageFrame.onTerminateRequest.pid;

                if (m_bDumpAll)
                    ss << L" -a";

                WCHAR cmdLine[MAX_PATH];
                ZeroMemory(cmdLine, sizeof(cmdLine));
                wcscpy_s(cmdLine, MAX_PATH, ss.str().c_str());

                STARTUPINFO si;
                PROCESS_INFORMATION pi;
                ZeroMemory(&si, sizeof(si));
                si.cb = sizeof(si);
                ZeroMemory(&pi, sizeof(pi));

                if (!CreateProcess(NULL,   // No module name (use command line)
                    cmdLine,        // Command line
                    NULL,           // Process handle not inheritable
                    NULL,           // Thread handle not inheritable
                    FALSE,          // Set handle inheritance to FALSE
                    0,              // No creation flags
                    NULL,           // Use parent's environment block
                    NULL,           // Use parent's starting directory 
                    &si,            // Pointer to STARTUPINFO structure
                    &pi)            // Pointer to PROCESS_INFORMATION structure
                    )
                {
                    printf("CreateProcess failed (%d).\n", GetLastError());
                }
                else
                {
                    AddPid(pi.dwProcessId);
                    WaitForSingleObject(pi.hProcess, INFINITE);
                    RemovePid(pi.dwProcessId);
                }
            }
            ReplyMessage(msgId, answer);
        }
        else if (messageFrame.msgId == MessageId::OnCreateRequest)
        {
            printf("Creating pid %u by parent %u '%ls' - '%ls' \n",
                messageFrame.onCreateRequest.pid,
                messageFrame.onCreateRequest.parentPid,
                messageFrame.onCreateRequest.imagePath,
                messageFrame.onCreateRequest.commandLine);

            ProcessNotificationMessage answer;
            answer.msgId = MessageId::OnCreateResponse;
            answer.onCreateResponse.status = 0;

            if (messageFrame.onCreateRequest.parentPid == GetCurrentProcessId())
            {
                ReplyMessage(msgId, answer);
                return;
            }

            // Inject
            HANDLE hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, FALSE, messageFrame.onCreateRequest.pid);
            if (hProcess)
            {
                BOOL bWow64 = FALSE;
                IsWow64Process(hProcess, &bWow64);
                CloseHandle(hProcess);
                std::wstringstream ss;
                if (bWow64)
                {
                    ss << L"inject32.exe ";
                }
                else
                {
                    ss << L"inject64.exe ";
                }
                ss << messageFrame.onCreateRequest.pid;

                WCHAR cmdLine[MAX_PATH];
                ZeroMemory(cmdLine, sizeof(cmdLine));
                wcscpy_s(cmdLine, MAX_PATH, ss.str().c_str());

                STARTUPINFO si;
                PROCESS_INFORMATION pi;
                ZeroMemory(&si, sizeof(si));
                si.cb = sizeof(si);
                ZeroMemory(&pi, sizeof(pi));

                if (!CreateProcess(NULL,   // No module name (use command line)
                    cmdLine,        // Command line
                    NULL,           // Process handle not inheritable
                    NULL,           // Thread handle not inheritable
                    FALSE,          // Set handle inheritance to FALSE
                    0,              // No creation flags
                    NULL,           // Use parent's environment block
                    NULL,           // Use parent's starting directory 
                    &si,            // Pointer to STARTUPINFO structure
                    &pi)            // Pointer to PROCESS_INFORMATION structure
                    )
                {
                    printf("CreateProcess failed (%d).\n", GetLastError());
                }
                else
                {
                    WaitForSingleObject(pi.hProcess, INFINITE);
                }
            }
            ReplyMessage(msgId, answer);
        }
    }

    
    void ReplyMessage(ULONGLONG msgId, ProcessNotificationMessage& messageFrame)
    {
        DWORD dwSendSize = sizeof(FILTER_REPLY_HEADER) + sizeof(messageFrame);

        std::vector<BYTE> replyMessage;
        replyMessage.resize(dwSendSize, 0);

        FILTER_REPLY_HEADER* pReplyHeader = (FILTER_REPLY_HEADER*)replyMessage.data();
        pReplyHeader->MessageId = msgId;
        memcpy(replyMessage.data() + sizeof(FILTER_REPLY_HEADER), &messageFrame, sizeof(messageFrame));

        HRESULT hr = FilterReplyMessage(m_hPort, pReplyHeader, dwSendSize);
        if (!SUCCEEDED(hr))
            printf("Failed to reply message. Error = 0x%X, sendsize=%u\n", hr, dwSendSize);
    }

};

BOOL SvcInstall()
{
    SC_HANDLE schSCManager;
    SC_HANDLE schService;

    // Get a handle to the SCM database. 

    schSCManager = OpenSCManager(
        NULL,                    // local computer
        NULL,                    // ServicesActive database 
        SC_MANAGER_ALL_ACCESS);  // full access rights 

    if (NULL == schSCManager)
    {
        printf("OpenSCManager failed (%d)\n", GetLastError());
        return FALSE;
    }

    // Create the service

    schService = CreateService(
        schSCManager,                   // SCM database 
        L"psnotify",                    // name of service 
        NULL,                           // no service name to display 
        SERVICE_ALL_ACCESS,             // desired access 
        SERVICE_KERNEL_DRIVER,          // service type 
        SERVICE_DEMAND_START,           // start type 
        SERVICE_ERROR_NORMAL,           // error control type 
        L"c:\\psnotify\\psnotify.sys",  // path to service's binary 
        NULL,                           // no load ordering group 
        NULL,                           // no tag identifier 
        NULL,                           // no dependencies 
        NULL,                           // LocalSystem account 
        NULL);                          // no password 

    if (schService == NULL)
    {
        printf("CreateService failed (%d)\n", GetLastError());
        CloseServiceHandle(schSCManager);
        return FALSE;
    }
    else
    {
        system("reg import inst.reg");
        printf("Service installed successfully\n");
    }

    CloseServiceHandle(schService);
    CloseServiceHandle(schSCManager);
    return TRUE;
}

BOOL SvcStart()
{
    SERVICE_STATUS_PROCESS ssStatus;
    DWORD dwOldCheckPoint;
    DWORD dwStartTickCount;
    DWORD dwWaitTime;
    DWORD dwBytesNeeded;
    SC_HANDLE schSCManager;
    SC_HANDLE schService;

    // Get a handle to the SCM database. 

    schSCManager = OpenSCManager(
        NULL,                    // local computer
        NULL,                    // servicesActive database 
        SC_MANAGER_ALL_ACCESS);  // full access rights 

    if (NULL == schSCManager)
    {
        printf("OpenSCManager failed (%d)\n", GetLastError());
        return FALSE;
    }

    // Get a handle to the service.

    schService = OpenService(
        schSCManager,         // SCM database 
        L"psnotify",          // name of service 
        SERVICE_ALL_ACCESS);  // full access 

    if (schService == NULL)
    {
        printf("OpenService failed (%d)\n", GetLastError());
        CloseServiceHandle(schSCManager);
        return FALSE;
    }

    // Check the status in case the service is not stopped. 

    if (!QueryServiceStatusEx(
        schService,                     // handle to service 
        SC_STATUS_PROCESS_INFO,         // information level
        (LPBYTE)&ssStatus,             // address of structure
        sizeof(SERVICE_STATUS_PROCESS), // size of structure
        &dwBytesNeeded))              // size needed if buffer is too small
    {
        printf("QueryServiceStatusEx failed (%d)\n", GetLastError());
        CloseServiceHandle(schService);
        CloseServiceHandle(schSCManager);
        return FALSE;
    }

    // Check if the service is already running. It would be possible 
    // to stop the service here, but for simplicity this example just returns. 

    if (ssStatus.dwCurrentState != SERVICE_STOPPED && ssStatus.dwCurrentState != SERVICE_STOP_PENDING)
    {
        printf("Cannot start the service because it is already running\n");
        CloseServiceHandle(schService);
        CloseServiceHandle(schSCManager);
        return FALSE;
    }

    // Save the tick count and initial checkpoint.

    dwStartTickCount = GetTickCount();
    dwOldCheckPoint = ssStatus.dwCheckPoint;

    // Wait for the service to stop before attempting to start it.

    while (ssStatus.dwCurrentState == SERVICE_STOP_PENDING)
    {
        // Do not wait longer than the wait hint. A good interval is 
        // one-tenth of the wait hint but not less than 1 second  
        // and not more than 10 seconds. 

        dwWaitTime = ssStatus.dwWaitHint / 10;

        if (dwWaitTime < 1000)
            dwWaitTime = 1000;
        else if (dwWaitTime > 10000)
            dwWaitTime = 10000;

        Sleep(dwWaitTime);

        // Check the status until the service is no longer stop pending. 

        if (!QueryServiceStatusEx(
            schService,                     // handle to service 
            SC_STATUS_PROCESS_INFO,         // information level
            (LPBYTE)&ssStatus,             // address of structure
            sizeof(SERVICE_STATUS_PROCESS), // size of structure
            &dwBytesNeeded))              // size needed if buffer is too small
        {
            printf("QueryServiceStatusEx failed (%d)\n", GetLastError());
            CloseServiceHandle(schService);
            CloseServiceHandle(schSCManager);
            return FALSE;
        }

        if (ssStatus.dwCheckPoint > dwOldCheckPoint)
        {
            // Continue to wait and check.

            dwStartTickCount = GetTickCount();
            dwOldCheckPoint = ssStatus.dwCheckPoint;
        }
        else
        {
            if (GetTickCount() - dwStartTickCount > ssStatus.dwWaitHint)
            {
                printf("Timeout waiting for service to stop\n");
                CloseServiceHandle(schService);
                CloseServiceHandle(schSCManager);
                return FALSE;
            }
        }
    }

    // Attempt to start the service.

    if (!StartService(
        schService,  // handle to service 
        0,           // number of arguments 
        NULL))      // no arguments 
    {
        printf("StartService failed (%d)\n", GetLastError());
        CloseServiceHandle(schService);
        CloseServiceHandle(schSCManager);
        return FALSE;
    }
    else printf("Service start pending...\n");

    // Check the status until the service is no longer start pending. 

    if (!QueryServiceStatusEx(
        schService,                     // handle to service 
        SC_STATUS_PROCESS_INFO,         // info level
        (LPBYTE)&ssStatus,             // address of structure
        sizeof(SERVICE_STATUS_PROCESS), // size of structure
        &dwBytesNeeded))              // if buffer too small
    {
        printf("QueryServiceStatusEx failed (%d)\n", GetLastError());
        CloseServiceHandle(schService);
        CloseServiceHandle(schSCManager);
        return FALSE;
    }

    // Save the tick count and initial checkpoint.

    dwStartTickCount = GetTickCount();
    dwOldCheckPoint = ssStatus.dwCheckPoint;

    while (ssStatus.dwCurrentState == SERVICE_START_PENDING)
    {
        // Do not wait longer than the wait hint. A good interval is 
        // one-tenth the wait hint, but no less than 1 second and no 
        // more than 10 seconds. 

        dwWaitTime = ssStatus.dwWaitHint / 10;

        if (dwWaitTime < 1000)
            dwWaitTime = 1000;
        else if (dwWaitTime > 10000)
            dwWaitTime = 10000;

        Sleep(dwWaitTime);

        // Check the status again. 

        if (!QueryServiceStatusEx(
            schService,             // handle to service 
            SC_STATUS_PROCESS_INFO, // info level
            (LPBYTE)&ssStatus,             // address of structure
            sizeof(SERVICE_STATUS_PROCESS), // size of structure
            &dwBytesNeeded))              // if buffer too small
        {
            printf("QueryServiceStatusEx failed (%d)\n", GetLastError());
            break;
        }

        if (ssStatus.dwCheckPoint > dwOldCheckPoint)
        {
            // Continue to wait and check.

            dwStartTickCount = GetTickCount();
            dwOldCheckPoint = ssStatus.dwCheckPoint;
        }
        else
        {
            if (GetTickCount() - dwStartTickCount > ssStatus.dwWaitHint)
            {
                // No progress made within the wait hint.
                break;
            }
        }
    }

    // Determine whether the service is running.

    if (ssStatus.dwCurrentState == SERVICE_RUNNING)
    {
        printf("Service started successfully.\n");
    }
    else
    {
        printf("Service not started. \n");
        printf("  Current State: %d\n", ssStatus.dwCurrentState);
        printf("  Exit Code: %d\n", ssStatus.dwWin32ExitCode);
        printf("  Check Point: %d\n", ssStatus.dwCheckPoint);
        printf("  Wait Hint: %d\n", ssStatus.dwWaitHint);
    }

    CloseServiceHandle(schService);
    CloseServiceHandle(schSCManager);
    return TRUE;
}

BOOL SvcStop()
{
    SERVICE_STATUS_PROCESS ssp;
    DWORD dwStartTime = GetTickCount();
    DWORD dwBytesNeeded;
    DWORD dwTimeout = 30000; // 120-second time-out
    DWORD dwWaitTime;
    SC_HANDLE schSCManager;
    SC_HANDLE schService;

    // Get a handle to the SCM database. 

    schSCManager = OpenSCManager(
        NULL,                    // local computer
        NULL,                    // ServicesActive database 
        SC_MANAGER_ALL_ACCESS);  // full access rights 

    if (NULL == schSCManager)
    {
        printf("OpenSCManager failed (%d)\n", GetLastError());
        return FALSE;
    }

    // Get a handle to the service.

    schService = OpenService(
        schSCManager,         // SCM database 
        L"psnotify",          // name of service 
        SERVICE_STOP |
        SERVICE_QUERY_STATUS |
        SERVICE_ENUMERATE_DEPENDENTS);

    if (schService == NULL)
    {
        printf("OpenService failed (%d)\n", GetLastError());
        CloseServiceHandle(schSCManager);
        return FALSE;
    }

    // Make sure the service is not already stopped.

    if (!QueryServiceStatusEx(
        schService,
        SC_STATUS_PROCESS_INFO,
        (LPBYTE)&ssp,
        sizeof(SERVICE_STATUS_PROCESS),
        &dwBytesNeeded))
    {
        printf("QueryServiceStatusEx failed (%d)\n", GetLastError());
        goto stop_cleanup;
    }

    if (ssp.dwCurrentState == SERVICE_STOPPED)
    {
        printf("Service is already stopped.\n");
        goto stop_cleanup;
    }

    // If a stop is pending, wait for it.

    while (ssp.dwCurrentState == SERVICE_STOP_PENDING)
    {
        printf("Service stop pending...\n");

        // Do not wait longer than the wait hint. A good interval is 
        // one-tenth of the wait hint but not less than 1 second  
        // and not more than 10 seconds. 

        dwWaitTime = ssp.dwWaitHint / 10;

        if (dwWaitTime < 1000)
            dwWaitTime = 1000;
        else if (dwWaitTime > 10000)
            dwWaitTime = 10000;

        Sleep(dwWaitTime);

        if (!QueryServiceStatusEx(
            schService,
            SC_STATUS_PROCESS_INFO,
            (LPBYTE)&ssp,
            sizeof(SERVICE_STATUS_PROCESS),
            &dwBytesNeeded))
        {
            printf("QueryServiceStatusEx failed (%d)\n", GetLastError());
            goto stop_cleanup;
        }

        if (ssp.dwCurrentState == SERVICE_STOPPED)
        {
            printf("Service stopped successfully.\n");
            goto stop_cleanup;
        }

        if (GetTickCount() - dwStartTime > dwTimeout)
        {
            printf("Service stop timed out.\n");
            goto stop_cleanup;
        }
    }

    // Send a stop code to the service.
    printf("Stopping service...\n");

    if (!ControlService(
        schService,
        SERVICE_CONTROL_STOP,
        (LPSERVICE_STATUS)&ssp))
    {
        printf("ControlService failed (%d)\n", GetLastError());
        goto stop_cleanup;
    }

    // Wait for the service to stop.

    while (ssp.dwCurrentState != SERVICE_STOPPED)
    {
        Sleep(ssp.dwWaitHint);

        if (!QueryServiceStatusEx(
            schService,
            SC_STATUS_PROCESS_INFO,
            (LPBYTE)&ssp,
            sizeof(SERVICE_STATUS_PROCESS),
            &dwBytesNeeded))
        {
            printf("QueryServiceStatusEx failed (%d)\n", GetLastError());
            goto stop_cleanup;
        }
        printf("Service status: %d\n", ssp.dwCurrentState);

        if (ssp.dwCurrentState == SERVICE_STOPPED)
            break;

        if (GetTickCount() - dwStartTime > dwTimeout)
        {
            printf("Wait timed out\n");
            goto stop_cleanup;
        }
    }
    printf("Service stopped successfully\n");

stop_cleanup:
    CloseServiceHandle(schService);
    CloseServiceHandle(schSCManager);
    return TRUE;
}


BOOL SvcDelete()
{
    SC_HANDLE schSCManager;
    SC_HANDLE schService;

    // Get a handle to the SCM database. 

    schSCManager = OpenSCManager(
        NULL,                    // local computer
        NULL,                    // ServicesActive database 
        SC_MANAGER_ALL_ACCESS);  // full access rights 

    if (NULL == schSCManager)
    {
        printf("OpenSCManager failed (%d)\n", GetLastError());
        return FALSE;
    }

    // Get a handle to the service.

    schService = OpenService(
        schSCManager,       // SCM database 
        L"psnotify",       // name of service 
        DELETE);            // need delete access 

    if (schService == NULL)
    {
        printf("OpenService failed (%d)\n", GetLastError());
        CloseServiceHandle(schSCManager);
        return FALSE;
    }

    // Delete the service.

    if (!DeleteService(schService))
    {
        printf("DeleteService failed (%d)\n", GetLastError());
    }
    else printf("Service deleted successfully\n");

    CloseServiceHandle(schService);
    CloseServiceHandle(schSCManager);
    return TRUE;
}

ProcessNotificationMessageHandler g_handler;

// Handler function will be called on separate thread!
static BOOL WINAPI console_ctrl_handler(DWORD dwCtrlType)
{
    switch (dwCtrlType)
    {
    case CTRL_C_EVENT: // Ctrl+C
    case CTRL_BREAK_EVENT: // Ctrl+Break
    case CTRL_CLOSE_EVENT: // Closing the console window
    case CTRL_LOGOFF_EVENT: // User logs off. Passed only to services!
    case CTRL_SHUTDOWN_EVENT: // System is shutting down. Passed only to services!
        g_handler.Close();
        SvcStop();
        SvcDelete();
        break;
    }

    return FALSE;
}

int wmain(int argc, const wchar_t* argv[])
{ 
    BOOL bDumpAll = FALSE;
    if (argc > 1)
        bDumpAll = TRUE;

    if (SvcInstall())
    {
        if (!SvcStart())
        {
            SvcDelete();
            printf("Cannot start service, press any key to close");
            std::cin.ignore();
            return 1;
        }
    }
    else
    {
        printf("Cannot install, press any key to close");
        std::cin.ignore();
        return 1;
    }

    
    if (!g_handler.Initialize(bDumpAll))
        return 1;

    SetConsoleCtrlHandler(console_ctrl_handler, TRUE);
    
    g_handler.Run();
    printf("Waiting for messages. Press Ctrl-C to exit\n");

    Sleep(INFINITE);

    return 0;
}