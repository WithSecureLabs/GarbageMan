using System;
using System.IO;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Diagnostics.Runtime;
using System.Runtime.InteropServices;

namespace GMLib
{
    public class DataBase
    {
        public bool initialized { get; set; }
        public string DataBasePath { get; set; }
        public SqliteConnection db { get; set; }

        private void InitializeDatabase()
        {
            if (File.Exists(DataBasePath))
            {
                File.Delete(DataBasePath);
            }
            db = new SqliteConnection($"Filename={DataBasePath}");
            db.Open();

            string pragmaCommand = "PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;";
            SqliteCommand createPragma = new(pragmaCommand, db);
            createPragma.ExecuteNonQuery();

            string tableCommand = "CREATE TABLE IF NOT EXISTS Runtimes(" +
                "Id INTEGER, " +
                "Version STRING, " +
                "Dac STRING, " +
                "Arch STRING, " +
                "PRIMARY KEY (Id, Version));";

            tableCommand += "CREATE TABLE IF NOT EXISTS AppDomains(" +
                "Id INTEGER, " +
                "Aid INTEGER NOT NULL, " +
                "Name STRING, " +
                "Address INTEGER NOT NULL, " +
                "PRIMARY KEY (Id, Aid));";

            // XXX: AppDomain id (Aid) as a key !!
            tableCommand += "CREATE TABLE IF NOT EXISTS Modules(" +
                "Id INTEGER, " +
                "AsmAddress INTEGER NOT NULL, " +
                "ImgAddress INTEGER NOT NULL, " +
                "Name STRING, " +
                "AsmName STRING, " +
                "IsDynamic BOOLEAN, " +
                "IsPe BOOLEAN, " +
                "PRIMARY KEY (Id, AsmAddress));";

            tableCommand += "CREATE TABLE IF NOT EXISTS Threads(" +
                "Id INTEGER, " +
                "Tid INTEGER NOT NULL, " +
                "Context BLOB, " +
                "PRIMARY KEY (Id, Tid));";

            tableCommand += "CREATE TABLE IF NOT EXISTS Snapshots(" +
                "Id INTEGER, " +
                "Pid INTEGER, " +
                "Time INTEGER UNIQUE, " +
                "PointerSize INTEGER NOT NULL, " +
                "PRIMARY KEY (Id));";

            tableCommand += "CREATE TABLE IF NOT EXISTS Processes(" +
                "Pid INTEGER, " +
                "Arch STRING, " +
                "Date STRING, " +
                "Path STRING, " +
                "Args STRING, " +                                                
                "PRIMARY KEY (Pid));";

            tableCommand += "CREATE TABLE IF NOT EXISTS Settings(" +
                "Id INTEGER, " +
                "Setting STRING, " +
                "PRIMARY KEY (Id));";

            tableCommand += "CREATE TABLE IF NOT EXISTS Objects(" +
                "Id INTEGER, " +
                "ObjectId INTEGER, " +
                "Address INTEGER, " +
                "Type STRING, " +
                "Size INTEGER," +
                "Value BLOB, " +
                "PRIMARY KEY (Id, ObjectId));";

            tableCommand += "CREATE TABLE IF NOT EXISTS Bookmarks(" +
                "Id INTEGER, " +
                "ObjectId INTEGER, " +
                "Notes STRING, " +
                "PRIMARY KEY (Id, ObjectId));";

            tableCommand += "CREATE TABLE IF NOT EXISTS Refs(" +
                "Id INTEGER, " +
                "Address INTEGER, " +
                "Ref INTEGER, " +
                "PRIMARY KEY (Id, Address, Ref));";

            tableCommand += "CREATE TABLE IF NOT EXISTS Stacks(" +
                "Id INTEGER, " +
                "StackPtr INTEGER, " +
                "Tid INTEGER, " +
                "Object INTEGER, " +
                "PRIMARY KEY (Id, StackPtr));";

            tableCommand += "CREATE TABLE IF NOT EXISTS Frames(" +
                "Id INTEGER, " +
                "Tid INTEGER NOT NULL," +
                "StackPtr INTEGER NOT NULL, " +
                "IP INTEGER NOT NULL, " +
                "Frame BLOB, " +
                "PRIMARY KEY (Id, StackPtr));";

            tableCommand += "CREATE TABLE IF NOT EXISTS Handles(" +
                "Id INTEGER, " +
                "Address INTEGER NOT NULL, " +
                "Object INTEGER NOT NULL, " +
                "Kind STRING, " +
                "PRIMARY KEY (Id, Address));";

            SqliteCommand createTable = new(tableCommand, db);
            createTable.ExecuteNonQuery();
        }

