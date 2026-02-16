using System;

namespace AspireResourceServer.Services
{
    public class ResourceManagerSettings
    {
        public string ServerName { get; set; }
        public bool LoadFromDb { get; set; }
        public TimeSpan DbTraceSince { get; set; }
    }
}