using System;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using AspireResourceServer.Services;
using Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Metrics.V1;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Metrics.V1;
using OtlpServer.Db;
using OtlpServer.Extensions;
using OtlpServer.Utils;
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

            services.AddSingleton<ResourceManager>().AddSingleton<IHostedService>(p => p.GetRequiredService<ResourceManager>()).AddSettings<ResourceManagerSettings>(configuration);
            services.AddSettings<HostMapSettings>(configuration);
            services.AddHostedService<SqliteWritingService>();
            services.AddHostedService<MigrationStartupService>();
            services.AddSingleton<SingleThreadSynchronizationContext>();
            services.AddHostedService(p => p.GetRequiredService<SingleThreadSynchronizationContext>());

            services.AddSingleton<IObservable<TraceData>, ISubject<TraceData>, Subject<TraceData>>();
            services.AddSingleton<IObservable<LogRecord>, ISubject<LogRecord>, Subject<LogRecord>>();
        }

        public static void Main(string[] args)
        {
            TypeDescriptor.AddAttributes(typeof(EndPoint), new TypeConverterAttribute(typeof(EndpointConverter)));
            var builder = WebApplication.CreateBuilder(args);
            ConfigureServices(builder.Services, builder.Configuration);

            var app = builder.Build();

            app.MapGrpcService<TraceService>();
            app.MapGrpcService<LogsService>();
            app.MapGrpcService<MetricsService>();
            app.MapGrpcService<DashboardService>();

            app.MapGet("/", () => "OTLP gRPC server is running");

            app.Run();
        }
        private class TraceService(ISubject<TraceData> traceDataStream) : TraceServiceBase
        {
            public override Task<ExportTraceServiceResponse> Export(ExportTraceServiceRequest request, ServerCallContext context)
            {
                traceDataStream.OnNextRange(request.ResourceSpans.SelectMany(sc => sc.ScopeSpans.SelectMany(s => s.Spans.Select(sp => new TraceData(sc.Resource, s.Scope, sp)))));
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