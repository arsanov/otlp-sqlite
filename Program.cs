using System;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Metrics.V1;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Metrics.V1;
using OpenTelemetry.Proto.Trace.V1;
using OtlpServer.Db;
using OtlpServer.Extensions;
using Settings.Extensions.Configuration;
using static OpenTelemetry.Proto.Collector.Logs.V1.LogsService;
using static OpenTelemetry.Proto.Collector.Metrics.V1.MetricsService;
using static OpenTelemetry.Proto.Collector.Trace.V1.TraceService;

namespace OtlpServer
{
    public static class Program
    {
        private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddGrpc();

            services.AddDbContext<TraceContext>().AddSettings<TraceContextSettings>(configuration);

            services.AddHostedService<SqliteWritingService>();
            services.AddHostedService<MigrationStartupService>();

            services.AddSingleton<IObservable<Span>, ISubject<Span>, Subject<Span>>();
            services.AddSingleton<IObservable<LogRecord>, ISubject<LogRecord>, Subject<LogRecord>>();
        }

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            ConfigureServices(builder.Services, builder.Configuration);

            var app = builder.Build();

            app.MapGrpcService<TraceService>();
            app.MapGrpcService<LogsService>();
            app.MapGrpcService<MetricsService>();

            app.MapGet("/", () => "OTLP gRPC server is running");

            app.Run();
        }

        private class TraceService(ISubject<Span> spanStream) : TraceServiceBase
        {
            public override Task<ExportTraceServiceResponse> Export(ExportTraceServiceRequest request, ServerCallContext context)
            {
                spanStream.OnNextRange(request.ResourceSpans.SelectMany(sc => sc.ScopeSpans.SelectMany(s => s.Spans)));
                return Task.FromResult(new ExportTraceServiceResponse());
            }
        }
        private class LogsService(ISubject<LogRecord> spanStream) : LogsServiceBase
        {
            public override Task<ExportLogsServiceResponse> Export(ExportLogsServiceRequest request, ServerCallContext context)
            {
                spanStream.OnNextRange(request.ResourceLogs.SelectMany(sc => sc.ScopeLogs.SelectMany(s => s.LogRecords)));
                return Task.FromResult(new ExportLogsServiceResponse());
            }
        }
        private class MetricsService(ISubject<Metric> spanStream) : MetricsServiceBase
        {
            public override Task<ExportMetricsServiceResponse> Export(ExportMetricsServiceRequest request, ServerCallContext context)
            {
                spanStream.OnNextRange(request.ResourceMetrics.SelectMany(sc => sc.ScopeMetrics.SelectMany(s => s.Metrics)));
                return Task.FromResult(new ExportMetricsServiceResponse());
            }
        }
    }
}