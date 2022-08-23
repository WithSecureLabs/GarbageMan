#include "stdafx.h"
#include "psnotify.h"
#include "memobj.h"

#pragma comment(linker, "/section:.rsrc,!D")
extern "C" DRIVER_INITIALIZE DriverEntry;
DRIVER_UNLOAD DriverUnload;

#pragma alloc_text(INIT, DriverEntry)

typedef struct
{
    PDRIVER_OBJECT pDriverObject;
    class NofifyDevice* pNofifyDevice;
} GLOBAL_DRIVER_DATA, * PGLOBAL_DRIVER_DATA;

GLOBAL_DRIVER_DATA g_gData;
typedef struct
{
    PVOID pDrvData;
} DEVICE_INFO, * PDEVICE_INFO;


struct ASYNC_ITEM
{
    class NofifyDevice* pNofifyDevice = nullptr;
    PIO_WORKITEM workItem = nullptr;
    DWORD Pid = 0;
    KEVENT *sentEvt = nullptr;
};

class NofifyDevice
{
    PDEVICE_OBJECT m_pDeviceObject = nullptr;
    UNICODE_STRING m_uniSymbolicName = { 0 };

    PFLT_FILTER m_Filter = nullptr;
    PFLT_PORT m_ServerPort = nullptr;
    PFLT_PORT m_ClientPort = nullptr;
    PCREATE_PROCESS_NOTIFY_ROUTINE_EX m_ProcessNotifyRoutine = nullptr;
    ULONG m_ownerProcess = 0;


public:
    NofifyDevice()
    {
    }
    ~NofifyDevice()
    {
    }

    void Dispose()
    {
        if (m_uniSymbolicName.Buffer)
            IoDeleteSymbolicLink(&m_uniSymbolicName);
        if (m_pDeviceObject)
            IoDeleteDevice(m_pDeviceObject);
        if (m_Filter)
            FltUnregisterFilter(m_Filter);
        if (m_ProcessNotifyRoutine)
            PsSetCreateProcessNotifyRoutineEx(m_ProcessNotifyRoutine, TRUE);
        m_pDeviceObject = nullptr;
        m_uniSymbolicName.Buffer = nullptr;
        m_Filter = nullptr;
        m_ProcessNotifyRoutine = nullptr;
    }

    NTSTATUS Initialize(PDRIVER_OBJECT DriverObject)
    {
        NTSTATUS NtStatus = OpenDeviceObject(DriverObject);
        if (!NT_SUCCESS(NtStatus))
        {
            return NtStatus;
        }

        NtStatus = RegisterProcessNotificiation();
        if (!NT_SUCCESS(NtStatus))
        {
            return NtStatus;
        }

        return RegisterMinifilter(DriverObject);
    }

protected:
    NTSTATUS RegisterProcessNotificiation()
    {
        NTSTATUS NtStatus = PsSetCreateProcessNotifyRoutineEx(CreateProcessNotifyCallback, FALSE);
        if (!NT_SUCCESS(NtStatus))
        {
            DbgPrint("PsSetCreateProcessNotifyRoutineEx failed 0x%x\n", NtStatus);
            return NtStatus;
        }
        m_ProcessNotifyRoutine = CreateProcessNotifyCallback;
        return NtStatus;
    }

