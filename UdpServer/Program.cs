﻿using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace UdpServer
{
    class Program
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Удалите неиспользуемый параметр", Justification = "<Ожидание>")]
        static void Main(string[] args)
        {
            IConfigurationRoot config;
            IPAddress multiCastAddress;
            MultiCastServer multiCastServer;
            int port;

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

            string rangeStartStr = config.GetSection("rangeStart").Value;
            string rangeEndStr = config.GetSection("RangeEnd").Value;
            string multiCastGroup = config.GetSection("MultiCastGroup").Value;
            string portStr = config.GetSection("Port").Value;

            int rangeStart = Convert.ToInt32(rangeStartStr);
            int rangeEnd = Convert.ToInt32(rangeEndStr);
            if (rangeStart >= rangeEnd)
            {
                Console.WriteLine($"Некорректный диапазон в настройках: {rangeStartStr} - {rangeEndStr}");
                Console.ReadKey();
                return;
            }            

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
                multiCastServer = new MultiCastServer(rangeStart, rangeEnd, multiCastAddress, port);
                _ = Task.Run(() => multiCastServer.Start());
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.ReadKey();
                return;
            }

            Console.WriteLine($"Диапазон чисел: {rangeStart} - {rangeEnd}");
            Console.WriteLine($"Мультикаст группа: {multiCastAddress}");
            Console.WriteLine("Запущена рассылка UDP multicast. Для остановки нажмите любую клавишу.");
            Console.ReadKey();

            multiCastServer.Stop();
        }
    }
}