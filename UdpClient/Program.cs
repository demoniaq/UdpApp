﻿using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Net;

namespace UdpClient
{
    class Program
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Удалите неиспользуемый параметр", Justification = "<Ожидание>")]
        static void Main(string[] args)
        {
            IConfigurationRoot config;
            IPAddress multiCastAddress;
            int port;
            int delayMilliSeconds;

            try
            {
                config = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json").Build();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.ReadKey();
                return;
            }

            string multiCastGroup = config.GetSection("MultiCastGroup").Value;
            string portStr = config.GetSection("Port").Value;
            string delayMilliSecondsStr = config.GetSection("DelayMilliSeconds").Value;

            try
            {
                multiCastAddress = IPAddress.Parse(multiCastGroup);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine($"Некорректный IP адрес мультикаст группы в настройках = '{multiCastGroup}'");
                Console.ReadKey();
                return;
            }

            try
            {
                port = Convert.ToInt32(portStr);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine($"Некорректный адрес порта в настройках = '{portStr}'");
                Console.ReadKey();
                return;
            }

            try
            {
                delayMilliSeconds = Convert.ToInt32(delayMilliSecondsStr);
                if (delayMilliSeconds > 1000) throw new ArgumentOutOfRangeException();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine($"Некорректное значение задержки в настройках = '{delayMilliSecondsStr}' (ожидалось значение 0 - 1000)");
                Console.ReadKey();
                return;
            }



            Console.WriteLine("Клиент остановлен");
            Console.ReadKey();
        }
    }
}