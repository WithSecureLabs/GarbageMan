#pragma once

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
                        operator new(size_t NumberOfBytes, POOL_TYPE PoolType);

void __cdecl operator delete(void *ptr, POOL_TYPE PoolType);

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
                        operator new[](size_t NumberOfBytes, POOL_TYPE PoolType);

void __cdecl operator delete[](void *ptr, POOL_TYPE PoolType);


void* __cdecl operator new(size_t NumberOfBytes, POOL_TYPE PoolType, ULONG Tag);
void* __cdecl operator new[](size_t NumberOfBytes, POOL_TYPE PoolType, ULONG Tag);
void __cdecl operator delete(void* ptr, POOL_TYPE PoolType, ULONG Tag);
void __cdecl operator delete[](void* ptr, POOL_TYPE PoolType, ULONG Tag);