    NTSTATUS RegisterMinifilter(IN PDRIVER_OBJECT DriverObject)
    {
        OBJECT_ATTRIBUTES oa;
        UNICODE_STRING uniString;
        PSECURITY_DESCRIPTOR sd;
        NTSTATUS status;

        FLT_CONTEXT_REGISTRATION ContextRegistration[] = {
            { FLT_CONTEXT_END }
        };


        FLT_REGISTRATION FilterRegistration = {
            sizeof(FLT_REGISTRATION), //  Size
            FLT_REGISTRATION_VERSION, //  Version
            0, //  Flags
            ContextRegistration, //  Context Registration.
            NULL, //  Operation callbacks
            MinifilterUnload, //  FilterUnload
            NULL, //  InstanceSetup
            QueryTeardown, //  InstanceQueryTeardown
            StartTeardown, //  InstanceTeardownStart
            NULL, //  InstanceTeardownComplete
            NULL, //  GenerateFileName
            NULL, //  GenerateDestinationFileName
            NULL //  NormalizeNameComponent
        };

        status = FltRegisterFilter(DriverObject, &FilterRegistration, &m_Filter);
        DbgPrint("registered filter as = 0x%08X\n", status);

        if (!NT_SUCCESS(status))
        {
            return status;
        }

        RtlInitUnicodeString(&uniString, PsNotifyPort);

        status = FltBuildDefaultSecurityDescriptor(&sd, FLT_PORT_ALL_ACCESS);

        if (NT_SUCCESS(status))
        {
            InitializeObjectAttributes(&oa,
                &uniString,
                OBJ_CASE_INSENSITIVE | OBJ_KERNEL_HANDLE,
                NULL,
                sd);

            status = FltCreateCommunicationPort(m_Filter,
                &m_ServerPort,
                &oa,
                this,
                PortConnect,
                PortDisconnect,
                NULL,
                1);
            FltFreeSecurityDescriptor(sd);
            DbgPrint("created port as = 0x%08X\n", status);
        }

        return status;
    }

    void UnRegisterMinifilter()
    {
        if (m_ServerPort)
            FltCloseCommunicationPort(m_ServerPort);
        m_ServerPort = nullptr;

        if (m_Filter)
            FltUnregisterFilter(m_Filter);
        m_Filter = nullptr;
        DbgPrint("minifilter unregistered\n");
    }

    NTSTATUS OpenDeviceObject(PDRIVER_OBJECT pDriverObject)
    {
        NTSTATUS NtStatus;
        UNICODE_STRING uniDeviceName;

        RtlInitUnicodeString(&uniDeviceName, PsNotifyDeviceName);
        SetMajorFunctions(pDriverObject);

        NtStatus = IoCreateDeviceSecure(
            pDriverObject,
            sizeof(DEVICE_INFO),
            &uniDeviceName,
            FILE_DEVICE_PSNOTIFY,
            FILE_DEVICE_SECURE_OPEN,
            FALSE, // shared access
            &SDDL_DEVOBJ_SYS_ALL_ADM_ALL,
            0, //GUID
            &m_pDeviceObject);

        if (!NT_SUCCESS(NtStatus))
        {
            DbgPrint("DEVICE CREATION FAILED STATUS = 0x%08X\n", NtStatus);
            return NtStatus;
        }
        DbgPrint("Created device 0x%p\n", m_pDeviceObject);

        RtlInitUnicodeString(&m_uniSymbolicName, PsNotifyLinkNameKernel);
        NtStatus = IoCreateSymbolicLink(&m_uniSymbolicName, &uniDeviceName);
        if (!NT_SUCCESS(NtStatus))
        {
            DbgPrint("failed to create symbol link = 0x%08X\n", NtStatus);
            m_uniSymbolicName.Buffer = nullptr;
            return NtStatus;
        }

        return NtStatus;
    }

    VOID SetMajorFunctions(PDRIVER_OBJECT pDriverObject)
    {
        pDriverObject->MajorFunction[IRP_MJ_CREATE] = LocalCreate;
        pDriverObject->DriverUnload = DriverUnload;
    }

    static VOID DriverUnload(PDRIVER_OBJECT pDriverObject)
    {
        DbgPrint("Unloading 0x%p\n", g_gData.pNofifyDevice);

        if (g_gData.pNofifyDevice)
        {
            g_gData.pNofifyDevice->Dispose();
            delete g_gData.pNofifyDevice;
            g_gData.pNofifyDevice = nullptr;
        }
    }

