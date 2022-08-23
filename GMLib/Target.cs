using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime;

namespace GMLib
{

    public class TargetEventArgs : EventArgs
    {
        public object Obj { get; set; }
        public bool Stop { get; set; }
        public TargetEventArgs(object obj)
        {
            Obj = obj;
        }
    }

    // Target is the basic unit of data collection, presenting
    // single process "snapshot", independent of previous results
    public class Target
    {
        public readonly List<GMAppDomain> AppDomains = new();
        public readonly List<GMModule> Modules = new();
        public readonly List<GMObject> Objects = new();
        public readonly List<GMStack> Stacks = new();
        public readonly List<GMRuntime> Runtimes = new();
        public readonly List<GMHandle> Handles = new();
        public readonly List<GMThread> Threads = new();
        public readonly List<GMFrame> Frames = new();
        public readonly List<GMRef> Refs = new();
        public object Ctx { get; set; }
        public DataTarget Dt { get; set; }
        public int Pid { get; set; }
        public int Id { get; set; }
        public long Time { get; set; }
        public uint Flags { get; set; }

        private ulong objectId = 0;

        private ulong heapTotal = 0;

        private Dictionary<ulong, ulong> objectIndex = new();

        public event EventHandler<TargetEventArgs> DataEventHandler;

        public event EventHandler<DebugEventArgs> DebugEventHandler;

        void DbgMsg(string msg)
        {
            DebugEventHandler?.Invoke(this, new DebugEventArgs(msg));
        }
        public Target(DataTarget dt, long time, int id, int pid = 0, uint flags = Constants.COLLECT_EVERYTHING)
        {
            Dt = dt;
            Time = time;
            Pid = pid;
            Id = id;
            Flags = flags;
        }
        public void Close()
        {
            Dt.Dispose();
        }

