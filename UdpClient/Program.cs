using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Net;
using System.Threading;

namespace UdpClient
{
    internal class Program
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Удалите неиспользуемый параметр", Justification = "<Ожидание>")]
        private static void Main(string[] args)
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
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine($"Некорректное значение задержки в настройках = '{delayMilliSecondsStr}'");
                Console.ReadKey();
                return;
            }

            MultiCastClient multiCastClient = new MultiCastClient(multiCastAddress, port, delayMilliSeconds);
            Thread listenThread = new Thread(new ThreadStart(multiCastClient.StartListen));
            Thread calcThread = new Thread(new ThreadStart(multiCastClient.ProcessingData));
            listenThread.Start();
            calcThread.Start();


            Console.WriteLine("Нажмите Enter для отображения расчетных значений.");
            while (true)
            {
                if (Console.ReadKey(true).Key == ConsoleKey.Enter)
                {
                    multiCastClient.CalcStats();
                    Console.WriteLine($"Среднее = {multiCastClient.Average:f3}, СтандОтклонение = {multiCastClient.StandardDeviation:f3}, Мода = {multiCastClient.Moda}, Медиана = {multiCastClient.Mediana}, Потеряно пакетов = {multiCastClient.LostPackets}");
                    Thread.Sleep(1000); // Задержка, чтобы не спамить поток расчетом статистики
                }
            }
        }
    }
}
