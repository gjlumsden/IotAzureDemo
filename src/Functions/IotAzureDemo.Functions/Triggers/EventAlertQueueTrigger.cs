using IotAzureDemo.Model;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SendGrid.Helpers.Mail;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace IotAzureDemo.Functions.Triggers
{
    public static class EventAlertQueueTrigger
    {
        private static string alertSenderName = Environment.GetEnvironmentVariable(nameof(alertSenderName));
        private static string alertSenderAddress = Environment.GetEnvironmentVariable(nameof(alertSenderAddress));

        [FunctionName("EventAlertFunction")]
        public static async Task Run([QueueTrigger("%alertsQueueName%", Connection ="alertStorageQueueConnection")]string message, 
            [SendGrid(ApiKey = "sendgridKey")]IAsyncCollector<SendGrid.Helpers.Mail.SendGridMessage> mail, ILogger log)
        {
            try
            {
                var evt = JsonConvert.DeserializeObject<EventBase>(message);
                var recipients = Environment.GetEnvironmentVariable("emailAlertRecipients").Split(new[] { ',' },
                    StringSplitOptions.RemoveEmptyEntries);

                if (evt.EventType.ToString().Equals("maintenance", StringComparison.InvariantCultureIgnoreCase))
                {
                    var maintenanceEvent = JsonConvert.DeserializeObject<MaintenanceEvent>(message);

                    var emails =
                        recipients.Select(x => CreateEmail(x, $"Maintenance Alert: {maintenanceEvent.DeviceName} - {maintenanceEvent.Action}", GetMaintenanceEventEmailContent(maintenanceEvent)));
                    foreach (var e in emails)
                    {
                        await mail.AddAsync(e);
                    }
                }
                else
                {
                    var thresholdEvent = JsonConvert.DeserializeObject<ThresholdEvent>(message);
                    var emails =
                        recipients.Select(x => CreateEmail(x, $"Threshold Alert: {thresholdEvent.DeviceName} - {thresholdEvent.EventType}", GetThresholdEventEmailContent(thresholdEvent)));
                    
                    foreach (var e in emails)
                    {
                        await mail.AddAsync(e);
                    }
                }
            }
            catch(Exception ex)
            {
                log.LogError(0, ex, "Failed to send alerts.");
            }
        }

        private static SendGridMessage CreateEmail(string x, string subject, string content)
        {
            var msg = new SendGridMessage();
            msg.SetFrom(new EmailAddress(alertSenderAddress, alertSenderName));
            msg.SetSubject(subject);
            msg.AddTo(new EmailAddress(x));
            msg.AddContent("text/plain", content);
            return msg;
        }

        private static string GetThresholdEventEmailContent(ThresholdEvent thresholdEvent)
        {
            return $"{thresholdEvent.Severity} threshold crossed on detected Device {thresholdEvent.DeviceName}." +
                   Environment.NewLine +
                   $"{thresholdEvent.EventType} crossed the event threshold at {thresholdEvent.EventTime:yyyy-MM-ddTHH:mm:ssZ}." +
                   Environment.NewLine +
                   $"The value when the threshold was crossed was {thresholdEvent.CurrentValue}." + Environment.NewLine +
                   $"This value was observed for {thresholdEvent.DurationSeconds} seconds before the event was raised.";
        }

        private static string GetMaintenanceEventEmailContent(MaintenanceEvent maintenanceEvent)
        {
            return
                $"A {maintenanceEvent.Action} maintenance action was {(maintenanceEvent.Succeeded ? "successfully" : "not successfully")} performed at {maintenanceEvent.EventTime:yyyy-MM-ddTHH:mm:ssZ} on the equipment monitored by device {maintenanceEvent.DeviceName}.";
        }
    }
}