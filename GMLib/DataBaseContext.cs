using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;


namespace GMLib
{
    public class DatabaseContext : DbContext
    {
        public string dbPath { get; set; }
        public DatabaseContext(string path)
        {
            dbPath = path;
        }

        public DatabaseContext(DbContextOptions<DatabaseContext> options)
            : base(options)
        {
        }

        public virtual DbSet<GMAppDomain> AppDomains { get; set; }
        public virtual DbSet<GMFrame> Frames { get; set; }
        public virtual DbSet<GMHandle> Handles { get; set; }
        public virtual DbSet<GMModule> Modules { get; set; }
        public virtual DbSet<GMObjectData> Objects { get; set; }
        public virtual DbSet<GMProcess> Processes { get; set; }
        public virtual DbSet<GMRef> Refs { get; set; }
        public virtual DbSet<GMRuntime> Runtimes { get; set; }
        public virtual DbSet<GMSnapshot> Snapshots { get; set; }
        public virtual DbSet<GMStack> Stacks { get; set; }
        public virtual DbSet<GMThread> Threads { get; set; }
        public virtual DbSet<GMSetting> Settings { get; set; }
        public virtual DbSet<GMBookmark> Bookmarks { get; set; }


        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseSqlite($"Data Source={dbPath};Pooling=False;");
            //options.UseSqlite($"Data Source={dbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<GMAppDomain>(entity =>
            {
                entity.HasKey(e => new { e.Id, e.Aid });
            });

            modelBuilder.Entity<GMFrame>(entity =>
            {
                entity.HasKey(e => new { e.Id, e.StackPtr });
            });

            modelBuilder.Entity<GMHandle>(entity =>
            {
                entity.HasKey(e => new { e.Id, e.Address });
            });

            // XXX: AppDomain id (Aid) as a key
            modelBuilder.Entity<GMModule>(entity =>
            {
                entity.HasKey(e => new { e.Id, e.AsmAddress });
            });

            modelBuilder.Entity<GMObjectData>(entity =>
            {
                entity.HasKey(e => new { e.Id, e.ObjectId });
            });

            modelBuilder.Entity<GMBookmark>(entity =>
            {
                entity.HasKey(e => new { e.Id, e.ObjectId });
            });

            modelBuilder.Entity<GMProcess>(entity =>
            {
                entity.HasKey(e => new { e.Pid });
            });

            modelBuilder.Entity<GMRef>(entity =>
            {
                entity.HasKey(e => new { e.Id, e.Address, e.Ref });
            });

            modelBuilder.Entity<GMRuntime>(entity =>
            {
                entity.HasKey(e => new { e.Id, e.Version });
            });

            modelBuilder.Entity<GMSnapshot>(entity =>
            {
                entity.HasKey(e => new { e.Id });
            });

            modelBuilder.Entity<GMSetting>(entity =>
            {
                entity.HasKey(e => new { e.Id });
            });

            modelBuilder.Entity<GMStack>(entity =>
            {
                entity.HasKey(e => new { e.Id, e.StackPtr });
            });

            modelBuilder.Entity<GMThread>(entity =>
            {
                entity.HasKey(e => new { e.Id, e.Tid });
            });

        }

    }
}
