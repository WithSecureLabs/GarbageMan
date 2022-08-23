using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime;


namespace GMLib
{
    public class GMCmdOutput
    {
        public string Type { get; set; }
        public string Msg { get; set; }
    }

    public class GMCmdOutputProcess : GMCmdOutput
    {
        public GMProcessInfo Process { get; set; }
    }
    public class GMCmdOutputPList: GMCmdOutput
    {
        public List<GMProcessInfo> PList { get; set; }
    }
    public class GMProcessInfo
    {
        public int Pid { get; set; }
        public string Name { get; set; }
        public List<string> Runtimes { get; set; }
    }
    public class GMObject
    {
        public ulong Address { get; set; }
        public ulong Size { get; set; }
        public ulong Index { get; set; }

        private ClrObject _obj;

        private int _pointerSize;

        private object _value = null;

        public GMObject(ClrObject obj, ulong index, int pointerSize = 8)
        {
            Index = index;
            Address = obj.Address;
            Size = obj.Size;
            _obj = obj;
            _pointerSize = pointerSize;
        }
        public string Type()
        {
            return _obj.Type.Name;
        }

        public object Value()
        {
            if (_value == null)
            {
                try
                {
                    _value = _Value();
                }
                catch { }
            }
            return _value;
        }

        private object _Value()
        {
            object retData = null;
            switch (_obj.Type.Name)
            {
                case "System.String":
                    retData = _obj.AsString(maxLength: Constants.MAX_STRING_SIZE);
                    break;
                case "System.Char":
                case "System.Int16":
                case "System.Int32":
                case "System.UInt16":
                case "System.UInt32":
                case "System.Boolean":
                    retData = _obj.ReadField<uint>("m_value");
                    break;
                // XXX: 32/64 bit systems seem to have this diffent
                case "System.IntPtr":
                case "System.UIntPtr":
                    if (_pointerSize == 4)
                        retData = _obj.ReadField<uint>("m_value");
                    else
                        retData = _obj.ReadField<ulong>("_value");
                    break;
                // XXX: Convert to string to avoid sqlite "NaN" problem, or maybe check the value?
                case "System.Double":
                    retData = $"{_obj.ReadField<double>("m_value")}";
                    break;
                case "System.Int64":
                case "System.UInt64":
                    retData = _obj.ReadField<ulong>("m_value");
                    break;
                case "System.Char[]":
                case "System.Byte[]":
                case "System.SByte[]":
                case "System.Int16[]":
                case "System.Int32[]":
                case "System.Int64[]":
                case "System.UInt16[]":
                case "System.UInt32[]":
                case "System.UInt64[]":
                    retData = _obj.Type.ReadArrayElements<byte>(_obj, 0, Math.Min((int)_obj.Size - 12, Constants.MAX_BUFFER_SIZE));
                    break;
                default:
                    break;
            }
            return retData;
        }
    }
    public class GMAppDomain
    {
        public long Id { get; set; }
        public long Aid { get; set; }
        public string Name { get; set; }
        public ulong Address { get; set; }
    }

    public class GMFrame
    {
        public long Id { get; set; }
        public long Tid { get; set; }
        public ulong StackPtr { get; set; }
        public ulong Ip { get; set; }
        public string Frame { get; set; }
    }

    public class GMHandle
    {
        public long Id { get; set; }
        public ulong Address { get; set; }
        public ulong Object { get; set; }
        public string Kind { get; set; }
    }

    // XXX: every module belongs to AppDomain, so we need to include Aid here
    public class GMModule
    {
        public long Id { get; set; }
        public ulong AsmAddress { get; set; }
        public ulong ImgAddress { get; set; }
        public string Name { get; set; }
        public string AsmName { get; set; }
        public bool IsDynamic { get; set; }
        public bool IsPe { get; set; }
    }

    public class GMObjectData
    {
        public long Id { get; set; }
        public ulong ObjectId { get; set; }
        public ulong Address { get; set; }
        public string Type { get; set; }
        public ulong Size { get; set; }
        public byte[] Value { get; set; }
    }

    public class GMRuntime
    {
        public long Id { get; set; }
        public string Version { get; set; }
        public string Dac { get; set; }
        public string Arch { get; set; }
    }

    public class GMRef
    {
        public long Id { get; set; }
        public ulong Address { get; set; }
        public ulong Ref { get; set; }
    }

    public class GMStack
    {
        public long Id { get; set; }
        public long Tid { get; set; }
        public ulong StackPtr { get; set; }
        public ulong Object { get; set; }
    }

    public class GMThread
    {
        public long Id { get; set; }
        public long Tid { get; set; }
        public byte[] Context { get; set; }
    }

    public class GMSnapshot
    {
        public long Id { get; set; }
        public long Pid { get; set; }
        public long Time { get; set; }
        public int PointerSize { get; set; }
    }

    public class GMProcess
    {
        public long Pid { get; set; }
        public string Arch { get; set; }
        public string Date { get; set; }
        public string Path { get; set; }
        public string Args { get; set; }

    }

    public class GMBookmark
    {
        public long Id { get; set; }
        public ulong ObjectId { get; set; }
        public string Notes { get; set; }
    }

    public class GMSetting
    {
        public long Id { get; set; }
        public string Setting { get; set; }
    }

}
