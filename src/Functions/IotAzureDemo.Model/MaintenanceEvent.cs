namespace IotAzureDemo.Model
{
    public class MaintenanceEvent : EventBase
    {
        public string Action { get; set; }
        public bool Succeeded { get; set; }
    }
}