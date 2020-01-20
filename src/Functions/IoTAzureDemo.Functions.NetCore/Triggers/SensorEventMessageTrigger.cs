using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.Devices;
using Newtonsoft.Json;
using System;
using System.Linq;
using IotAzureDemo.Model;
using Microsoft.Extensions.Logging;

namespace IotAzureDemo.Functions.Triggers
{
    public static class SensorEventMessageTrigger
    {
        static readonly ServiceClient Client = ServiceClient.CreateFromConnectionString(Environment.GetEnvironmentVariable("DeviceManagementConnection"));
        static readonly RegistryManager Registry = RegistryManager.CreateFromConnectionString(Environment.GetEnvironmentVariable("DeviceManagementConnection"));
        static bool emailEnabled = bool.Parse(Environment.GetEnvironmentVariable("EmailEnabled") ?? "false");

        [FunctionName("SensorEventFunction")]
        public static async Task Run([EventHubTrigger("telemetry", Connection = "sensorEventConnectionString", ConsumerGroup = "%eventsConsumerGroup%")]string message, 
            [Queue("%alertsQueueName%", Connection ="alertStorageQueueConnection")]IAsyncCollector<EventBase> events,
            [CosmosDB("IotDemoDb","Events",CollectionThroughput = 400,ConnectionStringSetting = "cosmosDbConnectionString",CreateIfNotExists =true, PartitionKey = "/DeviceId")]IAsyncCollector<EventBase> cosmosEvents,
            ILogger log, ExecutionContext context)
        {
            log.LogInformation(message);
            var evts = JsonConvert.DeserializeObject<AsaThresholdEvent[]>(message); //deserialise as an AsaThresholdEvent. This accounts for the nasty casing on json properties.
            foreach (var evt in evts.Select(x=>x.AsThresholdEvent()))
            {
                if (emailEnabled)
                {
                    await events.AddAsync((ThresholdEvent)evt); //Pass through to alerts.
                }
                await cosmosEvents.AddAsync((ThresholdEvent)evt); //Pass Through to CosmosDB
                if (evt.Severity.Equals("critical", StringComparison.InvariantCultureIgnoreCase))
                {
                    await PerformMaintenanceAction("running", "Shutdown", evt, log, events, cosmosEvents);
                }
                if (evt.Severity.Equals("healthy", StringComparison.InvariantCultureIgnoreCase))
                {
                    await PerformMaintenanceAction("shutdown", "Start", evt, log, events, cosmosEvents);
                }
            }
        }

        private static async Task PerformMaintenanceAction(string statusConditionValue, string action, EventBase triggerEvent, ILogger log, IAsyncCollector<EventBase> events, IAsyncCollector<EventBase> cosmosEvents)
        {
            var status = await GetStatusFromTwin(triggerEvent.DeviceId, log);
            if ((status ?? string.Empty).Equals(statusConditionValue, StringComparison.InvariantCultureIgnoreCase) || string.IsNullOrWhiteSpace(status))
            {
                var result = await CallDirectMethod(log, triggerEvent.DeviceId, action.ToLower());
                var evt = new MaintenanceEvent
                {
                    EventType = "Maintenance",
                    Action = action,
                    Succeeded = result,
                    DeviceId = triggerEvent.DeviceId,
                    DeviceName = triggerEvent.DeviceName,
                    EventTime = DateTime.UtcNow
                };
                await cosmosEvents.AddAsync(evt);
                if (emailEnabled)
                {
                    await events.AddAsync(evt);
                }
            }
        }

        private static async Task<string> GetStatusFromTwin(string deviceId, ILogger log)
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
                log.LogError(0, ex, "Failed to retrieve status.");
                return null;
            }
        }

        private static async Task<bool> CallDirectMethod(ILogger log, string deviceId, string methodName)
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
                log.LogError(0,ex,"Failed to call device method.");
                return false;
            }
        }
    }
}