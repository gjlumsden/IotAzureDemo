using System;
using Newtonsoft.Json;

namespace IotAzureDemo.Model
{
    public class AsaThresholdEvent : ThresholdEvent
    {

        [JsonProperty("eventtype")]
        public override string EventType { get; set; }
        [JsonProperty("eventtime")]
        public override DateTime EventTime { get; set; }
        [JsonProperty("name")]
        public override string DeviceName { get; set; }
        [JsonProperty("deviceid")]
        public override string DeviceId { get; set; }
        [JsonProperty("currentvalue")]
        public override double CurrentValue { get; set; }
        [JsonProperty("beforethreshold")]
        public override double BeforeThreshold { get; set; }
        [JsonProperty("beforethresholdtime")]
        public override DateTime BeforeThresholdTime { get; set; }
        [JsonProperty("duration")]
        public override int DurationSeconds { get; set; }
        [JsonProperty("equipmentstatus")]
        public override string EquipmentStatus { get; set; }
        [JsonProperty("severity")]
        public override string Severity { get; set; }

        public ThresholdEvent AsThresholdEvent()
        {
            return new ThresholdEvent
            {
                BeforeThreshold = BeforeThreshold,
                BeforeThresholdTime = BeforeThresholdTime,
                CurrentValue = CurrentValue,
                DeviceId = DeviceId,
                DeviceName = DeviceName,
                DurationSeconds = DurationSeconds,
                EquipmentStatus = EquipmentStatus,
                EventTime = EventTime,
                EventType = EventType,
                Severity = Severity
            };
        }
    }
}