        public DataBase(string path)
        {
            DataBasePath = path;
            if (DataBasePath == null)
                throw new Exception("Trying to initialize database without path.");
        }
        public void Close()
        {
            db.Close();
        }
        public void AddProcess(GMProcess process)
        {
            if (!initialized)
            {
                InitializeDatabase();
                initialized = true;
            }
            string insertCommand = $"INSERT INTO Processes(Pid,Arch,Date,Path,Args) VALUES(" +
                $"{process.Pid},\"{process.Arch}\",\"{process.Date}\",\"{process.Path}\",\"{process.Args}\");";
            SqliteCommand insertTarget = new(insertCommand, db);
            insertTarget.ExecuteNonQuery();
        }

        public void AddSnapshot(GMSnapshot snapshot)
        {
            if (!initialized)
            {
                InitializeDatabase();
                initialized = true;
            }
            string insertCommand = $"INSERT INTO Snapshots(Id,Pid,Time,PointerSize) VALUES({snapshot.Id},{snapshot.Pid},{snapshot.Time}, {snapshot.PointerSize});";
            SqliteCommand insertTarget = new(insertCommand, db);
            insertTarget.ExecuteNonQuery();
        }
        
        private void AddObject(Target target, GMObject obj)
        {
            SqliteCommand cmdTarget;

            // Get the data value for object
            object objData = obj.Value();
            if (objData != null)
            {
                string command = $"INSERT OR IGNORE INTO Objects(Id,ObjectId,Address,Type,Size,Value) VALUES(" +
                    $"{target.Id}," +
                    $"{obj.Index}," +
                    $"{obj.Address}," +
                    $"\"{obj.Type()}\"," +
                    $"{obj.Size}," +
                    "@Value);";
                cmdTarget = new(command, db);

                // Store bytes as blob
                if (objData is System.Byte[])
                {
                    cmdTarget.Parameters.Add(new SqliteParameter()
                    {
                        ParameterName = "@Value",
                        Value = objData,
                        DbType = System.Data.DbType.Binary
                    });
                }
                else
                    cmdTarget.Parameters.Add("@Value", SqliteType.Text).Value = objData;
                cmdTarget.ExecuteNonQuery();
            }
            else
            {
                string command = $"INSERT OR IGNORE INTO Objects(Id,ObjectId,Address,Type,Size) VALUES(" +
                    $"{target.Id}," +
                    $"{obj.Index}," +
                    $"{obj.Address}," +
                    $"\"{obj.Type()}\"," +
                    $"{obj.Size})";
                cmdTarget = new(command, db);
                cmdTarget.ExecuteNonQuery();
            }
        }
        private void AddRefs(Target target)
        {
            foreach (GMRef referece in target.Refs)
            {
                string command = $"INSERT OR IGNORE INTO Refs(Id,Address,Ref) VALUES(" +
                    $"{target.Id}," +
                    $"{referece.Address}," +
                    $"{referece.Ref});";
                SqliteCommand cmdTarget = new(command, db);
                cmdTarget.ExecuteNonQuery();
            }
        }

