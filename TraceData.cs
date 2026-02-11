using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;

namespace OtlpServer
{
    public record TraceData(Resource Resource, InstrumentationScope Scope, Span Span);
}