using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.Devices;
using System.Configuration;
using Newtonsoft.Json;
using System;
using Functions.Model;

namespace SensorEventFunction
{
    public static class SensorEventMessageTrigger
    {
        static readonly ServiceClient Client = ServiceClient.CreateFromConnectionString(ConfigurationManager.AppSettings["DeviceManagementConnection"]);
        static readonly RegistryManager Registry = RegistryManager.CreateFromConnectionString(ConfigurationManager.AppSettings["DeviceManagementConnection"]);
        
        public static async Task Run(string message, ICollector<EventBase> events, TraceWriter log)
        {
            log.Info(message);
            var evts = JsonConvert.DeserializeObject<ThresholdEvent[]>(message);
            foreach (var evt in evts)
            {
                events.Add(evt); //Pass through to alerts.
                if (evt.Severity.Equals("critical", StringComparison.InvariantCultureIgnoreCase))
                {
                    await PerformMaintenanceAction("running", "Shutdown", evt, log, events);
                }
                if (evt.Severity.Equals("healthy", StringComparison.InvariantCultureIgnoreCase))
                {
                    await PerformMaintenanceAction("shutdown", "Start", evt, log, events);
                }
            }
        }

        private static async Task PerformMaintenanceAction(string statusConditionValue, string action, EventBase triggerEvent, TraceWriter log, ICollector<EventBase> events)
        {
            var status = await GetStatusFromTwin(triggerEvent.DeviceId, log);
            if ((status ?? string.Empty).Equals(statusConditionValue, StringComparison.InvariantCultureIgnoreCase) || string.IsNullOrWhiteSpace(status))
            {
                var result = await CallDirectMethod(log, triggerEvent.DeviceId, action.ToLower());
                events.Add(new MaintenanceEvent
                {
                    EventType = "Maintenance",
                    Action = action,
                    Succeeded = result,
                    DeviceId = triggerEvent.DeviceId,
                    DeviceName = triggerEvent.DeviceName,
                    EventTime = DateTime.UtcNow
                });
            }
        }

        private static async Task<string> GetStatusFromTwin(string deviceId, TraceWriter log)
        {
            try
            {
                var twin = await Registry.GetTwinAsync(deviceId);
                var status = twin.Properties.Reported.Contains("EquipmentStatus")
                    ? twin.Properties.Reported["EquipmentStatus"].ToString()
                    : null;
                return status;
            }
            catch (Exception ex)
            {
                log.Error("Failed to retrieve status.", ex);
                return null;
            }
        }

        private static async Task<bool> CallDirectMethod(TraceWriter log, string deviceId, string methodName)
        {
            await Client.OpenAsync();
            var method = new CloudToDeviceMethod(methodName)
            {
                ResponseTimeout = TimeSpan.FromSeconds(30)
            };
            try
            {
                var result = await Client.InvokeDeviceMethodAsync(deviceId, method);
                return result.Status == 200;
            }
            catch (Exception ex)
            {
                log.Error("Failed to call device method.", ex);
                return false;
            }
        }
    }
}