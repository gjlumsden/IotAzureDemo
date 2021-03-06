﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using Newtonsoft.Json;
using System.Text;
using Newtonsoft.Json.Linq;

namespace DeviceSimulator
{
    class Program
    {
        private const string DeviceConnectionStringFormat = "HostName={0}.azure-devices.net;DeviceId={1};SharedAccessKey={2}";

        private static IList<SimulatedDevice> devices;
        private static IConfiguration Configuration { get; set; }

        static async Task Main(string[] args)
        {
            Console.WriteLine(System.Runtime.InteropServices.RuntimeInformation.OSDescription);
            Console.CancelKeyPress += HandleConsoleCancelEventHandler;
            bool ok = false;
            int numDevices = 0;
            bool removeExisting = true;
            while (!ok)
            {
                Console.WriteLine("How many devices do you want to simulate?");
                var response = Console.ReadLine();
                ok = int.TryParse(response, out numDevices);
                if (!ok)
                {
                    Console.WriteLine("That's not a whole number. Give it another go.");
                    continue;
                }
                Console.WriteLine("Remove existing devices? Y/n");
                response = Console.ReadLine().ToLower();
                ok = false;
                if (string.IsNullOrWhiteSpace(response) || response == "y")
                    ok = true;
                else if (response == "n")
                {
                    removeExisting = false;
                    ok = true;
                }
                else
                    ok = false;
                    
            }
            await RunAsync(numDevices, removeExisting);
        }

        private static async Task RunAsync(int numDevices, bool removeExisting)
        {
            await ConfigureAsync(numDevices, removeExisting);
            await Task.WhenAll(devices.Select(x => x.StartAsync()));
        }

        private static async Task ConfigureAsync(int numDevices, bool removeExisting)
        {
            var builder = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json");

            Configuration = builder.Build();
            var url = Configuration.GetValue<string>("RegistrationUrl");
            using (var client = new HttpClient() { Timeout = TimeSpan.FromMinutes(2) })
            {
                var response = await client.PostAsync(url,
                    new StringContent(JsonConvert.SerializeObject(new
                    {
                        NumDevices = numDevices,
                        ClearBeforeCreate = removeExisting
                    }), Encoding.UTF8, "application/json"));
                response.EnsureSuccessStatusCode();

                var createdDevices = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());

                devices = ((JArray)createdDevices.Devices).Select(x => new SimulatedDevice()
                    {
                        ConnectionString = string.Format(DeviceConnectionStringFormat, createdDevices.IoTHubName, x["Id"], x["Key"]),
                        DisplayName = x["Id"].ToString(),
                        Enabled = true
                    }).ToList();

                Console.WriteLine($"Created {numDevices} devices: {string.Join(", ", devices.Select(x => x.DisplayName))}");
                await Task.Delay(5000);
            }
        }

        private static async void HandleConsoleCancelEventHandler(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            PauseAll();
            Console.Clear();
            Console.WriteLine("1\tModify Temperature Range");
            Console.WriteLine("2\tModify Humidity Range");
            Console.WriteLine("3\tModify Vibration Range");
            Console.WriteLine("X\tExit");
            var value = Console.ReadKey();
            if (value.Key == ConsoleKey.X)
            {
                Console.WriteLine();
                await StopAllAsync();
                return;
            }

            ModifySensorRanges(value.Key);
        }

        private static void ModifySensorRanges(ConsoleKey key)
        {
            Console.Clear();
            Console.WriteLine("Select a device");
            var enabled = devices.Where(x => x.Enabled).ToArray();
            for (int i = 0; i < enabled.Count(); i++)
            {
                Console.WriteLine($"{i + 1}\t{enabled[i].DisplayName}");
            }

            int.TryParse(Console.ReadKey().KeyChar.ToString(), out int index);
            var targetDevice = enabled[index - 1];

            Console.WriteLine();
            Console.WriteLine("Delta?");
            double.TryParse(Console.ReadLine(), out double delta);

            if (key == ConsoleKey.NumPad1 || key == ConsoleKey.D1)
            {
                targetDevice.PerformTemperatureChange(delta);
            }
            if (key == ConsoleKey.NumPad2 || key == ConsoleKey.D2)
            {
                targetDevice.PerformHumidityChange(delta);
            }
            if (key == ConsoleKey.NumPad3 || key == ConsoleKey.D3)
            {
                targetDevice.PerformVibrationChange(delta);
			}
			ResumeAll();
        }

        private static void ResumeAll()
        {
            foreach (var d in devices.Where(x=>x.Enabled))
            {
                d.Resume();
            }
        }

        private static void PauseAll()
        {
            foreach (var d in devices.Where(x=>x.Enabled))
            {
                d.Pause();
            }
        }

        private static async Task StopAllAsync()
        {
            await Task.WhenAll(devices.Select(x => x.StopAsync()));
        }
    }
}
