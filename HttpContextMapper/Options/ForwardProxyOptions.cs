#nullable disable
using Microsoft.Extensions.Configuration;

namespace HttpContextMapper.Options
{
    /// <summary>
    /// A model class to be bound against <see cref="IConfiguration">IConfiguration</see>
    /// </summary>
    public class ForwardProxyOptions
    {
        /// <summary>
        /// The default root section key
        /// </summary>
        public const string ForwardProxy = "ForwardProxy";

        public string Host { get; set; }
        public string Port { get; set; }

        public int PortInt => int.TryParse(Port, out var portInt) ? portInt : 0;

        public bool IsValid => string.IsNullOrWhiteSpace(Host) is false && PortInt is not 0;
    }
}