        public void Collect()
        {
            CollectInternal();
        }
        private void CollectInternal()
        {
            DataEventHandler?.Invoke(this, new TargetEventArgs("TARGET_START"));

            // No runtimes present
            if (Dt.ClrVersions.Length == 0)
                throw new("No runtime present.");
            if (((Flags & Constants.COLLECT_RUNTIMES) != 0) && !CollectRuntimes())
                throw new("Collection or runtimes was canceled.");
            foreach (ClrInfo version in Dt.ClrVersions)
            {
                ClrRuntime rt = version.CreateRuntime();
                if (((Flags & Constants.COLLECT_APPDOMAINS) != 0) && !CollectDomains(rt))
                    throw new("Collection of domains was canceled.");
                if (((Flags & Constants.COLLECT_HEAP) != 0) && !CollectHeap(rt))
                    throw new("Collection of heap objects was canceled.");
                if (((Flags & Constants.COLLECT_THREADS) != 0) && !CollectThreads(rt))
                    throw new("Collection of threads was canceled .");
                if (((Flags & Constants.COLLECT_STACK) != 0) && !CollectStack(rt))
                    throw new("Collection of stack objects was canceled.");
                if (((Flags & Constants.COLLECT_HANDLES) != 0) && !CollectHandles(rt))
                    throw new("Collection of handles was canceled.");

                // Index stacks
                if (Stacks.Count != 0)
                {
                    DbgMsg("Indexing stack");
                    for (int i = Stacks.Count - 1; i >= 0; i--)
                    {
                        if (objectIndex.ContainsKey(Stacks[i].Object))
                            Stacks[i].Object = objectIndex[Stacks[i].Object];
                        else
                        {
                            DbgMsg($"Removing duplicate stack object @{i}");
                            Stacks.RemoveAt(i);
                        }
                    }
                }
                // Index handles
                if (Handles.Count != 0)
                {
                    DbgMsg("Indexing handles");
                    for (int i = Handles.Count - 1; i >= 0; i--)
                    {
                        if (objectIndex.ContainsKey(Handles[i].Object))
                            Handles[i].Object = objectIndex[Handles[i].Object];
                        else
                        {
                            DbgMsg($"Removing duplicate heap object @{i}");
                            Handles.RemoveAt(i);
                        }
                    }
                }
                // Index refs
                if (Refs.Count != 0)
                {
                    DbgMsg("Indexing refs");
                    for (int i = Refs.Count - 1; i >= 0; i--)
                    {
                        if (objectIndex.ContainsKey(Refs[i].Address))
                            Refs[i].Address = objectIndex[Refs[i].Address];
                        else
                        {
                            DbgMsg($"Removing duplicate ref object @{i}");
                            Refs.RemoveAt(i);
                            continue;
                        }
                        if (objectIndex.ContainsKey(Refs[i].Ref))
                            Refs[i].Ref = objectIndex[Refs[i].Ref];
                        else
                        {
                            DbgMsg($"Removing duplicate ref object @{i}");
                            Refs.RemoveAt(i);
                        }
                    }
                }
            }
            DataEventHandler?.Invoke(this, new TargetEventArgs("TARGET_STOP"));
        }
        private bool CollectRuntimes()
        {
            DbgMsg("Collecting runtimes");

            foreach (ClrInfo version in Dt.ClrVersions)
            {
                GMRuntime gmRuntime = new GMRuntime {
                    Id = Id,
                    Version = version.Version.ToString(),
                    Dac = version.DacInfo.PlatformSpecificFileName,
                    Arch = version.DacInfo.TargetArchitecture.ToString()
                };

                Runtimes.Add(gmRuntime);
                TargetEventArgs args = new(gmRuntime);
                DataEventHandler?.Invoke(this, args);
                if (args.Stop)
                {
                    return false;
                }
            }
            return true;
        }
        private bool CollectDomains(ClrRuntime rt)
        {
            DbgMsg("Collecting appdomains and modules");

            foreach (ClrAppDomain domain in rt.AppDomains)
            {
                GMAppDomain gmAppdomain = new GMAppDomain
                {
                    Id = Id,
                    Aid = domain.Id,
                    Name = domain.Name,
                    Address = domain.Address
                };

                AppDomains.Add(gmAppdomain);
                TargetEventArgs args = new(gmAppdomain);
                DataEventHandler?.Invoke(this, args);
                if (args.Stop)
                {
                    return false;
                }
                if ((Flags & Constants.COLLECT_MODULES) != 0)
                {
                    foreach (ClrModule module in domain.Modules)
                    {
                        // XXX: AppDomain id (Aid) as a module key !!
                        GMModule gmModule = new GMModule
                        {
                            Id = Id,
                            AsmAddress = module.AssemblyAddress,
                            ImgAddress = module.ImageBase,
                            Name = module.Name,
                            AsmName = module.AssemblyName,
                            IsDynamic = module.IsDynamic,
                            IsPe = module.IsPEFile
                        };

                        Modules.Add(gmModule);
                        args = new(gmModule);
                        DataEventHandler?.Invoke(this, args);
                        if (args.Stop)
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }
        private bool CollectThreads(ClrRuntime rt)
        {
            DbgMsg("Collecting threads");

            foreach (ClrThread thread in rt.Threads)
            {
                if (!thread.IsAlive)
                    continue;

                GMThread gmThread = new GMThread
                {
                    Id = Id,
                    Tid = thread.OSThreadId,
                    Context = new byte[2048]
                };

                // Get the thread context
                uint contextFlags = Constants.CONTEXT_FLAGS_32;
                if (Dt.DataReader.PointerSize == 8)
                    contextFlags = Constants.CONTEXT_FLAGS_64;
                Dt.DataReader.GetThreadContext(thread.OSThreadId, contextFlags, gmThread.Context);

                Threads.Add(gmThread);

                foreach (ClrStackFrame frame in thread.EnumerateStackTrace())
                {
                    GMFrame gmFrame = new GMFrame
                    {
                        Id = Id,
                        Tid = thread.OSThreadId,
                        StackPtr = frame.StackPointer,
                        Frame = frame.ToString(),
                        Ip = frame.InstructionPointer
                    };

                    Frames.Add(gmFrame);
                    TargetEventArgs args = new(gmFrame);
                    DataEventHandler?.Invoke(this, args);
                    if (args.Stop)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private bool ObjectExists(ulong addr)
        {
            foreach (GMObject obj in Objects)
            {
                if (obj.Address == addr)
                {
                    return true;
                }
            }
            return false;
        }
        private bool CollectStack(ClrRuntime rt)
        {
            DbgMsg("Collecting stack objects");

            //long objectId = 0;
            //List<ulong> unique = new();

            foreach (ClrThread thread in rt.Threads)
            {
                if (!thread.IsAlive)
                    continue;

                // We'll need heap data to find objects on the stack.
                ClrHeap heap = rt.Heap;

                // Walk each pointer aligned address on the stack.  Note that StackBase/StackLimit
                // is exactly what they are in the TEB.  This means StackBase > StackLimit on AMD64.
                ulong start = thread.StackBase;
                ulong stop = thread.StackLimit;

                // We'll walk these in pointer order.
                if (start > stop)
                {
                    ulong tmp = start;
                    start = stop;
                    stop = tmp;
                }

                long stackId = 0;

                // Walk each pointer aligned address.  Ptr is a stack address.
                for (ulong ptr = start; ptr <= stop; ptr += (uint)IntPtr.Size)
                {
                    // Read the value of this pointer.  If we fail to read the memory, break. The
                    // stack region should be in the crash dump.
                    if (!Dt.DataReader.ReadPointer(ptr, out ulong obj))
                        break;

                    // We check to see if this address is a valid object by simply calling
                    // GetObject. If that returns null type, it's not an object.
                    ClrObject stackObj = heap.GetObject(obj);
                    if (stackObj.Type == null)
                    {
                        continue;
                    }

                    GMStack gmStack = new GMStack
                    {
                        Id = Id,
                        Tid = thread.OSThreadId,
                        StackPtr = ptr,
                        Object = obj
                    };
                    Stacks.Add(gmStack);

                    // If heap collection is not enabled, collect objects referenced by/to heap
                    // We need to always check for duplicates because objects in stack are not unique:
                    // they can reside in many stack slots or point to heap

                    if (!ObjectExists(obj))
                    {
                        List<ulong> uniqueRefs = new();
                        if ((Flags & Constants.COLLECT_REFS) != 0)
                        {
                            IEnumerable<ClrObject> refs = null;
                            try
                            {
                                refs = stackObj.EnumerateReferences(carefully: true, considerDependantHandles: true);
                                foreach (ClrObject refObj in refs)
                                {
                                    if (refObj.IsValid && !uniqueRefs.Contains(refObj.Address))
                                    {
                                        uniqueRefs.Add(refObj.Address);
                                        int refcount = 0;
                                        foreach (ulong addr in uniqueRefs)
                                        {
                                            if (!ObjectExists(addr))
                                            {
                                                GMObject gmRefObj = new(refObj, objectId, Dt.DataReader.PointerSize);
                                                Objects.Add(gmRefObj);
                                                TargetEventArgs refObjArgs = new(gmRefObj);
                                                DataEventHandler?.Invoke(this, refObjArgs);
                                                if (refObjArgs.Stop)
                                                {
                                                    return false;
                                                }
                                                UpdateObjectIndex(refObj.Address);
                                                GMRef gmRef = new GMRef
                                                {
                                                    Id = Id,
                                                    Address = stackObj.Address,
                                                    Ref = addr
                                                };
                                                Refs.Add(gmRef);
                                            }
                                            if (refcount > Constants.MAX_INT_REFCOUNT)
                                                break;
                                            refcount++;
                                        }
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                DbgMsg(e.Message);
                            }
                            GMObject gmStackObj = new(stackObj, objectId, Dt.DataReader.PointerSize);
                            Objects.Add(gmStackObj);
                            TargetEventArgs objArgs = new(gmStackObj);
                            DataEventHandler?.Invoke(this, objArgs);
                            if (objArgs.Stop)
                            {
                                return false;
                            }
                            UpdateObjectIndex(stackObj.Address);
                        }
                    }

                    TargetEventArgs args = new(gmStack);
                    DataEventHandler?.Invoke(this, args);
                    if (args.Stop)
                    {
                        return false;
                    }
                    stackId++;
                }
            }
            DbgMsg($"Collected total of {objectId-heapTotal} stack items");
            return true;
        }
        private bool CollectHandles(ClrRuntime rt)
        {
            DbgMsg("Collecting handles");

            foreach (ClrHandle handle in rt.EnumerateHandles())
            {
                GMHandle gmHandle = new GMHandle
                {
                    Id = Id,
                    Address = handle.Address,
                    Object = handle.Object.Address,
                    Kind = handle.HandleKind.ToString()
                };

                Handles.Add(gmHandle);
                TargetEventArgs args = new(gmHandle);
                DataEventHandler?.Invoke(this, args);
                if (args.Stop)
                {
                    return false;
                }
            }
            return true;
        }

        private bool CollectHeap(ClrRuntime rt)
        {
            if (!rt.Heap.CanWalkHeap)
                return true;

            DbgMsg("Collecting heap objects");

            foreach (ClrSegment seg in rt.Heap.Segments)
            {
                foreach (ClrObject obj in seg.EnumerateObjects())
                {
                    if (!obj.IsValid)
                        continue;

                    // Collect references is required
                    List<ulong> unique = new();
                    if ((Flags & Constants.COLLECT_REFS) != 0)
                    {
                        IEnumerable<ClrObject> refs = null;
                        // This fails sometimes on parsing errors
                        try
                        {
                            refs = obj.EnumerateReferences(carefully: true, considerDependantHandles: true);
                            int refcount = 0;
                            foreach (ClrObject refObj in refs)
                            {
                                if (refObj.IsValid)
                                {
                                    if (!unique.Contains(refObj.Address))
                                        unique.Add(refObj.Address);

                                }
                                if (refcount > Constants.MAX_INT_REFCOUNT)
                                    break;
                                refcount++;
                            }
                        }
                        catch (Exception e) 
                        {
                            DbgMsg(e.Message);
                        }
                        foreach (ulong addr in unique)
                        {
                            GMRef gmRef = new GMRef
                            {
                                Id = Id,
                                Address = obj.Address,
                                Ref = addr
                            };
                            Refs.Add(gmRef);
                        }
                    }
                    GMObject gmObj = new(obj, objectId, Dt.DataReader.PointerSize);
                    Objects.Add(gmObj);

                    // XXX: this doesn't contain data anymore - data reading is done by DataBase.AddTarget()
                    // Maybe it is up to client?
                    TargetEventArgs args = new(gmObj);
                    DataEventHandler?.Invoke(this, args);
                    if (args.Stop)
                    {
                        return false;
                    }
                    objectIndex.Add(obj.Address, objectId);
                    objectId++;
                }
            }
            if (objectId > 0)
                heapTotal = objectId-1;
            DbgMsg($"Collected total of {heapTotal} heap items");
            return true;
        }
        private void UpdateObjectIndex(ulong addr)
        {
            if (!objectIndex.ContainsKey(addr))
            {
                objectIndex.Add(addr, objectId);
                objectId++;
            }
        }
    }
}
