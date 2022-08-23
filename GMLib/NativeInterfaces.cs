using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using PInvoke;


namespace GMLib
{
    /// <summary>
    /// Enumerates objects with the IUnknown interface. It can be used to enumerate through the objects in a component containing multiple objects.
    /// 
    /// </summary>
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("00000100-0000-0000-C000-000000000046")]
    [ComImport]
    public interface IEnumUnknown
    {
        /// <summary>
        /// Retrieves the specified number of items in the enumeration sequence.
        /// 
        /// </summary>
        [MethodImpl(MethodImplOptions.PreserveSig)]
        int Next([In] uint elementArrayLength, [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.IUnknown), Out] object[] elementArray, out uint fetchedElementCount);

        /// <summary>
        /// Skips over the specified number of items in the enumeration sequence.
        /// 
        /// </summary>
        [MethodImpl(MethodImplOptions.PreserveSig)]
        int Skip([In] uint count);

        /// <summary>
        /// Resets the enumeration sequence to the beginning.
        /// 
        /// </summary>
        [MethodImpl(MethodImplOptions.PreserveSig)]
        int Reset();

        /// <summary>
        /// Creates a new enumerator that contains the same enumeration state as the current one.
        /// 
        /// </summary>
        /// 
        /// <remarks>
        /// This method makes it possible to record a point in the enumeration sequence in order to return to that point at a later time. The caller must release this new enumerator separately from the first enumerator.
        /// </remarks>
        [MethodImpl(MethodImplOptions.PreserveSig)]
        int Clone([MarshalAs(UnmanagedType.Interface)] out IEnumUnknown enumerator);
    }


    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("D332DB9E-B9B3-4125-8207-A14884F53216")]
    [ComImport]
    public interface IClrMetaHost
    {
        /// <summary>
        /// Gets the ICLRRuntimeInfo interface that corresponds to a particular version of the common language runtime (CLR). This method supersedes the CorBindToRuntimeEx function used with the STARTUP_LOADER_SAFEMODE flag.
        /// 
        /// </summary>
        /// HRESULT GetRuntime([in] LPCWSTR pwzVersion, [in, REFIID riid, [out, iid_is(riid), retval] LPIntPtr ppRuntime);
        [MethodImpl(MethodImplOptions.PreserveSig)]
        int GetRuntime([MarshalAs(UnmanagedType.LPWStr), In] string version, [MarshalAs(UnmanagedType.LPStruct), In] Guid interfaceId, [MarshalAs(UnmanagedType.Interface)] out object ppRuntime);

        /// <summary>
        /// Gets an assembly's original .NET Framework compilation version (stored in the metadata), given its file path. This method supersedes the GetFileVersion function.
        /// 
        /// </summary>
        /// HRESULT GetVersionFromFile([in] LPCWSTR pwzFilePath, [out, size_is(*pcchBuffer)] LPWSTR pwzBuffer, [in, out] DWORD* pcchBuffer);
        [MethodImpl(MethodImplOptions.PreserveSig)]
        int GetVersionFromFile([MarshalAs(UnmanagedType.LPWStr), In] string filePath, [MarshalAs(UnmanagedType.LPWStr), Out] StringBuilder buffer, [In, Out] ref int bufferLength);

        /// <summary>
        /// Returns an enumeration that contains a valid ICLRRuntimeInfo interface for each version of the common language runtime (CLR) that is installed on a computer.
        /// 
        /// </summary>
        /// HRESULT EnumerateInstalledRuntimes([out, retval] IEnumUnknown** ppEnumerator);
        [MethodImpl(MethodImplOptions.PreserveSig)]
        int EnumerateInstalledRuntimes([MarshalAs(UnmanagedType.Interface)] out IEnumUnknown ppEnumerator);

        /// <summary>
        /// Returns an enumeration that includes a valid ICLRRuntimeInfo interface pointer for each version of the common language runtime (CLR) that is loaded in a given process. This method supersedes the GetVersionFromProcess function.
        /// 
        /// </summary>
        /// HRESULT EnumerateLoadedRuntimes([in] HANDLE hndProcess, [out, retval] IEnumUnknown** ppEnumerator);
        [MethodImpl(MethodImplOptions.PreserveSig)]
        int EnumerateLoadedRuntimes([In] IntPtr processHandle, [MarshalAs(UnmanagedType.Interface)] out IEnumUnknown ppEnumerator);

        /// <summary>
        /// Provides a callback function that is guaranteed to be called when a common language runtime (CLR) version is first loaded, but not yet started. This method supersedes the LockClrVersion function.
        /// 
        /// </summary>
        /// HRESULT RequestRuntimeLoadedNotification([in] RuntimeLoadedCallbackFnPtr pCallbackFunction);
        [MethodImpl(MethodImplOptions.PreserveSig)]
        int RequestRuntimeLoadedNotification([In] IntPtr pCallbackFunction);

        /// <summary>
        /// Returns an interface that represents a runtime to which legacy activation policy has been bound, for example, by using the useLegacyV2RuntimeActivationPolicy attribute on the [startup] element configuration file entry, by direct use of the legacy activation APIs, or by calling the ICLRRuntimeInfo::BindAsLegacyV2Runtime method.
        /// 
        /// </summary>
        /// HRESULT QueryLegacyV2RuntimeBinding([in] REFIID riid, [out, iid_is(riid), retval] LPIntPtr ppUnk);
        [MethodImpl(MethodImplOptions.PreserveSig)]
        int QueryLegacyV2RuntimeBinding([MarshalAs(UnmanagedType.LPStruct), In] Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppUnk);

        [MethodImpl(MethodImplOptions.PreserveSig)]
        int ExitProcess([In] int iExitCode);
    }


    /// <summary>
    /// Provides methods that return information about a specific common language runtime (CLR), including version, directory, and load status.
    ///             This interface also provides runtime-specific functionality without initializing the runtime.
    ///             It includes the runtime-relative LoadLibrary method, the runtime module-specific GetProcAddress method, and runtime-provided interfaces through the GetInterface method.
    /// 
    /// </summary>
    [Guid("BD39D1D2-BA2F-486A-89B0-B4B0CB466891")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    public interface IClrRuntimeInfo
    {
        /// <summary>
        /// Gets common language runtime (CLR) version information associated with a given ICLRRuntimeInfo interface. This method supersedes GetRequestedRuntimeInfo and GetRequestedRuntimeVersion functions.
        /// 
        /// </summary>
        /// <param name="buffer">
        /// <para>
        /// The .NET Framework compilation version in the format "vA.B[.X]". A, B, and X are decimal numbers that correspond to the major version, the minor version, and the build number. X is optional. If X is not present, there is no trailing period.
        /// </para>
        /// 
        /// <para>
        /// This parameter must match the directory name for the .NET Framework version, as it appears under C:\Windows\Microsoft.NET\Framework.
        /// </para>
        /// 
        /// <para>
        /// Example values are "v1.0.3705", "v1.1.4322", "v2.0.50727", and "v4.0.x", where x depends on the build number installed. Note that the "v" prefix is mandatory.
        /// </para>
        /// </param><param name="bufferLength">Specifies the size of pwzBuffer to avoid buffer overruns. If pwzBuffer is null, pchBuffer returns the required size of pwzBuffer to allow preallocation.</param>
        /// <returns>
        /// HRESULT
        /// </returns>
        [MethodImpl(MethodImplOptions.PreserveSig)]
        int GetVersionString([MarshalAs(UnmanagedType.LPWStr), Out] StringBuilder buffer, [In, Out] ref int bufferLength);

        /// <summary>
        /// Gets the installation directory of the common language runtime (CLR) associated with this interface. This method supersedes the GetCORSystemDirectory function provided in the .NET Framework versions 2.0, 3.0, and 3.5.
        /// 
        /// </summary>
        /// <param name="buffer">Returns the CLR installation directory. The installation path is fully qualified; for example, "c:\windows\microsoft.net\framework\v1.0.3705\".</param><param name="bufferLength">Specifies the size of pwzBuffer to avoid buffer overruns. If pwzBuffer is null, pchBuffer returns the required size of pwzBuffer.</param>
        /// <returns>
        /// HRESULT
        /// </returns>
        [MethodImpl(MethodImplOptions.PreserveSig)]
        int GetRuntimeDirectory([MarshalAs(UnmanagedType.LPWStr), Out] StringBuilder buffer, [In, Out] ref uint bufferLength);

        /// <summary>
        /// Indicates whether the common language runtime (CLR) associated with the ICLRRuntimeInfo interface is loaded into a process. A runtime can be loaded without also being started.
        /// 
        /// </summary>
        /// <param name="processHandle">A handle to the process.</param><param name="isLoaded">True if the CLR is loaded into the process; otherwise, false.</param>
        /// <returns>
        /// HRESULT
        /// </returns>
        [MethodImpl(MethodImplOptions.PreserveSig)]
        int IsLoaded([In] IntPtr processHandle, [MarshalAs(UnmanagedType.Bool)] out bool isLoaded);

        /// <summary>
        /// Translates an HRESULT value into an appropriate error message for the specified culture. This method supersedes the following functions: LoadStringRC, LoadStringRCEx.
        /// 
        /// </summary>
        /// <param name="resourceId">The HRESULT to translate.</param><param name="buffer">The message string associated with the given HRESULT.</param><param name="bufferLength">The size of pwzbuffer to avoid buffer overruns. If pwzbuffer is null, pcchBuffer provides the expected size of pwzbuffer to allow preallocation.</param><param name="iLocaleID">The culture identifier. To use the default culture, you must specify -1.</param>
        /// <returns>
        /// HRESULT
        /// </returns>
        [LCIDConversion(3)]
        [MethodImpl(MethodImplOptions.PreserveSig)]
        int LoadErrorString([In] int resourceId, [MarshalAs(UnmanagedType.LPWStr), Out] StringBuilder buffer, [In, Out] ref uint bufferLength);

        /// <summary>
        /// Loads a .NET Framework library from the common language runtime (CLR) represented by an ICLRRuntimeInfo interface. This method supersedes the LoadLibraryShim function.
        /// 
        /// </summary>
        /// <param name="dllName">The name of the assembly to be loaded.</param><param name="hModule">A handle to the loaded assembly.</param>
        /// <returns>
        /// HRESULT
        /// </returns>
        [MethodImpl(MethodImplOptions.PreserveSig)]
        int LoadLibrary([MarshalAs(UnmanagedType.LPWStr), In] string dllName, out IntPtr hModule);

        /// <summary>
        /// Gets the address of a specified function that was exported from the common language runtime (CLR) associated with this interface. This method supersedes the GetRealProcAddress function.
        /// 
        /// </summary>
        /// <param name="procName">The name of the exported function.</param><param name="pProc">The address of the exported function.</param>
        /// <returns>
        /// HRESULT
        /// </returns>
        [MethodImpl(MethodImplOptions.PreserveSig)]
        int GetProcAddress([MarshalAs(UnmanagedType.LPStr), In] string procName, out IntPtr pProc);

        /// <summary>
        /// Loads the CLR into the current process and returns runtime interface pointers, such as ICLRRuntimeHost, ICLRStrongName, ICorDebug, and IMetaDataDispenserEx. This method supersedes all the CorBindTo* functions in the .NET Framework 1.1 and 2.0 Hosting Global Static Functions section.
        /// 
        /// </summary>
        /// <param name="clsid">The CLSID interface for the coclass.</param><param name="iid">The IID of the requested rclsid interface.</param><param name="ppUnk">A pointer to the queried interface.</param>
        /// <returns>
        /// HRESULT
        /// </returns>
        [MethodImpl(MethodImplOptions.PreserveSig)]
        int GetInterface([MarshalAs(UnmanagedType.LPStruct), In] Guid clsid, [MarshalAs(UnmanagedType.LPStruct), In] Guid iid, [MarshalAs(UnmanagedType.Interface)] out object ppUnk);

        /// <summary>
        /// Indicates whether the runtime associated with this interface can be loaded into the current process, taking into account other runtimes that might already be loaded into the process.
        /// 
        /// </summary>
        /// <param name="isLoaded">True if this runtime could be loaded into the current process; otherwise, false.</param>
        /// <returns>
        /// HRESULT
        /// </returns>
        /// 
        /// <remarks>
        /// If another runtime is already loaded into the process and the runtime associated with this interface can be loaded for in-process side-by-side execution, pbLoadable returns true. If the two runtimes cannot run side-by-side in-process, pbLoadable returns false. For example, the common language runtime (CLR) version 4 can run side-by-side in the same process with CLR version 2.0 or CLR version 1.1. However, CLR version 1.1 and CLR version 2.0 cannot run side-by-side in-process.
        ///             If no runtimes are loaded into the process, this method always returns true.
        /// 
        /// </remarks>
        [MethodImpl(MethodImplOptions.PreserveSig)]
        int IsLoadable([MarshalAs(UnmanagedType.Bool)] out bool isLoaded);

        /// <summary>
        /// Sets the startup flags and the host configuration file that will be used to start the runtime. This method supersedes the use of the startupFlags parameter in the CorBindToRuntimeEx and CorBindToRuntimeHost functions.
        /// 
        /// </summary>
        /// <param name="dwStartupFlags">The host startup flags to set. Use the same flags as with the CorBindToRuntimeEx and CorBindToRuntimeHost functions.</param><param name="pwzHostConfigFile">The directory path of the host configuration file to set.</param>
        /// <returns>
        /// HRESULT
        /// </returns>
        [MethodImpl(MethodImplOptions.PreserveSig)]
        int SetDefaultStartupFlags([In] uint dwStartupFlags, [MarshalAs(UnmanagedType.LPWStr), In] string pwzHostConfigFile);

        /// <summary>
        /// Gets the startup flags and host configuration file that will be used to start the runtime.
        /// 
        /// </summary>
        /// <param name="dwStartupFlags">A pointer to the host startup flags that are currently set.</param><param name="pwzHostConfigFile">A pointer to the directory path of the current host configuration file.</param><param name="pcchHostConfigFile">On input, the size of pwzHostConfigFile, to avoid buffer overruns. If pwzHostConfigFile is null, the method returns the required size of pwzHostConfigFile for pre-allocation.</param>
        /// <returns>
        /// HRESULT
        /// </returns>
        /// 
        /// <remarks>
        /// This method returns the default flag values (STARTUP_CONCURRENT_GC and NULL), or the values provided by a previous call to the ICLRRuntimeInfo::SetDefaultStartupFlags method, or the values set by any of the CorBind* methods if they are bound to this runtime.
        /// 
        /// </remarks>
        [MethodImpl(MethodImplOptions.PreserveSig)]
        int GetDefaultStartupFlags(out uint dwStartupFlags, [MarshalAs(UnmanagedType.LPWStr), Out] StringBuilder pwzHostConfigFile, [In, Out] ref uint pcchHostConfigFile);

        /// <summary>
        /// Binds the current runtime for all legacy common language runtime (CLR) version 2 activation policy decisions.
        /// 
        /// </summary>
        /// 
        /// <returns>
        /// HRESULT
        /// </returns>
        /// 
        /// <remarks>
        /// If the current runtime is already bound for all legacy CLR version 2 activation policy decisions (for example, by using the useLegacyV2RuntimeActivationPolicy attribute on the &lt;startup&gt; element in the configuration file), this method does not return an error result; instead, the result is S_OK, just as it would be if the method had successfully bound legacy activation policy.
        /// 
        /// </remarks>
        [MethodImpl(MethodImplOptions.PreserveSig)]
        int BindAsLegacyV2Runtime();

        /// <summary>
        /// Indicates whether the runtime has been started (that is, whether the ICLRRuntimeHost::Start method has been called and has succeeded).
        /// 
        /// </summary>
        /// <param name="isStarted">True if this runtime is started; otherwise, false.</param><param name="dwStartupFlags">Returns the flags that were used to start the runtime.</param>
        /// <returns>
        /// HRESULT
        /// </returns>
        [MethodImpl(MethodImplOptions.PreserveSig)]
        int IsStarted([MarshalAs(UnmanagedType.Bool)] out bool isStarted, out uint dwStartupFlags);
    }

    public static class EnumUnknownExtensions
    {
        private static IEnumerator<object> GetEnumerator(this IEnumUnknown enumerator)
        {
            if (enumerator == null)
            {
                throw new ArgumentNullException(nameof(enumerator));
            }

            uint count;
            do
            {
                var elementArray = new object[1];
                enumerator.Next(1, elementArray, out count);
                if (count == 1)
                {
                    yield return elementArray[0];
                }
            }
            while (count > 0);
        }

        public static IEnumerable<T> Cast<T>(this IEnumUnknown enumerator)
        {
            var e = enumerator.GetEnumerator();
            while (e.MoveNext())
            {
                yield return (T)e.Current;
            }
        }
    }

    internal static class ClrUtil
    {
        private static readonly IClrMetaHost ClrMetaHost = CreateClrMetaHost();
        private static IClrMetaHost CreateClrMetaHost()
        {
            object pClrMetaHost;
            HResult result = MSCorEE.CLRCreateInstance(MSCorEE.CLSID_CLRMetaHost, typeof(IClrMetaHost).GUID, out pClrMetaHost);
            if (result.Failed)
            {
                throw new Win32Exception();
            }

            return (IClrMetaHost)pClrMetaHost;
        }
        public static IEnumerable<string> GetProcessRuntimes(SafeHandle handle)
        {
            var buffer = new StringBuilder(1024);
            int num = 0;
            if (ClrMetaHost != null)
            {
                IEnumUnknown ppEnumerator;
                num = ClrMetaHost.EnumerateLoadedRuntimes(handle.DangerousGetHandle(), out ppEnumerator);
                if (num >= 0)
                    return ppEnumerator.Cast<IClrRuntimeInfo>().Select(rti =>
                    {
                        int bufferLength = buffer.Capacity;
                        rti.GetVersionString(buffer, ref bufferLength);
                        return buffer.ToString();
                    });
            }
            return Enumerable.Empty<string>();
        }
    }
}
