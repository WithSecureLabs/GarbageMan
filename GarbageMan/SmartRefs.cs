using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GMLib;
using Microsoft.EntityFrameworkCore;
using System.Collections;
using Microsoft.Data.Sqlite;

namespace GarbageMan
{
    public class UITraceNode
    {
        public ulong Address { get; set; }
        public string To { get; set; }
        public string Type { get; set; }
    }

    public class UITraceObject
    {
        public ulong Address { get; set; }
        public int Distance { get; set; }
        public UIObjectData Object { get; set; }
        public GMObjectData ObjectData { get; set; }
        public List<UITraceNode> Path { get; set; }

        // Return json string on ToString()
        public override string ToString()
        {
            if (Path.Count != 0)
            {
                string path = "[\n";
                for (int i = 0; i < Path.Count; i++)
                {
                    path += "  {";
                    path += $" \"Address\": {Path[i].Address}";
                    path += $", \"To\": {Path[i].To}, \"Type\": \"{Path[i].Type}\"" + " }";
                    if (i < (Path.Count - 1))
                        path += ",";
                    path += "\n";
                }
                path += "]\n";
                return path;
            }
            return null;
        }
    }

    public class ReferenceTracer
    {
        public int Snapshot { get; set; }
        public SqliteConnection db { get; set; }

        private bool VerifyObjectType(GMObjectData obj)
        {
            bool retVal = false;
            if (obj != null)
            {
                if (obj.Type == "System.Object[]" && obj.Size > 100)
                    retVal = false;
                else if (obj.Type.StartsWith("System.Globalization") || obj.Type.StartsWith("System.Configuration"))
                    retVal = false;
                else
                    retVal = true;
            }
            return retVal;
        }

        private bool IsPrimitiveValue(string type)
        {
            bool retVal = false;
            switch (type)
            {
                case "System.String":
                case "System.Char[]":
                case "System.Byte[]":
                case "System.SByte[]":
                    retVal = true;
                    break;
                default:
                    break;
            }
            return retVal;
        }
        public void Close()
        {
            db.Dispose();
            db.Close();
        }

        public void Trace(TracerArguments args)
        {
            Snapshot = args.Snapshot;

            db = new SqliteConnection($"Filename={args.Database}; Pooling=False;");
            db.Open();
            /*
            string pragmaCommand = "PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;";
            SqliteCommand createPragma = new(pragmaCommand, db);
            createPragma.ExecuteNonQuery();
            */
            // Return values
            List<UITraceObject> retList = new();

            HashSet<ulong> seen = new HashSet<ulong>() { args.Object.Object.ObjectId };
            Stack<UITraceObject> todo = new Stack<UITraceObject>();

            // First node
            List<UITraceNode> path = new();
            path.Add(new UITraceNode { Address = args.Object.Address, To = "false", Type = args.Object.Type });
            todo.Push(new UITraceObject { Object = args.Object, ObjectData = args.Object.Object, Distance = 0, Address = args.Object.Object.ObjectId, Path = path });

            while (todo.Count > 0)
            {
                if (args.IsStopped)
                    break;
                if (args.IsCanceled)
                {
                    args.Done?.Set();
                    return;
                }

                UITraceObject curr = todo.Pop();

                // Do not follow very long paths
                if (curr.Distance > args.TraceDepth)
                {
                    continue;
                }
                retList.Add(curr);

                // SQL query gets Object rows for all objects referencing to given object or referenced by the object
                var cmd = db.CreateCommand();
                cmd.CommandText =  $"SELECT * FROM Objects WHERE Id = {Snapshot + 1} AND ObjectId IN (";
                cmd.CommandText += $"SELECT Address FROM Refs WHERE Id = {Snapshot + 1} AND (Address = {curr.Address} OR Ref = {curr.Address}) ";
                cmd.CommandText += $"UNION SELECT Ref FROM Refs WHERE Id = {Snapshot + 1} AND (Address = {curr.Address} OR Ref = {curr.Address})) LIMIT 50";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        ulong objId = (ulong)reader.GetInt64(1);
                        if (seen.Add(objId))
                        {
                            GMObjectData obj = new GMObjectData
                            {
                                Id = Snapshot + 1,
                                ObjectId = objId,
                                Address = (ulong)reader.GetInt64(2),
                                Type = reader.GetString(3),
                                Size = (ulong)reader.GetInt64(4)
                            };
                            if (VerifyObjectType(obj))
                            {
                                if (!reader.IsDBNull(5))
                                    obj.Value = reader.GetFieldValue<byte[]>(5);

                                List<UITraceNode> reftopath = new();
                                reftopath.AddRange(curr.Path);
                                reftopath.Add(new UITraceNode { Address = obj.Address, To = "true", Type = obj.Type });
                                todo.Push(new UITraceObject { Address = objId, Distance = curr.Distance + 1, ObjectData = obj, Path = reftopath });
                            }
                        }
                    }
                }
            }
            for (int i = retList.Count - 1; i >= 0; i--)
            {
                if (retList[i].Distance == 0)
                    continue;
                else
                {
                    // non-primitive types probably not very interesting here
                    if (!(IsPrimitiveValue(retList[i].ObjectData.Type)))
                        retList.RemoveAt(i);
                    else
                        retList[i].Object = new UIObjectData(retList[i].ObjectData);
                }
            }
            args.Object.Trace = retList.OrderBy(r => r.Distance).ToList();
            args.Done?.Set();
        }
    }
}