        private void AddHeapObjects(Target target)
        {
            foreach (GMObject obj in target.Objects)
            {
                AddObject(target, obj);
            }
        }
        private void AddStackObjects(Target target)
        {
            SqliteCommand cmdTarget;
            string command;

            // Add threads
            foreach (GMThread thread in target.Threads)
            {
                command = $"INSERT INTO Threads(Id,Tid,Context) VALUES(" +
                    $"{target.Id},{thread.Tid},@Context);";
                cmdTarget = new(command, db);
                cmdTarget.Parameters.Add(new SqliteParameter()
                {
                    ParameterName = "@Context",
                    Value = thread.Context,
                    DbType = System.Data.DbType.Binary
                });
                cmdTarget.ExecuteNonQuery();
            }

            // Add frames
            foreach (GMFrame frame in target.Frames)
            {
                string frameStr = frame.Frame;
                command = $"INSERT OR IGNORE INTO Frames(Id,Tid,StackPtr,IP,Frame) VALUES(" +
                    $"{target.Id}," +
                    $"{frame.Tid}," +
                    $"{frame.StackPtr}," +
                    $"{frame.Ip}," +
                    $"\"{frameStr}\");";
                cmdTarget = new(command, db);
                cmdTarget.ExecuteNonQuery();
            }

            // Add stacks
            foreach (GMStack stack in target.Stacks)
            {
                command = $"INSERT OR IGNORE INTO Stacks(Id,StackPtr,Tid,Object) VALUES(" +
                    $"{target.Id}," +
                    $"{stack.StackPtr}," +
                    $"{stack.Tid}," +
                    $"{stack.Object})";
                cmdTarget = new(command, db);
                cmdTarget.ExecuteNonQuery();
            }
        }
        private void AddHandles(Target target)
        {
            SqliteCommand cmdTarget;
            string command;
            foreach (GMHandle handle in target.Handles)
            {
                command = $"INSERT INTO Handles(Id,Address,Object,Kind) VALUES(" +
                    $"{target.Id}," +
                    $"{handle.Address}," +
                    $"{handle.Object}," +
                    $"\"{handle.Kind}\");";
                cmdTarget = new(command, db);
                cmdTarget.ExecuteNonQuery();
            }
        }
        private void AddAppDomains(Target target)
        {
            SqliteCommand cmdTarget;
            string command;
            HashSet<long> appdomainSet = new();
            foreach (GMAppDomain domain in target.AppDomains)
            {
                if (appdomainSet.Add(domain.Aid))
                {
                    command = $"INSERT INTO AppDomains(Id,Aid,Name,Address) VALUES(" +
                    $"{target.Id}," +
                    $"{domain.Aid}," +
                    $"\"{domain.Name}\"," +
                    $"{domain.Address});";
                    cmdTarget = new(command, db);
                    cmdTarget.ExecuteNonQuery();
                }
            }
        }

        private void AddRuntimes(Target target)
        {
            SqliteCommand cmdTarget;
            string command;
            foreach (GMRuntime info in target.Runtimes)
            {
                command = $"INSERT INTO Runtimes(Id,Version,Dac,Arch) VALUES(" +
                    $"{target.Id}," +
                    $"\"{info.Version}\"," +
                    $"\"{info.Dac}\"," +
                    $"\"{info.Arch}\");";
                cmdTarget = new(command, db);
                cmdTarget.ExecuteNonQuery();
            }
        }

        // XXX: AppDomain id (Aid) as a key here!!
        private void AddModules(Target target)
        {
            SqliteCommand cmdTarget;
            string command;
            HashSet<ulong> modSet = new();
            foreach (GMModule module in target.Modules)
            {
                if (modSet.Add(module.AsmAddress))
                {
                    command = $"INSERT INTO Modules(Id,AsmAddress,ImgAddress,Name,AsmName,IsDynamic,IsPe) VALUES(" +
                        $"{target.Id}," +
                        $"{module.AsmAddress}," +
                        $"{module.ImgAddress}," +
                        $"\"{module.Name}\"," +
                        $"\"{module.AsmName}\"," +
                        $"{module.IsDynamic}," +
                        $"{module.IsPe});";
                    cmdTarget = new(command, db);
                    cmdTarget.ExecuteNonQuery();
                }
            }
        }
        public void AddTarget(Target target)
        {
            if (!initialized)
            {
                InitializeDatabase();
                initialized = true;
            }

            SqliteCommand cmdTarget = new("BEGIN", db);
            cmdTarget.ExecuteNonQuery();

            AddAppDomains(target);
            AddRuntimes(target);
            AddModules(target);
            AddHeapObjects(target);
            AddStackObjects(target);
            AddHandles(target);
            AddRefs(target);

            cmdTarget = new("COMMIT", db);
            cmdTarget.ExecuteNonQuery();

        }
    }
}
