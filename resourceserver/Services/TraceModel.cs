using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Google.Protobuf.Collections;
using OpenTelemetry.Proto.Common.V1;
using OtlpServer;

namespace AspireResourceServer.Services
{

    public class TraceModel
    {
        public ActivityTraceId TraceId { get; set; }
        public ActivitySpanId SpanId { get; set; }
        public Dictionary<string, JsonNode> Attributes { get; set; }
        public Dictionary<string, JsonNode> ScopeAttributes { get; set; }
        public Dictionary<string, JsonNode> ResourceAttributes { get; set; }
        public string ServiceName { get; set; }
        public string ServiceInstanceId { get; set; }
        public string Version { get; set; }
        public string Hostname { get; set; }
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }
        public ActivityKind Kind { get; set; }
        public int? StatusCode { get; set; }
        public string Environment { get; set; }
        public string Name { get; set; }
        public string ServiceNamespace { get; set; }
        public string ContainerId { get; set; }

        private static ActivityKind FromDb(int kind)
        {
            return kind switch
            {
                0 or 1 => ActivityKind.Internal,
                2 => ActivityKind.Server,
                3 => ActivityKind.Client,
                4 => ActivityKind.Producer,
                5 => ActivityKind.Consumer,
                _ => ActivityKind.Internal,
            };
        }

        public static TraceModel FromProtobuf(TraceData traceData)
        {
            var span = traceData.Span;
            var scope = traceData.Scope;
            var resource = traceData.Resource;
            var attributes = JsonSerializer.Deserialize<Dictionary<string, JsonNode>>(GetAttributes(span.Attributes));
            var scopeAttributes = JsonSerializer.Deserialize<Dictionary<string, JsonNode>>(GetAttributes(scope.Attributes));
            var resourceAttributes = JsonSerializer.Deserialize<Dictionary<string, JsonNode>>(GetAttributes(resource.Attributes));

            var result = new TraceModel
            {
                TraceId = ActivityTraceId.CreateFromBytes(span.TraceId.ToByteArray()),
                SpanId = ActivitySpanId.CreateFromBytes(span.SpanId.ToByteArray()),
                Name = span.Name,
                Attributes = attributes,
                ScopeAttributes = scopeAttributes,
                ResourceAttributes = resourceAttributes,
                StartTime = DateTimeOffset.FromUnixTimeMilliseconds((long)(span.StartTimeUnixNano / 1000000)),
                EndTime = DateTimeOffset.FromUnixTimeMilliseconds((long)(span.EndTimeUnixNano / 1000000)),
                Kind = FromDb((int)span.Kind),
                StatusCode = (int?)(span.Status?.Code),
                ServiceName = resourceAttributes.TryGetValue("service.name")?.GetValue<string>(),
                ServiceNamespace = resourceAttributes.TryGetValue("service.namespace")?.GetValue<string>(),
                ServiceInstanceId = resourceAttributes.TryGetValue("service.instance.id")?.GetValue<string>(),
                Version = resourceAttributes.TryGetValue("service.version")?.GetValue<string>(),
                Hostname = resourceAttributes.TryGetValue("host.name")?.GetValue<string>(),
                Environment = resourceAttributes.TryGetValue("env")?.GetValue<string>(),
                ContainerId = resourceAttributes.TryGetValue("container.id")?.GetValue<string>()
            };
            return result;
        }

        private static string GetAttributes(RepeatedField<KeyValue> values)
        {
            return JsonSerializer.Serialize(values.ToDictionary(kv => kv.Key, kv => ExtraUtils.SerializeAnyValue(kv.Value)));
        }

        // public static TraceModel FromDb(TraceEntry traceEntry)
        // {
        //     var resourceAttributes = JsonSerializer.Deserialize<Dictionary<string, JsonNode>>(traceEntry.ResourceAttributes);
        //     var startTime = DateTimeOffset.FromUnixTimeMilliseconds((long)(traceEntry.StartTimeUnixNano / 1000000));
        //     var endTime = DateTimeOffset.FromUnixTimeMilliseconds((long)(traceEntry.EndTimeUnixNano / 1000000));
        //     var result = new TraceModel
        //     {
        //         TraceId = ActivityTraceId.CreateFromBytes(traceEntry.TraceId.ToByteArray()),
        //         SpanId = ActivitySpanId.CreateFromBytes(BitConverter.GetBytes(traceEntry.SpanId)),
        //         Name = traceEntry.Name,
        //         Attributes = JsonSerializer.Deserialize<Dictionary<string, JsonNode>>(traceEntry.Attributes),
        //         ScopeAttributes = JsonSerializer.Deserialize<Dictionary<string, JsonNode>>(traceEntry.ScopeAttributes),
        //         ResourceAttributes = resourceAttributes,
        //         StartTime = startTime,
        //         EndTime = endTime,
        //         Kind = FromDb(traceEntry.Kind),
        //         StatusCode = traceEntry.StatusCode,
        //         ServiceName = resourceAttributes.TryGetValue("service.name")?.GetValue<string>(),
        //         ServiceNamespace = resourceAttributes.TryGetValue("service.namespace")?.GetValue<string>(),
        //         ServiceInstanceId = resourceAttributes.TryGetValue("service.instance.id")?.GetValue<string>(),
        //         Version = resourceAttributes.TryGetValue("service.version")?.GetValue<string>(),
        //         Hostname = resourceAttributes.TryGetValue("host.name")?.GetValue<string>(),
        //         Environment = resourceAttributes.TryGetValue("env")?.GetValue<string>(),
        //         ContainerId = resourceAttributes.TryGetValue("container.id")?.GetValue<string>()
        //     };
        //     return result;
        // }
    }
}