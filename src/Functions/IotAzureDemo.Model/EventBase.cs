using System;
using Newtonsoft.Json;

namespace IotAzureDemo.Model
{
    public class EventBase
    {
        public virtual string EventType { get; set; }
        public virtual DateTime EventTime { get; set; }
        public virtual string DeviceName { get; set; }
        public virtual string DeviceId { get; set; }
        public long EventTimeMs => ((DateTimeOffset)EventTime).ToUnixTimeMilliseconds();
    }
}