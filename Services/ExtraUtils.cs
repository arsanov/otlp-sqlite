using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using OpenTelemetry.Proto.Common.V1;

namespace OtlpServer
{
    public static class ExtraUtils
    {
        public static object SerializeAnyValue(AnyValue anyValue)
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
    }
}
