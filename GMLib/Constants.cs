using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GMLib
{
    static public class Constants
    {
        public const uint COLLECT_MODULES    = 0x00000001;
        public const uint COLLECT_RUNTIMES   = 0x00000002;
        public const uint COLLECT_APPDOMAINS = 0x00000004;
        public const uint COLLECT_THREADS    = 0x00000010;
        public const uint COLLECT_STACK      = 0x00000100;
        public const uint COLLECT_HEAP       = 0x00010000;
        public const uint COLLECT_HANDLES    = 0x00100000;
        public const uint COLLECT_REFS       = 0x10000000;
        public const uint COLLECT_BASIC_INFO = COLLECT_RUNTIMES | COLLECT_APPDOMAINS | COLLECT_MODULES;
        public const uint COLLECT_EVERYTHING = 0xFFFFFFFF;

        public const uint CONTEXT_FLAGS_32 = 0x1003F;
        public const uint CONTEXT_FLAGS_64 = 0x1001F;

        public const int MAX_STRING_SIZE = 0x10000;
        public const int MAX_BUFFER_SIZE = 0x100000;
        public const int MAX_INT_REFCOUNT = 1000;

        public const string CMD_OUTPUT_TYPE_MSG     = "msg";
        public const string CMD_OUTPUT_TYPE_PROCESS = "process";
        public const string CMD_OUTPUT_TYPE_PSLIST  = "pslist";

    }
}
