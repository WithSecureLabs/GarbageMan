using System;
using System.Collections.Generic;
using System.Linq;
using GMLib;
using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Drawing;
using System.Windows.Media.Imaging;
using System.Reflection;

namespace GarbageMan
{

    public class UIRegister
    {
        public string Reg { get; set; }
        public ulong Value { get; set; }
        public bool HasData { get; set; }
        public string DataPreview { get; set; }
        public UIObjectData Data { get; set; }
    }
    public class UIPickedProcess
    {
        public int Pid { get; set; }
        public string Name { get; set; }
        public string Runtime { get; set; }
        public string Arch { get; set; }
    }
    public class UIHexData
    {
        public int Offset { get; set; }
        public string Hex { get; set; }
        public string Ascii { get; set; }

    }

    public class UIHexReader : IList, INotifyCollectionChanged
    {
        public byte[] Binary { get; set; }
        public string Printable { get; set; }
        public string ToolTip { get; set; }
        public int Size { get; set; }
        public int Count { get; set; }
        public bool IsImage { get; set; }
        public bool IsAssembly { get; set; }
        public BitmapImage Img { get; set; }

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        private Dictionary<int, List<UIHexData>> _cache = new();
        private int _pageSize = 100;
        private int _lastSize;

        private bool isPrintable(byte value)
        {
            if ((value >= 32) && (value < 127))
            {
                Printable += (char)value;
                return true;
            }
            return false;
        }
        private UIHexData getHexData(int index, int len)
        {
            int line = index * 16;
            string hex = "";
            string ascii = "";
            for (int i = 0; i < len; i++)
            {
                string e = " ";
                string s = "";
                if (i == 0) s = " ";
                if (i == 7) e = "  ";
                else if (i == len) e = "";
                hex += $"{s}{Binary[line + i]:X2}{e}";
                ascii += isPrintable(Binary[line + i]) ? (char)Binary[line + i] : ".";
            }
            return new UIHexData
            {
                Offset = line,
                Hex = hex,
                Ascii = ascii
            };
        }

        private void LoadLines(int index, int count)
        {
            int realCount = count;
            if ((index + count) > Size)
                realCount = Size - index;

            _cache.Add(index, new List<UIHexData>());

            int i = index;
            for (; i < index + realCount; i++)
            {
                _cache[index].Add(getHexData(i, (i == (Size - 1) && _lastSize != 0) ? _lastSize : 16));
            }
            Count++;
        }

        private void LoadImage()
        {
            try
            {
                using (var stream = new MemoryStream(Binary))
                {
                    Img = new BitmapImage();
                    Img.BeginInit();
                    Img.CacheOption = BitmapCacheOption.OnLoad;
                    Img.StreamSource = stream;
                    Img.EndInit();
                }
                IsImage = true;
                ToolTip = $"Image {Img.ToString()}";
            }
            catch
            {
                Img = null;
                IsImage = false;
            }
        }

        private void LoadAssembly()
        {
            if (Binary.Length > 1000 && Binary[0] == 'M' && Binary[1] == 'Z')
            {
                if (PeNet.PeFile.IsPeFile(Binary))
                {
                    if (PeNet.PeFile.TryParse(Binary, out var pe))
                    {
                        string bits = pe.Is64Bit ? "64" : "32";
                        if (pe.IsDotNet)
                        {
                            ToolTip = $".NET {bits}-bit executable ({pe.FileSize} bytes)";
                            if (pe.MetaDataStreamTablesHeader?.Tables?.Assembly?.Count > 0)
                            {
                                var assembly = pe.MetaDataStreamTablesHeader.Tables.Assembly[0];
                                string name;
                                try
                                {
                                    name = pe.MetaDataStreamString?.GetStringAtIndex(assembly.Name);
                                }
                                catch
                                {
                                    name = "";
                                }
                                ToolTip = $".NET {bits}-bit assembly ({name}, Version={assembly.MajorVersion}.{assembly.MinorVersion}.{assembly.RevisionNumber}, Size={pe.FileSize} bytes)";
                            }
                        }
                        else
                            ToolTip = $"PE {bits}-bit executable ({pe.FileSize} bytes)";
                    }
                }
            }
        }

        public UIHexReader(UIObjectData obj, int maxLength = -1)
        {
            byte[] data = obj.Object.Value;

            if (data == null)
                return;
            Binary = data;
            int len = data.Length;
            ToolTip = $"Binary data (len: {data.Length} bytes)";

            if (maxLength != -1)
                len = Math.Max(data.Length, maxLength);

            int lines = len / 16;
            int leftover = len % 16;

            Size = lines;
            if (leftover != 0) Size++;
            _lastSize = leftover;

            LoadLines(0, _pageSize);
            if (obj.IsBlob)
            {
                LoadImage();
                if (!IsImage)
                {
                    try
                    {
                        LoadAssembly();
                    }
                    catch {}
                }
            }
        }

