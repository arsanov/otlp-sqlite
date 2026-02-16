using System;
using System.ComponentModel;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace AspireResourceServer.Services
{
    /// <summary>
    /// Type converter for converting strings to TimeZoneInfo instances.
    /// </summary>
    public class EndpointConverter : TypeConverter
    {
        /// <summary>
        /// Determines whether this converter can convert from the specified type.
        /// </summary>
        /// <param name="context">The type descriptor context.</param>
        /// <param name="sourceType">The source type.</param>
        /// <returns>True if the source type is string; otherwise, false.</returns>
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string);
        }

        /// <summary>
        /// Converts the specified value to a TimeZoneInfo instance.
        /// </summary>
        /// <param name="context">The type descriptor context.</param>
        /// <param name="culture">The culture info.</param>
        /// <param name="value">The value to convert.</param>
        /// <returns>A TimeZoneInfo instance or the base conversion result.</returns>
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string timeZoneId)
            {
                var match = Regex.Match(timeZoneId, "^(?<host>.+):(?<port>\\d+)$");
                if (match.Success)
                {
                    var host = match.Groups["host"].Value;
                    var port = Int32.Parse(match.Groups["port"].Value);
                    return ToEndpoint(host, port);
                }
                if (TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var result))
                {
                    return result;
                }
            }

            return base.ConvertFrom(context, culture, value);
        }

        public static EndPoint ToEndpoint(string host, int port)
        {
            if (IPAddress.TryParse(host, out var iPAddress))
            {
                return new IPEndPoint(iPAddress, port);
            }
            else
            {
                return new DnsEndPoint(host, port, System.Net.Sockets.AddressFamily.InterNetwork);
            }
        }
    }
}