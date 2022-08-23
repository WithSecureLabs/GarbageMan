#pragma once

#define FILE_DEVICE_PSNOTIFY 504919
const WCHAR PsNotifyLinkNameKernel[] = L"\\DosDevices\\psnotify";
const WCHAR PsNotifyDeviceName[] = L"\\Device\\psnotify";
const WCHAR PsNotifyLinkNameDos[] = L"\\\\.\\psnotify";
const WCHAR PsNotifyPort[] = L"\\psnotifyPort";

enum class MessageId : UINT
{
    Unset,
    OnCreateRequest,
    OnCreateResponse,
    OnTerminateRequest,
    OnTerminateResponse,
};

struct ProcessNotificationMessageOnProcessCreateRequest
{
    DWORD pid = 0;
    DWORD parentPid = 0;
    WCHAR imagePath[1024];
    WCHAR commandLine[1024];
};

struct ProcessNotificationMessageOnProcessCreateResponse
{
    DWORD status = 0;
};

struct ProcessNotificationMessageOnProcessTerminateRequest
{
    DWORD pid = 0;
};

struct ProcessNotificationMessageOnProcessTerminateResponse
{
    DWORD status = 0;
};


struct ProcessNotificationMessage
{
    MessageId msgId = MessageId::Unset;
    union {
        ProcessNotificationMessageOnProcessCreateRequest onCreateRequest;
        ProcessNotificationMessageOnProcessCreateResponse onCreateResponse;
        ProcessNotificationMessageOnProcessTerminateRequest onTerminateRequest;
        ProcessNotificationMessageOnProcessTerminateResponse onTerminateResponse;
    };
    ProcessNotificationMessage() {}
};


