using System;

namespace DeviceSimulator
{
    public static class SimulatedDeviceExtensions
    {
        private static object lockObj = new object();
        public static void LogToConsole(this SimulatedDevice device, string message)
        {
            lock (lockObj)
            {
                if (device.status == SimulatedDevice.Running)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                }
                Console.WriteLine(message);
            }
        }
    }
}