        private UIHexData GetObject(int index)
        {
            int ptr = index % _pageSize;
            int page = index - ptr;

            if (!_cache.ContainsKey(page))
            {
                LoadLines(page, _pageSize);
            }
            if (_cache[page].Count != 0)
                return _cache[page][ptr];
            else
                return null;
        }

        public object this[int index] { get => GetObject(index); set => throw new NotImplementedException(); }

        bool IList.IsReadOnly => true;

        int ICollection.Count => Size;

        public bool IsFixedSize => throw new NotImplementedException();

        public bool IsSynchronized => throw new NotImplementedException();

        public object SyncRoot => throw new NotImplementedException();

        public IEnumerator<UIHexData> GetEnumerator()
        {
            foreach (var list in _cache.Values)
            {
                foreach (UIHexData obj in list)
                    yield return obj;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        int IList.IndexOf(object value)
        {
            return ((UIHexData)value).Offset;
        }

        public int Add(object value)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        bool IList.Contains(object value)
        {
            foreach (UIHexData obj in this)
            {
                if (obj == value)
                    return true;
            }
            return false;
        }

        public void Insert(int index, object value)
        {
            throw new NotImplementedException();
        }

        public void Remove(object value)
        {
            throw new NotImplementedException();
        }

        public void RemoveAt(int index)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(Array array, int index)
        {
            throw new NotImplementedException();
        }

    }

    public class UIRefData
    {
        public ulong Address { get; set; }
        public UIObjectData Data { get; set; }
    }

    public class UIBookmark
    {
        public UIObjectData Data { get; set; }
        public string Notes { get; set; }
    }

    public class UIObjectData
    {
        public int Snapshot { get; set; }
        public int Item { get; set; }
        public ulong Address { get; set; }
        public ulong Size { get; set; }
        public string Type { get; set; }
        public string Value { get; set; }
        public string Repr { get; set; }
        public bool IsBlob { get; set; }
        public bool IsNull { get; set; }
        public bool IsString { get; set; }
        public bool IsPrimitive { get; set; }
        public bool IsBookmarked { get; set; }
        public bool IsTraced { get; set; }
        public UIHexReader HexReader { get; set; }

        public GMObjectData Object;
        public List<UIRefData> Refsto { get; set; }
        public List<UIRefData> Refsby { get; set; }
        public List<UITraceObject> Trace { get; set; }
        public bool PendingTrace { get; set; }

        private bool isPrintable(byte value)
        {
            if ((value >= 32) && (value < 127))
            {
                return true;
            }
            return false;
        }

        private void GetRepr()
        {
            for (int i = 0; i < Math.Min(Object.Value.Length, 32); i++)
            {
                if (isPrintable(Object.Value[i]))
                    Repr += (char)Object.Value[i];
                else
                    Repr += '.';
            }
        }

        public UIObjectData(GMObjectData obj, int item = 0)
        {
            Snapshot = (int)obj.Id;
            Item = item;
            Address = obj.Address;
            Size = obj.Size;
            Type = obj.Type;
            Object = obj;

            if (obj.Value == null)
            {
                Value = "NULL";
                Repr = Value;
                IsNull = true;
            }
            else
            {
                switch (obj.Type)
                {
                    case "System.Byte[]":
                    case "System.Char[]":
                    case "System.SByte[]":
                    case "System.Int16[]":
                    case "System.Int32[]":
                    case "System.Int64[]":
                    case "System.UInt16[]":
                    case "System.UInt32[]":
                    case "System.UInt64[]":
                        Value = "BIN";
                        GetRepr();
                        IsBlob = true;
                        IsPrimitive = true;
                        break;
                    case "System.String":
                        Value = System.Text.Encoding.ASCII.GetString(obj.Value);
                        Repr = Value;
                        IsString = true;
                        IsPrimitive = true;
                        break;
                    default:
                        Value = System.Text.Encoding.ASCII.GetString(obj.Value);
                        Repr = Value;
                        break;
                }
            }
        }
    }

    public class UIObjectReader : IList, INotifyCollectionChanged
    {
        public int Size { get; set; }
        public int Count { get; set; }

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        private DatabaseContext dbCtx;
        private int _snapshot;
        private Dictionary<int, List<UIObjectData>> _cache = new();
        private int _size;
        private int _pageSize = 1000;

        private void LoadPage(int index, int count)
        {
            int counter = index;

            _cache.Add(index, new List<UIObjectData>());

            var g = dbCtx.Objects.Where(o => o.Id == _snapshot).Skip(index).Take(count);
            foreach (GMObjectData o in g)
            {
                UIObjectData d = new(o, counter++);
                _cache[index].Add(d);
            }
            Count++;
        }

        private void InitialLoad()
        {
            _size = dbCtx.Objects.Where(o => o.Id == _snapshot).Count();
            Size = _size;
            LoadPage(0, _pageSize);
        }

        public UIObjectReader(DatabaseContext dbctx, int snapshot)
        {
            dbCtx = dbctx;
            _snapshot = snapshot;
            Count = 0;
            InitialLoad();
        }

        public UIObjectData GetObjectByAddress(ulong addr)
        {
            var obj = dbCtx.Objects.Where(o => o.Id == _snapshot && o.Address == addr).FirstOrDefault();
            if (obj != null)
                return new UIObjectData(obj, 0);
            return null;
        }

        private UIObjectData GetObject(int index)
        {
            int ptr = index % _pageSize;
            int page = index - ptr;

            if (!_cache.ContainsKey(page))
            {
                LoadPage(page, _pageSize);
            }
            if (_cache[page].Count != 0)
                return _cache[page][ptr];
            else
                return null;
        }

        public object this[int index] { get => GetObject(index); set => throw new NotImplementedException(); }

        bool IList.IsFixedSize => throw new NotImplementedException();

        bool IList.IsReadOnly => true;

        int ICollection.Count => _size;

        bool ICollection.IsSynchronized => throw new NotImplementedException();

        object ICollection.SyncRoot => throw new NotImplementedException();
        public void Notify(UIObjectData obj)
        {
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        int IList.Add(object value)
        {
            throw new NotImplementedException();
        }

        void IList.Clear()
        {
            throw new NotImplementedException();
        }

        bool IList.Contains(object value)
        {
            foreach (UIObjectData obj in this)
            {
                if (obj == value)
                    return true;
            }
            return false;
        }

        void ICollection.CopyTo(Array array, int index)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<UIObjectData> GetEnumerator()
        {
            foreach (var list in _cache.Values)
            {
                foreach (UIObjectData obj in list)
                    yield return obj;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        int IList.IndexOf(object value)
        {
            return ((UIObjectData)value).Item;
        }

        void IList.Insert(int index, object value)
        {
            throw new NotImplementedException();
        }

        void IList.Remove(object value)
        {
            throw new NotImplementedException();
        }

        void IList.RemoveAt(int index)
        {
            throw new NotImplementedException();
        }

    }

    public class UISearchItem
    {
        public string Header { get; set; }
        public string Category { get; set; }
        public string SearchType { get; set; }
        public string SearchAll { get; set; }
        public string Snapshot { get; set; }
        public string Type { get; set; }
        public string Value { get; set; }
        public string Order { get; set; }
        public string Limit { get; set; }
        public string Sort { get; set; }
        public string SQL { get; set; }
    }
    public class UISearchItems
    {
        public List<UISearchItem> Items { get; set; }
    }

    public class UISettings
    {
        public int Index { get; set; }
        public bool IsCurrent { get; set; }
        public Stack<int> History { get; set; }
        public int Tid { get; set; }
        public int Pid { get; set; }
    }

    public class UISnapshot
    {
        public int Id { get; set; }
        public int PID { get; set; }
        public int Time { get; set; }
        public int PointerSize { get; set; }
        public UISettings Settings { get; set; }
        public UIObjectReader ObjectReader { get; set; }
        public List<List<UIFrame>> Frames { get; set; }
        public Dictionary<int, List<UIStack>> Stacks { get; set; }
        public List<UIHandle> Handles { get; set; }
        public List<GMAppDomain> AppDomains { get; set; }
        public List<GMRuntime> Runtimes { get; set; }
        public List<GMModule> Modules { get; set; }
        public List<int> Tids { get; set; }
        public List<UIThread> Threads { get; set; }
        public string Status { get; set; }
        public int ThreadCount { get; set; }
        public int SocketCount { get; set; }
        public int IOCount { get; set; }
    }

    public class UIFrame
    {
        public ulong StackPtr { get; set; }
        public ulong IP { get; set; }
        public string Frame { get; set; }
    }

    public class UIStack
    {
        public ulong StackPtr { get; set; }
        public UIObjectData Data { get; set; }
    }

    public class UIHandle
    {
        public ulong Address { get; set; }
        public ulong Object { get; set; }
        public string Kind { get; set; }
    }

    public class UIThread
    {
        public int Tid { get; set; }
        public ThreadContext Context { get; set; }
        public GMThread Data { get; set; }
    }
}
