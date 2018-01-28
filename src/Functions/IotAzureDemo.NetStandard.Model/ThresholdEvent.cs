using System;

namespace IotAzureDemo.Model
{
    public class ThresholdEvent : EventBase
    {
        public virtual double CurrentValue { get; set; }
        public virtual double BeforeThreshold { get; set; }
        public virtual DateTime BeforeThresholdTime { get; set; }
        public virtual int DurationSeconds { get; set; }
        public virtual string EquipmentStatus { get; set; }
        public virtual string Severity { get; set; }

    }
}