#include "stdafx.h"
#include "memobj.h"

#define __DEFAULT_TAG__ 'fnsP'


__drv_allocatesMem(Mem)
    _When_((PoolType & PagedPool) != 0, _IRQL_requires_max_(APC_LEVEL))
        _When_((PoolType & PagedPool) == 0, _IRQL_requires_max_(DISPATCH_LEVEL))
            _When_((PoolType & NonPagedPoolMustSucceed) != 0,
                __drv_reportError("Must succeed pool allocations are forbidden. "
                                  "Allocation failures cause a system crash"))
                _When_((PoolType & (NonPagedPoolMustSucceed | POOL_RAISE_IF_ALLOCATION_FAILURE)) == 0,
                    _Post_maybenull_ _Must_inspect_result_)
                    _When_((PoolType & (NonPagedPoolMustSucceed | POOL_RAISE_IF_ALLOCATION_FAILURE)) != 0,
                        _Post_notnull_)
                        _Post_writable_byte_size_(NumberOfBytes) void* __cdecl
                        operator new(size_t NumberOfBytes, POOL_TYPE PoolType)
{
    return ::ExAllocatePoolWithTag(PoolType, NumberOfBytes, __DEFAULT_TAG__);
}

__drv_allocatesMem(Mem)
    _When_((PoolType & PagedPool) != 0, _IRQL_requires_max_(APC_LEVEL))
        _When_((PoolType & PagedPool) == 0, _IRQL_requires_max_(DISPATCH_LEVEL))
            _When_((PoolType & NonPagedPoolMustSucceed) != 0,
                __drv_reportError("Must succeed pool allocations are forbidden. "
                                  "Allocation failures cause a system crash"))
                _When_((PoolType & (NonPagedPoolMustSucceed | POOL_RAISE_IF_ALLOCATION_FAILURE)) == 0,
                    _Post_maybenull_ _Must_inspect_result_)
                    _When_((PoolType & (NonPagedPoolMustSucceed | POOL_RAISE_IF_ALLOCATION_FAILURE)) != 0,
                        _Post_notnull_)
                        _Post_writable_byte_size_(NumberOfBytes) void *__cdecl
                        operator new[](size_t NumberOfBytes, POOL_TYPE PoolType)
{
    return ::ExAllocatePoolWithTag(PoolType, NumberOfBytes, __DEFAULT_TAG__);
}

void __cdecl operator delete(void *ptr, POOL_TYPE PoolType)
{
    ::ExFreePoolWithTag(ptr, __DEFAULT_TAG__);
}

void __cdecl operator delete[](void *ptr, POOL_TYPE PoolType)
{
    ::ExFreePoolWithTag(ptr, __DEFAULT_TAG__);
}

void *__cdecl operator new(size_t ulSize)
{
    return ::ExAllocatePoolWithTag(PagedPool, ulSize, __DEFAULT_TAG__);
}

void __cdecl operator delete(void *ptr)
{
    if (!ptr)
        return;
    return ::ExFreePoolWithTag(ptr, __DEFAULT_TAG__);
}

void *__cdecl operator new(size_t /*ulSize*/, void *ptr)
{
    return ptr;
}

void __cdecl operator delete(void * /*ptr*/, void * /*ptr2*/)
{
    //    *(int*)0 = 0;
}

void *__cdecl operator new[](size_t ulSize)
{
    return ::ExAllocatePoolWithTag(PagedPool, ulSize, __DEFAULT_TAG__);
}

void __cdecl operator delete[](void *ptr)
{
    if (!ptr)
        return;
    return ::ExFreePoolWithTag(ptr, __DEFAULT_TAG__);
}

void* __cdecl operator new(size_t NumberOfBytes, POOL_TYPE PoolType, ULONG Tag)
{
    return ::ExAllocatePoolWithTag(PoolType, NumberOfBytes, Tag);
}

void* __cdecl operator new[](size_t NumberOfBytes, POOL_TYPE PoolType, ULONG Tag)
{
    return ::ExAllocatePoolWithTag(PoolType, NumberOfBytes, Tag);
}

void __cdecl operator delete(void* ptr, POOL_TYPE PoolType, ULONG Tag)
{
    if (ptr)
        ::ExFreePool(ptr);
}

void __cdecl operator delete[](void* ptr, POOL_TYPE PoolType, ULONG Tag)
{
    if(ptr) 
        ::ExFreePool(ptr);
}

void *__cdecl operator new[](size_t /*ulSize*/, void *ptr)
{
    return ptr;
}

void __cdecl operator delete[](void * /*ptr*/, void * /*ptr2*/)
{
    //    *(int*)0 = 0;
}

void __cdecl operator delete(void* ptr, size_t /*sz*/) // C++14
{
    if (!ptr)
        return;
    return ::ExFreePoolWithTag(ptr, __DEFAULT_TAG__);
}

void __cdecl operator delete[](void* ptr, size_t /*sz*/) // C++14
{
    if (!ptr)
        return;
    return ::ExFreePoolWithTag(ptr, __DEFAULT_TAG__);
}
