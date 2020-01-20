using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using System;
using Microsoft.Azure.Devices;
using IotAzureDemo.Functions.Utils;
using System.Collections.Generic;
using Microsoft.Azure.Devices.Common.Exceptions;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace IotAzureDemo.Functions.Triggers
{
    public static class DeviceRegistrationFunction
    {
        private static RegistryManager register = RegistryManager.CreateFromConnectionString(Environment.GetEnvironmentVariable("DeviceManagementConnection"));

        [FunctionName("DeviceRegistrationFunction")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = "devices")]HttpRequestMessage req, ILogger log)
        {
            try
            {
                var request = await req.Content.ReadAsAsync<dynamic>();
                if (request == null || request.NumDevices == null)
                    return req.CreateErrorResponse(HttpStatusCode.BadRequest, "NumDevices is required");
                if (request.ClearBeforeCreate != null && (bool)request.ClearBeforeCreate)
                {
                    await ClearExistingDevices();
                }

                var result = await GenerateDevices((int)request.NumDevices);

                return req.CreateResponse(HttpStatusCode.OK, result);
            }
            catch (Exception ex)
            {
                log.LogError("Failed to create devices", ex);
                return req.CreateErrorResponse(HttpStatusCode.InternalServerError, "Failed to generate devices.");
            }
        }

        private static async Task<List<dynamic>> GenerateDevices(int numDevices)
        {
            var ids = CreateDeviceIds(numDevices);
            var devices = ids.Select(x => new Device(x));
            var result = new ConcurrentBag<dynamic>();
            await Task.WhenAll(devices.Select(async device =>
            {
                Device d = null;
                d = await CreateDevice(device);
                result.Add(new { Id = d.Id, Key = d.Authentication.SymmetricKey.PrimaryKey });
            }));

            return result.ToList(); ;
        }

        private static async Task<Device> CreateDevice(Device device, bool retry = true)
        {
            Device d;
            try
            {
                d = await register.AddDeviceAsync(device);
            }
            catch (DeviceAlreadyExistsException)
            {

                if (retry)
                {
                    device = new Device(DeviceIdGenerator.GetRandomName(1));
                    try
                    {
                        d = await CreateDevice(device, false);
                    }
                    catch (DeviceAlreadyExistsException)
                    {
                        d = await register.GetDeviceAsync(device.Id);
                    }
                }
                else
                {
                    throw;
                }
            }

            return d;
        }

        private static async Task ClearExistingDevices()
        {
            var existing = await register.GetDevicesAsync(5000);
            if (existing.Any())
            {
                var currentSkip = 0;
                var exhausted = false;
                while (!exhausted)
                {
                    var batch = existing.Skip(currentSkip).Take(100);

                    if (!batch.Any())
                        exhausted = true;
                    else
                        await register.RemoveDevices2Async(batch);
                    currentSkip += 100;
                }
            }
        }

        private static string[] CreateDeviceIds(int numDevices)
        {
            var hashtable = new HashSet<string>();
            for(var i = 0; i < numDevices; i++)
            {
                var retry = 0;
                var id = DeviceIdGenerator.GetRandomName(retry);
                while (hashtable.Contains(id))
                {
                    retry++;
                    id = DeviceIdGenerator.GetRandomName(retry);
                }
                hashtable.Add(id);
            }
            return hashtable.ToArray();
        }
    }
}
