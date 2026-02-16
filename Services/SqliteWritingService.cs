using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aspire.DashboardService.Proto.V1;
using AspireResourceServer.Services;
using Google.Protobuf.Collections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Metrics.V1;
using OpenTelemetry.Proto.Trace.V1;
using OtlpServer.Db;
using OtlpServer.Utils;

namespace OtlpServer
{
    public class SqliteWritingService : IHostedService
    {
        private readonly IObservable<TraceData> spanStream;
        private readonly IObservable<LogRecord> logStream;
        private readonly IServiceScopeFactory scopeFactory;
        private readonly SingleThreadSynchronizationContext syncContext;
        private IDisposable subscription;

        public SqliteWritingService(IObservable<TraceData> spanStream,
        IObservable<LogRecord> logStream,
        IServiceScopeFactory scopeFactory,
        SingleThreadSynchronizationContext syncContext)
        {
            this.spanStream = spanStream;
            this.logStream = logStream;
            this.scopeFactory = scopeFactory;
            this.syncContext = syncContext;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            syncContext.ContextStarted.ContinueWith(t =>
            {
                subscription =
                     spanStream.Select(FromTraceData).Merge(logStream.Select(FromLog))
                     .Buffer(TimeSpan.FromSeconds(10))
                     .ObserveOn(syncContext)
                     .Subscribe(OnNewEntry);
            });
            return Task.CompletedTask;
        }

        private async void OnNewEntry(IList<Action<TraceContext>> list)
        {
            if (list.Any())
            {
                using var scope = scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<TraceContext>();
                foreach (var item in list)
                {
                    item(context);
                }
                await context.SaveChangesAsync();
            }
        }

        private static Action<TraceContext> FromTraceData(TraceData traceData)
        {
            static TraceEntry convertSpan(TraceData traceData)
            {
                var span = traceData.Span;
                var scope = traceData.Scope;
                var resource = traceData.Resource;
                return new TraceEntry
                {
                    TraceId = new Guid(span.TraceId.ToByteArray()),
                    SpanId = BitConverter.ToUInt64(span.SpanId.Span),
                    Kind = (int)span.Kind,
                    Attributes = GetAttributes(span.Attributes),
                    ScopeAttributes = GetAttributes(scope.Attributes),
                    ResourceAttributes = GetAttributes(resource.Attributes),
                    Name = span.Name,
                    ParentSpanId = span.ParentSpanId.IsEmpty ? null : BitConverter.ToUInt64(span.ParentSpanId.Span),
                    StatusMessage = span.Status?.Message,
                    StatusCode = (int?)span.Status?.Code,
                    TraceState = span.TraceState,
                    StartTimeUnixNano = span.StartTimeUnixNano,
                    EndTimeUnixNano = span.EndTimeUnixNano
                };
            }

            return c => c.TraceEntries.Add(convertSpan(traceData));
        }

        private static Action<TraceContext> FromLog(LogRecord log)
        {
            static LogEntry convertLog(LogRecord log)
            {
                return new LogEntry
                {
                    TraceId = log.TraceId.IsEmpty ? null : new Guid(log.TraceId.ToByteArray()),
                    SpanId = log.SpanId.IsEmpty ? null : BitConverter.ToUInt64(log.SpanId.Span),
                    Attributes = GetAttributes(log.Attributes),
                    Body = JsonSerializer.Serialize(ExtraUtils.SerializeAnyValue(log.Body)),
                    EventName = log.EventName,
                    Flags = log.Flags,
                    ObservedTimeUnixNano = log.ObservedTimeUnixNano,
                    SeverityNumber = log.SeverityNumber,
                    SeverityText = log.SeverityText,
                    TimeUnixNano = log.TimeUnixNano,
                };
            }

            return c => c.LogEntries.Add(convertLog(log));
        }

        private static Action<TraceContext> FromLog(Metric metric)
        {
            static MetricEntry convertMetric(Metric metric)
            {
                var result = new MetricEntry
                {
                    Kind = (int)metric.DataCase
                };
                return result;
            }

            return c => c.MetricEntries.Add(convertMetric(metric));
        }

        private static string GetAttributes(RepeatedField<KeyValue> values)
        {
            return JsonSerializer.Serialize(values.ToDictionary(kv => kv.Key, kv => ExtraUtils.SerializeAnyValue(kv.Value)));
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            subscription.Dispose();
            return Task.CompletedTask;
        }
    }
}
