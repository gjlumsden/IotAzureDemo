using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using System.Threading;
using System.Text;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;

namespace DeviceSimulator
{
    public class SimulatedDevice
    {
        internal const string Running = "RUNNING";
        internal const string Shutdown = "SHUTDOWN";

        private readonly Random random = new Random();
        private DeviceClient client;
        private readonly CancellationTokenSource cts;
        private readonly ValueBounds tempMaxMin;
        private readonly ValueBounds humidMaxMin;
        private readonly ValueBounds vibrationMaxMin;
        internal string status = Shutdown;
        private bool paused;

        public string DisplayName { get; set; }
        public string ConnectionString { get; set; }
        public bool Enabled { get; set; }

        public SimulatedDevice()
        {
            cts = new CancellationTokenSource();
            tempMaxMin = new ValueBounds { Minimum = random.Next(20, 25) };
            tempMaxMin.Maximum = tempMaxMin.Minimum + 1;
            humidMaxMin = new ValueBounds { Minimum = random.Next(45, 50) };
            humidMaxMin.Maximum = humidMaxMin.Minimum + 0.5;
            vibrationMaxMin = new ValueBounds { Minimum = random.NextDouble(0.01D, 0.06D) };
            vibrationMaxMin.Maximum = vibrationMaxMin.Minimum + 0.05;
        }

        internal async Task StartAsync()
        {
            if (!Enabled) return;
            await ConfigureClient();
            await StartEquipment();
            await StartSendLoop(cts.Token);

        }

        internal async Task StopAsync()
        {
            if (!Enabled) return;
            cts.Cancel();
            await client.CloseAsync();
            client.Dispose();
        }

        private async Task StartSendLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (paused)
                {
                    try
                    {
                        await Task.Delay(50, token);
                    }
                    catch (TaskCanceledException)
                    {
                        return;
                    }
                    continue;
                }
                var message = new
                {
                    Temperature = Math.Round(random.NextDouble(tempMaxMin.Minimum, tempMaxMin.Maximum), 4),
                    Humidity = Math.Round(random.NextDouble(humidMaxMin.Minimum, humidMaxMin.Maximum), 4),
                    Vibration = Math.Round(random.NextDouble(vibrationMaxMin.Minimum, vibrationMaxMin.Maximum), 4),
                    Name = DisplayName,
                    Time = DateTime.UtcNow,
                    EquipmentStatus = status
                };
                using (var deviceEvent = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message))))
                {
                    await client.SendEventAsync(deviceEvent);
                    this.LogToConsole($"{DisplayName.PadRight(20, ' ')}Sent Message: T:{message.Temperature.ToString(CultureInfo.InvariantCulture).PadRight(8,' ')} H:{message.Humidity.ToString(CultureInfo.InvariantCulture).PadRight(8,' ')} V:{message.Vibration.ToString(CultureInfo.InvariantCulture).PadRight(8, ' ')} TS: {message.Time.ToString().PadRight(20, ' ')} Status: {status.PadRight(10, ' ')}");
                }
                try
                {
                    await Task.Delay(1000, token);
                    if (status == Running)
                        PerformTemperatureChange(random.NextDouble(0.25, 0.6));
                    else
                        PerformTemperatureChange(-1 * random.NextDouble(0.25, 0.6));
                }
                catch(TaskCanceledException)
                { return;}
            }
        }

        private async Task ConfigureClient()
        {
            client = DeviceClient.CreateFromConnectionString(ConnectionString, TransportType.Mqtt);
            await client.OpenAsync();
            await client.SetMethodHandlerAsync("shutdown", HandleShutdown, null);
            await client.SetMethodHandlerAsync("start", HandleStart, null);
            this.LogToConsole($"{DisplayName}: Connected");
        }

        internal void Resume()
        {
            paused = false;
        }

        internal void Pause()
        {
            paused = true;
        }

        private async Task<MethodResponse> HandleShutdown(MethodRequest methodRequest, object userContext)
        {
            try
            {
                await ShutdownEquipment();
                return await Task.FromResult(new MethodResponse(new byte[0], 200));
            }
            catch (InvalidOperationException)
            {
                return await Task.FromResult(new MethodResponse(new byte[0], 400));
            }
            catch
            {
                return await Task.FromResult(new MethodResponse(new byte[0], 500));
            }
        }

        private async Task<MethodResponse> HandleStart(MethodRequest methodRequest, object userContext)
        {
            try
            {
                await StartEquipment();
                return await Task.FromResult(new MethodResponse(new byte[0], 200));
            }
            catch (InvalidOperationException)
            {
                return await Task.FromResult(new MethodResponse(new byte[0], 400));
            }
            catch
            {
                return await Task.FromResult(new MethodResponse(new byte[0], 500));
            }
        }

        private async Task StartEquipment()
        {
            paused = false;
            if (status != Shutdown)
                throw new InvalidOperationException("Invalid state to start.");
            status = Running;
            await UpdateDeviceTwinProperties();
            this.LogToConsole($"{DisplayName}: {status}");
        }

        private async Task ShutdownEquipment()
        {
            paused = false;
            if (status != Running)
                throw new InvalidOperationException("Invalid state to shutdown.");
            status = Shutdown;
            await UpdateDeviceTwinProperties();
            this.LogToConsole($"{DisplayName}: {status}");
        }

        private async Task UpdateDeviceTwinProperties()
        {
            var patch = $"{{\"EquipmentStatus\": \"{status}\"}}";
            await client.UpdateReportedPropertiesAsync(JsonConvert.DeserializeObject<TwinCollection>(patch));
        }

        public void PerformTemperatureChange(double delta)
        {
            tempMaxMin.Minimum += delta;
            tempMaxMin.Maximum += delta;
        }

        public void PerformHumidityChange(double delta)
        {
            humidMaxMin.Minimum += delta;
            humidMaxMin.Maximum += delta;
        }

        public void PerformVibrationChange(double delta)
        {
            vibrationMaxMin.Minimum += delta;
            vibrationMaxMin.Maximum += delta;
        }
    }

}