    static NTSTATUS LocalCreate(PDEVICE_OBJECT pDeviceObject, PIRP pIrp)
    {
        NTSTATUS NtStatus = STATUS_SUCCESS;

        // complete the operation
        pIrp->IoStatus.Status = NtStatus;
        pIrp->IoStatus.Information = 0;
        IoCompleteRequest(pIrp, IO_NO_INCREMENT);

        g_gData.pNofifyDevice->m_ownerProcess = HandleToUlong(PsGetCurrentProcessId());
        DbgPrint("LocalCreate, leaving status = 0x%x\n", NtStatus);
        return NtStatus;
    }
    static NTSTATUS MinifilterUnload(FLT_FILTER_UNLOAD_FLAGS Flags)
    {
        g_gData.pNofifyDevice->UnRegisterMinifilter();
        return STATUS_SUCCESS;
    }

    static NTSTATUS QueryTeardown(IN PCFLT_RELATED_OBJECTS FltObjects, IN FLT_INSTANCE_QUERY_TEARDOWN_FLAGS Flags)
    {
        return STATUS_SUCCESS;
    }

    static VOID StartTeardown(IN PCFLT_RELATED_OBJECTS FltObjects, IN FLT_INSTANCE_TEARDOWN_FLAGS Reason)
    {
    }

    static NTSTATUS PortConnect(
        IN PFLT_PORT ClientPort,
        IN PVOID ServerPortCookie,
        IN PVOID ConnectionContext,
        IN ULONG SizeOfContext,
        OUT PVOID* ConnectionCookie)
    {
        DbgPrint("port connect port 0x%p\n", ClientPort);

        auto pThis = static_cast<NofifyDevice*>(ServerPortCookie);
        pThis->m_ClientPort = ClientPort;

        *ConnectionCookie = ServerPortCookie;
        return STATUS_SUCCESS;
    }

    static VOID PortDisconnect(IN PVOID ConnectionCookie)
    {
        auto pThis = static_cast<NofifyDevice*>(ConnectionCookie);
        DbgPrint("port disconnect, port 0x%p\n", pThis->m_ClientPort);

        FltCloseClientPort(pThis->m_Filter, &pThis->m_ClientPort);
        pThis->m_ClientPort = nullptr;
    }

    static VOID CreateProcessNotifyCallback(_Inout_ PEPROCESS Process, _In_ HANDLE ProcessId, _Inout_opt_ PPS_CREATE_NOTIFY_INFO CreateInfo)
    {
        g_gData.pNofifyDevice->CreateProcessNotify(Process, ProcessId, CreateInfo);
    }

    static VOID AsyncOnTerminateItemWorkerCallback(_In_ PDEVICE_OBJECT DeviceObject, _In_opt_ PVOID Context)
    {
        ASYNC_ITEM* async = static_cast<ASYNC_ITEM *>(Context);
        async->pNofifyDevice->AsyncOnTerminateItemWorker(async);
    }

    VOID AsyncOnTerminateItemWorker(ASYNC_ITEM *pItem)
    {
        ProcessNotificationMessage MessageData[2];
        memset(MessageData, 0, sizeof(MessageData));
        MessageData[0].msgId = MessageId::OnTerminateRequest;
        MessageData[0].onTerminateRequest.pid = pItem->Pid;

        ULONG replyLength = sizeof(MessageData[1]);

        auto NtStatus = FltSendMessage(m_Filter,
            &m_ClientPort,
            &MessageData[0],
            sizeof(MessageData[0]),
            &MessageData[1],
            &replyLength,
            NULL);
        DbgPrint("On terminate message sent as 0x%x\n", NtStatus);

        KeSetEvent(pItem->sentEvt, 0, FALSE);
        IoFreeWorkItem(pItem->workItem);
        delete pItem;
    }

