using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly IObservable<Span> spanStream;
        private readonly IObservable<LogRecord> logStream;
        private readonly IServiceProvider provider;

        private SingleThreadSynchronizationContext syncContext;
        private Thread runnerThread;
        private IDisposable subscription;

        public SqliteWritingService(IObservable<Span> spanStream, IObservable<LogRecord> logStream, IServiceProvider provider)
        {
            this.spanStream = spanStream;
            this.logStream = logStream;

            this.provider = provider;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            syncContext = new SingleThreadSynchronizationContext();
            runnerThread = new Thread(_ =>
            {
                syncContext.RunOnCurrentThread();
            });
            runnerThread.Start();
            subscription =
                spanStream.Select(FromSpan).Merge(logStream.Select(FromLog))
                .Buffer(TimeSpan.FromSeconds(10))
                .ObserveOn(syncContext)
                .Subscribe(OnNewEntry);
            return Task.CompletedTask;
        }

        private async void OnNewEntry(IList<Action<TraceContext>> list)
        {
            if (list.Any())
            {
                using var scope = provider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<TraceContext>();
                foreach (var item in list)
                {
                    item(context);
                }
                await context.SaveChangesAsync();
            }
        }

        private static Action<TraceContext> FromSpan(Span span)
        {
            static TraceEntry convertSpan(Span span)
            {
                return new TraceEntry
                {
                    TraceId = new Guid(span.TraceId.ToByteArray()),
                    SpanId = BitConverter.ToUInt64(span.SpanId.Span),
                    Kind = (int)span.Kind,
                    Attributes = GetAttributes(span.Attributes),
                    Name = span.Name,
                    ParentSpanId = span.ParentSpanId.IsEmpty ? null : BitConverter.ToUInt64(span.ParentSpanId.Span),
                    StatusMessage = span.Status?.Message,
                    StatusCode = (int?)span.Status?.Code,
                    TraceState = span.TraceState,
                    StartTimeUnixNano = span.StartTimeUnixNano,
                    EndTimeUnixNano = span.EndTimeUnixNano
                };
            }

            return c => c.TraceEntries.Add(convertSpan(span));
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
                    Body = JsonSerializer.Serialize(SerializeAnyValue(log.Body)),
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
            return JsonSerializer.Serialize(values.ToDictionary(kv => kv.Key, kv => SerializeAnyValue(kv.Value)));
        }

        private static object SerializeAnyValue(AnyValue anyValue)
        {
            return anyValue.ValueCase switch
            {
                AnyValue.ValueOneofCase.None => null,
                AnyValue.ValueOneofCase.StringValue => anyValue.StringValue,
                AnyValue.ValueOneofCase.BoolValue => anyValue.BoolValue,
                AnyValue.ValueOneofCase.IntValue => anyValue.IntValue,
                AnyValue.ValueOneofCase.DoubleValue => anyValue.DoubleValue,
                AnyValue.ValueOneofCase.ArrayValue => anyValue.ArrayValue.Values.Select(SerializeAnyValue).ToArray(),
                AnyValue.ValueOneofCase.KvlistValue => anyValue.KvlistValue.Values.Select(kv => new KeyValuePair<string, object>(kv.Key, SerializeAnyValue(kv.Value))).ToList(),
                AnyValue.ValueOneofCase.BytesValue => anyValue.BytesValue.ToByteArray(),
                _ => throw new Exception(),
            };
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            subscription.Dispose();
            syncContext.Complete();
            runnerThread.Join();
            return Task.CompletedTask;
        }
    }
}
