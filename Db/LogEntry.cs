using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using OpenTelemetry.Proto.Logs.V1;

namespace OtlpServer.Db
{
    public class LogEntry
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }
        public Guid? TraceId { get; set; }
        public ulong? SpanId { get; set; }
        public string Attributes { get; internal set; }
        public string Body { get; internal set; }
        public string EventName { get; internal set; }
        public uint Flags { get; internal set; }
        public ulong ObservedTimeUnixNano { get; internal set; }
        public SeverityNumber SeverityNumber { get; internal set; }
        public string SeverityText { get; internal set; }
        public ulong TimeUnixNano { get; internal set; }
    }
}
