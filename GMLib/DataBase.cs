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
using System.Security.Cryptography;
using System.Diagnostics;
using System.Threading;
using System.Collections;
using System.Reflection.Metadata;


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
            // Parameterizing query because of sql injection bug before
            SqliteCommand insertCommand = new SqliteCommand("INSERT INTO Processes(Pid, Arch, Date, Path, Args) VALUES(@Pid, @Arch, @Date, @Path, @Args)", db);

            SqliteParameter param = new SqliteParameter("@Pid", process.Pid);
            insertCommand.Parameters.Add(new SqliteParameter()
            {
                ParameterName = "@Pid",
                Value = process.Pid
            });
            insertCommand.Parameters.Add(new SqliteParameter()
            {
                ParameterName = "@Arch",
                Value = process.Arch != null ? process.Arch : DBNull.Value 
            });
            insertCommand.Parameters.Add(new SqliteParameter()
            {
                ParameterName = "@Date",
                Value = process.Date != null ? process.Date : DBNull.Value
            });
            insertCommand.Parameters.Add(new SqliteParameter()
            {
                ParameterName = "@Path",
                Value = process.Path != null ? process.Path : DBNull.Value
            });
            insertCommand.Parameters.Add(new SqliteParameter()
            {
                ParameterName = "@Args",
                Value = process.Args != null ? process.Args : DBNull.Value
            });


            insertCommand.ExecuteNonQuery();
        }

        public void AddSnapshot(GMSnapshot snapshot)
        {
            if (!initialized)
            {
                InitializeDatabase();
                initialized = true;
            }

            // parameterizing query
            SqliteCommand insertCommand = new SqliteCommand("INSERT INTO Snapshots(Id,Pid,Time,PointerSize) VALUES(@Id,@Pid,@Time,@PointerSize)", db);

            insertCommand.Parameters.Add(new SqliteParameter()
            {
                ParameterName = "@Id",
                Value = snapshot.Id
            });
            insertCommand.Parameters.Add(new SqliteParameter()
            {
                ParameterName = "@Pid",
                Value = snapshot.Pid 
            });
            insertCommand.Parameters.Add(new SqliteParameter()
            {
                ParameterName = "@Time",
                Value = snapshot.Time 
            });
            insertCommand.Parameters.Add(new SqliteParameter()
            {
                ParameterName = "@PointerSize",
                Value = snapshot.PointerSize 
            });

            insertCommand.ExecuteNonQuery();
        }
        
        private void AddObject(Target target, GMObject obj)
        {
            SqliteCommand insertCommand;

            // Get the data value for object
            object objData = obj.Value();
            if (objData != null)
            {

                // parameterizing query
                insertCommand = new SqliteCommand("INSERT OR IGNORE INTO Objects(Id,ObjectId,Address,Type,Size,Value) VALUES(@Id,@ObjectId,@Address,@Type,@Size,@Value)", db);

                insertCommand.Parameters.Add(new SqliteParameter()
                {
                    ParameterName = "@Id",
                    Value = target.Id
                });
                insertCommand.Parameters.Add(new SqliteParameter()
                {
                    ParameterName = "@ObjectId",
                    Value = obj.Index 
                });
                insertCommand.Parameters.Add(new SqliteParameter()
                {
                    ParameterName = "@Address",
                    Value = obj.Address
                });
                insertCommand.Parameters.Add(new SqliteParameter()
                {
                    ParameterName = "@Type",
                    Value = obj.Type() != null ? obj.Type() : DBNull.Value
                });
                insertCommand.Parameters.Add(new SqliteParameter()
                {
                    ParameterName = "@Size",
                    Value = obj.Size
                });

                // Store bytes as blob
                if (objData is System.Byte[])
                {
                    insertCommand.Parameters.Add(new SqliteParameter()
                    {
                        ParameterName = "@Value",
                        Value = objData != null ? objData : DBNull.Value,
                        DbType = System.Data.DbType.Binary
                    });
                }
                else
                    insertCommand.Parameters.Add("@Value", SqliteType.Text).Value = objData;
                insertCommand.ExecuteNonQuery();
            }
            else
            {
                insertCommand = new SqliteCommand("INSERT OR IGNORE INTO Objects(Id,ObjectId,Address,Type,Size) VALUES(@Id,@ObjectId,@Address,@Type,@Size)", db);


                insertCommand.Parameters.Add(new SqliteParameter()
                {
                    ParameterName = "@Id",
                    Value = target.Id
                });
                insertCommand.Parameters.Add(new SqliteParameter()
                {
                    ParameterName = "@ObjectId",
                    Value = obj.Index
                });
                insertCommand.Parameters.Add(new SqliteParameter()
                {
                    ParameterName = "@Address",
                    Value = obj.Address
                });
                insertCommand.Parameters.Add(new SqliteParameter()
                {
                    ParameterName = "@Type",
                    Value = obj.Type() != null ? obj.Type() : DBNull.Value
                });
                insertCommand.Parameters.Add(new SqliteParameter()
                {
                    ParameterName = "@Size",
                    Value = obj.Size 
                });
                insertCommand.ExecuteNonQuery();
            }
        }
        private void AddRefs(Target target)
        {
            foreach (GMRef referece in target.Refs)
            {
                SqliteCommand insertCommand = new SqliteCommand("INSERT OR IGNORE INTO Refs(Id,Address,Ref) VALUES(@Id,@Address,@Ref)", db);


                insertCommand.Parameters.Add(new SqliteParameter()
                {
                    ParameterName = "@Id",
                    Value = target.Id
                });
                insertCommand.Parameters.Add(new SqliteParameter()
                {
                    ParameterName = "@Address",
                    Value = referece.Address
                });
                insertCommand.Parameters.Add(new SqliteParameter()
                {
                    ParameterName = "@Ref",
                    Value = referece.Ref 
                });
                insertCommand.ExecuteNonQuery();
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
            SqliteCommand insertCommand;

            // Add threads
            foreach (GMThread thread in target.Threads)
            {

                insertCommand = new SqliteCommand("INSERT INTO Threads(Id,Tid,Context) VALUES(@Id,@Tid,@Context)", db);
                insertCommand.Parameters.Add(new SqliteParameter()
                {
                    ParameterName = "@Id",
                    Value = thread.Id
                });
                insertCommand.Parameters.Add(new SqliteParameter()
                {
                    ParameterName = "@Tid",
                    Value = thread.Tid 
                });
                insertCommand.Parameters.Add(new SqliteParameter()
                {
                    ParameterName = "@Context",
                    Value = thread.Context != null ? thread.Context : DBNull.Value,
                    DbType = System.Data.DbType.Binary
                });
                insertCommand.ExecuteNonQuery();
            }

            // Add frames
            foreach (GMFrame frame in target.Frames)
            {
                string frameStr = frame.Frame;

                insertCommand = new SqliteCommand("INSERT OR IGNORE INTO Frames(Id,Tid,StackPtr,IP,Frame) VALUES(@Id,@Tid,@StackPtr,@IP,@Frame)", db);
                insertCommand.Parameters.Add(new SqliteParameter()
                {
                    ParameterName = "@Id",
                    Value = target.Id
                });
                insertCommand.Parameters.Add(new SqliteParameter()
                {
                    ParameterName = "@Tid",
                    Value = frame.Tid
                });
                insertCommand.Parameters.Add(new SqliteParameter()
                {
                    ParameterName = "@StackPtr",
                    Value = frame.StackPtr
                });
                insertCommand.Parameters.Add(new SqliteParameter()
                {
                    ParameterName = "@IP",
                    Value = frame.Ip
                });
                insertCommand.Parameters.Add(new SqliteParameter()
                {
                    ParameterName = "@Frame",
                    Value = frameStr != null ? frameStr : DBNull.Value
                });
                insertCommand.ExecuteNonQuery();
            }

            // Add stacks
            foreach (GMStack stack in target.Stacks)
            {
                insertCommand = new SqliteCommand("INSERT OR IGNORE INTO Stacks(Id,StackPtr,Tid,Object) VALUES(@Id,@StackPtr,@Tid,@Object)", db);
                insertCommand.Parameters.Add(new SqliteParameter()
                {
                    ParameterName = "@Id",
                    Value = target.Id
                });
                insertCommand.Parameters.Add(new SqliteParameter()
                {
                    ParameterName = "@StackPtr",
                    Value = stack.StackPtr
                });
                insertCommand.Parameters.Add(new SqliteParameter()
                {
                    ParameterName = "@Tid",
                    Value = stack.Tid
                });
                insertCommand.Parameters.Add(new SqliteParameter()
                {
                    ParameterName = "@Object",
                    Value = stack.Object 
                });
                insertCommand.ExecuteNonQuery();
            }
        }
        private void AddHandles(Target target)
        {
            SqliteCommand insertCommand;
            foreach (GMHandle handle in target.Handles)
            {

                insertCommand = new SqliteCommand("INSERT INTO Handles(Id,Address,Object,Kind) VALUES(@Id,@Address,@Object,@Kind)", db);
                insertCommand.Parameters.Add(new SqliteParameter()
                {
                    ParameterName = "@Id",
                    Value = target.Id
                });
                insertCommand.Parameters.Add(new SqliteParameter()
                {
                    ParameterName = "@Address",
                    Value = handle.Address
                });
                insertCommand.Parameters.Add(new SqliteParameter()
                {
                    ParameterName = "@Object",
                    Value = handle.Object 
                });
                insertCommand.Parameters.Add(new SqliteParameter()
                {
                    ParameterName = "@Kind",
                    Value = handle.Kind != null ? handle.Kind : DBNull.Value
                });
                insertCommand.ExecuteNonQuery();
            }
        }
        private void AddAppDomains(Target target)
        {
            SqliteCommand insertCommand;

            HashSet<long> appdomainSet = new();
            foreach (GMAppDomain domain in target.AppDomains)
            {
                if (appdomainSet.Add(domain.Aid))
                {

                    insertCommand = new SqliteCommand("INSERT INTO AppDomains(Id,Aid,Name,Address) VALUES(@Id,@Aid,@Name,@Address)", db);
                    insertCommand.Parameters.Add(new SqliteParameter()
                    {
                        ParameterName = "@Id",
                        Value = target.Id
                    });
                    insertCommand.Parameters.Add(new SqliteParameter()
                    {
                        ParameterName = "@Aid",
                        Value = domain.Aid
                    });
                    insertCommand.Parameters.Add(new SqliteParameter()
                    {
                        ParameterName = "@Name",
                        Value = domain.Name != null ? domain.Name : DBNull.Value
                    });
                    insertCommand.Parameters.Add(new SqliteParameter()
                    {
                        ParameterName = "@Address",
                        Value = domain.Address 
                    });
                    insertCommand.ExecuteNonQuery();
                }
            }
        }

        private void AddRuntimes(Target target)
        {
            SqliteCommand insertCommand;

            foreach (GMRuntime info in target.Runtimes)
            {

                insertCommand = new SqliteCommand("INSERT INTO Runtimes(Id,Version,Dac,Arch) VALUES(@Id,@Version,@Dac,@Arch)", db);
                insertCommand.Parameters.Add(new SqliteParameter()
                {
                    ParameterName = "@Id",
                    Value = target.Id
                });
                insertCommand.Parameters.Add(new SqliteParameter()
                {
                    ParameterName = "@Version",
                    Value = info.Version != null ? info.Version : DBNull.Value
                });
                insertCommand.Parameters.Add(new SqliteParameter()
                {
                    ParameterName = "@Dac",
                    Value = info.Dac != null ? info.Dac : DBNull.Value
                });
                insertCommand.Parameters.Add(new SqliteParameter()
                {
                    ParameterName = "@Arch",
                    Value = info.Arch != null ? info.Arch : DBNull.Value
                });
                insertCommand.ExecuteNonQuery();
            }
        }

        // XXX: AppDomain id (Aid) as a key here!!
        private void AddModules(Target target)
        {
            SqliteCommand insertCommand;

            HashSet<ulong> modSet = new();
            foreach (GMModule module in target.Modules)
            {
                if (modSet.Add(module.AsmAddress))
                {

                    insertCommand = new SqliteCommand("INSERT INTO Modules(Id,AsmAddress,ImgAddress,Name,AsmName,IsDynamic,IsPe) VALUES(@Id,@AsmAddress,@ImgAddress,@Name,@AsmName,@IsDynamic,@IsPe)", db);
                    insertCommand.Parameters.Add(new SqliteParameter()
                    {
                        ParameterName = "@Id",
                        Value = target.Id
                    });
                    insertCommand.Parameters.Add(new SqliteParameter()
                    {
                        ParameterName = "@AsmAddress",
                        Value = module.AsmAddress 
                    });
                    insertCommand.Parameters.Add(new SqliteParameter()
                    {
                        ParameterName = "@ImgAddress",
                        Value = module.ImgAddress 
                    });
                    insertCommand.Parameters.Add(new SqliteParameter()
                    {
                        ParameterName = "@Name",
                        Value = module.Name != null ? module.Name : DBNull.Value
                    });
                    insertCommand.Parameters.Add(new SqliteParameter()
                    {
                        ParameterName = "@AsmName",
                        Value = module.AsmName != null ? module.AsmName : DBNull.Value
                    });
                    insertCommand.Parameters.Add(new SqliteParameter()
                    {
                        ParameterName = "@IsDynamic",
                        Value = module.IsDynamic 
                    });
                    insertCommand.Parameters.Add(new SqliteParameter()
                    {
                        ParameterName = "@IsPe",
                        Value = module.IsPe 
                    });
                    insertCommand.ExecuteNonQuery();
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