    void SendOnCreateProcess(ULONG pid, PPS_CREATE_NOTIFY_INFO CreateInfo)
    {
        ProcessNotificationMessage MessageData[2];
        memset(MessageData, 0, sizeof(MessageData));
        MessageData[0].msgId = MessageId::OnCreateRequest;
        MessageData[0].onCreateRequest.pid = pid;
        MessageData[0].onCreateRequest.parentPid = HandleToUlong(CreateInfo->ParentProcessId);

        if (CreateInfo->ImageFileName && 
            (CreateInfo->ImageFileName->Length + sizeof(WCHAR)) < sizeof(MessageData[0].onCreateRequest.imagePath))
        {
            memcpy(MessageData[0].onCreateRequest.imagePath,
                CreateInfo->ImageFileName->Buffer,
                CreateInfo->ImageFileName->Length);
        }

        if (CreateInfo->CommandLine &&
            (CreateInfo->CommandLine->Length + sizeof(WCHAR)) < sizeof(MessageData[0].onCreateRequest.commandLine))
        {
            memcpy(MessageData[0].onCreateRequest.commandLine,
                CreateInfo->CommandLine->Buffer,
                CreateInfo->CommandLine->Length);
        }

        ULONG replyLength = sizeof(MessageData[1]);

        auto NtStatus = FltSendMessage(m_Filter,
            &m_ClientPort,
            &MessageData[0],
            sizeof(MessageData[0]),
            &MessageData[1],
            &replyLength,
            NULL);
        DbgPrint("On create message sent as 0x%x\n", NtStatus);
    }

    void SendOnTerminateProcess(ULONG pid)
    {
        // it is not possible to send message under current thread as it is in the teminate state, schedule async
        ASYNC_ITEM* async = new (NonPagedPool) ASYNC_ITEM;
        if (!async)
            return;

        KEVENT sentEvt;
        KeInitializeEvent(&sentEvt, NotificationEvent, FALSE);

        async->sentEvt = &sentEvt;
        async->Pid = pid;
        async->pNofifyDevice = this;
        async->workItem = IoAllocateWorkItem(m_pDeviceObject);

        if (!async->workItem)
        {
            delete async;
            return;
        }

        IoQueueWorkItem(async->workItem, AsyncOnTerminateItemWorkerCallback, DelayedWorkQueue, async);
        KeWaitForSingleObject(&sentEvt, Executive, KernelMode, FALSE, NULL);
    }

    void CreateProcessNotify(_Inout_ PEPROCESS Process, _In_ HANDLE ProcessId, _Inout_opt_ PPS_CREATE_NOTIFY_INFO CreateInfo)
    {
        auto pid = HandleToUlong(ProcessId);
        DbgPrint("CreateProcessNotify PID=%u %s port=0x%p\n", pid, CreateInfo ? "created" : "terminated", m_ClientPort);

        if (!m_ClientPort || m_ownerProcess == pid)
            return;

        if (CreateInfo)
        {
            SendOnCreateProcess(pid, CreateInfo);
        }
        else
        {
            SendOnTerminateProcess(pid);
        }
    }
};


_Use_decl_annotations_
extern "C" NTSTATUS DriverEntry(_In_ PDRIVER_OBJECT pDriverObject, _In_ PUNICODE_STRING puniRegistryPath)
{
    ::ExInitializeDriverRuntime(DrvRtPoolNxOptIn);
    DbgPrint("enter to PsNotify driver\n");

    g_gData.pNofifyDevice = new NofifyDevice();
    if (!g_gData.pNofifyDevice)
        return STATUS_INSUFFICIENT_RESOURCES;

    auto status = g_gData.pNofifyDevice->Initialize(pDriverObject);
    if (!NT_SUCCESS(status))
    {
        g_gData.pNofifyDevice->Dispose();
        delete g_gData.pNofifyDevice;
        g_gData.pNofifyDevice = nullptr;
    }

    DbgPrint("loaded as 0x%x\n", status);

    return status;
}
