using System;
using Microsoft.EntityFrameworkCore;

namespace OtlpServer.Db
{
    public class TraceContext : DbContext
    {
        private readonly TraceContextSettings settings;

        public TraceContext(TraceContextSettings settings)
        {
            this.settings = settings;
        }

        public DbSet<TraceEntry> TraceEntries { get; set; }
        public DbSet<MetricEntry> MetricEntries { get; set; }
        public DbSet<LogEntry> LogEntries { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var connectionString = $"Data Source={settings.DbPath}";
            optionsBuilder.UseSqlite(connectionString);
        }
    }
}
