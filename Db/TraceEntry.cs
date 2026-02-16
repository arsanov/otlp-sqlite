using System;
using Microsoft.EntityFrameworkCore;

namespace OtlpServer.Db
{
    [PrimaryKey(nameof(TraceId), nameof(SpanId))]
    public class TraceEntry
    {
        public Guid TraceId { get; set; }
        public ulong SpanId { get; set; }

        public int Kind { get; set; }
        public string Attributes { get; set; }
        public string ScopeAttributes { get; set; }
        public string ResourceAttributes { get; set; }
        public string Name { get; set; }
        public ulong? ParentSpanId { get; set; }
        public string StatusMessage { get; set; }
        public int? StatusCode { get; set; }
        public string TraceState { get; set; }
        public ulong StartTimeUnixNano { get; set; }
        public ulong EndTimeUnixNano { get; set; }
    }
}
