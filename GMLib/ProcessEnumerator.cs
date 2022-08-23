using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using PInvoke;

namespace GMLib
{
    public class ProcessEnumerator: IEnumerable
    {
        public List<GMProcessInfo> PList { get; set; } = new();
        public ProcessEnumerator()
        {
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    using (var handle = Kernel32.OpenProcess(Kernel32.ProcessAccess.PROCESS_QUERY_LIMITED_INFORMATION | Kernel32.ProcessAccess.PROCESS_VM_READ, false, p.Id))
                    {
                        if (!handle.IsInvalid)
                        {
                            List<string> runtimes = ClrUtil.GetProcessRuntimes(handle).ToList();
                            if (runtimes.Count != 0)
                            {
                                PList.Add(new GMProcessInfo { Pid = p.Id, Name = p.ProcessName, Runtimes = runtimes });
                            }
                        }
                    }
                }
                // Don't care
                catch { }
            }
        }
        public IEnumerator GetEnumerator()
        {
            return PList.GetEnumerator();
        }
    }
}
