using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UdpClient
{
    internal class MuliCastClient
    {
        static readonly Mutex queueMutex = new Mutex();
        static readonly Mutex totalDataMutex = new Mutex();

        private readonly Socket socket;
        private readonly int delayMilliSeconds;

        /// <summary>
        /// Очередь для получения/обработки данных
        /// </summary>
        private readonly Queue<byte[]> queue;

        /// <summary>
        /// Структура для накопления полученных данных
        /// </summary>
        private struct TolalData 
        {
            public long FirstRcvdPacketNumber;          // Номер первого полученного пакета
            public long CurrentPacketNumber;            // Номер текущего пакета
            public long Count;                          // Количество полученных значений
            public long Sum;                            // Сумма полученных значений
            public double Average;                      // Среднее значение
            public double SquaredDeviation;             // Квадрат отклонения от среднего значения
            public long MaxKeyCount;                    // Максимальная частота значения в полученной выборке
            public int Moda;                            // Мода
            public Dictionary<int, long> dictValCount;  // Словарь: значение - количество получений
        }
        private TolalData totalData; 

        public void CalcStats()
        {
            totalDataMutex.WaitOne();

            Average = totalData.Average;
            StandardDeviation = totalData.Count > 1 ? Math.Sqrt(totalData.SquaredDeviation / (totalData.Count - 1)) : 0;
            Moda = totalData.Moda;

            Mediana = 0;
            if (totalData.Count > 0)
            {
                Dictionary<int, double> dictFreq = totalData.dictValCount.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => (double)x.Value / (double)totalData.Count);
                //Dictionary<int, double> dictFreq = totalData.dictValCount.ToDictionary(x => x.Key, x => (double)x.Value / (double)totalData.Count);

            }

            if (totalData.FirstRcvdPacketNumber == 0)
            {
                LostPackets = 0;
            }
            else
            {
                LostPackets = totalData.CurrentPacketNumber - totalData.FirstRcvdPacketNumber - totalData.Count + 1;
                LostPackets = LostPackets > 0 ? LostPackets : 0;
            }

            totalDataMutex.ReleaseMutex();
        }

        /// <summary>
        /// Среднее значение всех полученных данных
        /// </summary>
        public double Average { get; set; }

        /// <summary>
        /// Стандартное отклонение
        /// </summary>
        public double StandardDeviation { get; set; }

        /// <summary>
        /// Мода
        /// </summary>
        public int Moda { get; set; }

        /// <summary>
        /// Медиана
        /// </summary>
        public double Mediana { get; set; }

        /// <summary>
        /// Количество потерянных пакетов
        /// </summary>
        public long LostPackets { get; set; }


        /// <summary>
        /// Конструктор
        /// </summary>
        /// <param name="multiCastAddress"></param>
        /// <param name="port"></param>
        /// <param name="delayMilliSeconds"></param>
        public MuliCastClient(IPAddress multiCastAddress, int port, int delayMilliSeconds)
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint remoteEp = new IPEndPoint(IPAddress.Any, port);
            socket.Bind(remoteEp);
            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(multiCastAddress, IPAddress.Any));

            this.delayMilliSeconds = delayMilliSeconds;
            queue = new Queue<byte[]>();
        }

        /// <summary>
        /// Прослушивание мультикаст группы, помещение полученных данных в очередь
        /// </summary>
        public void StartListen()
        {
            byte[] buffer = new byte[12];
            DateTime stamp = DateTime.Now;

            while (true)
            {
                try
                {
                    socket.Receive(buffer);

                    if ((DateTime.Now - stamp).TotalSeconds > 1)
                    {
                        Thread.Sleep(delayMilliSeconds);
                        stamp = DateTime.Now;
                    }

                    queueMutex.WaitOne();
                    queue.Enqueue(buffer);
                    queueMutex.ReleaseMutex();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        /// <summary>
        /// Обработка полученных из очереди данных
        /// </summary>
        public void ProcessingData()
        {
            bool doCalc;
            int rcvdVal;
            totalData = new TolalData
            {
                dictValCount = new Dictionary<int, long>()
            };

            while (true)
            {
                try
                {
                    queueMutex.WaitOne();
                    doCalc = queue.TryDequeue(out byte[] buffer);
                    queueMutex.ReleaseMutex();

                    if (doCalc)
                    {
                        totalDataMutex.WaitOne();

                        rcvdVal = BitConverter.ToInt32(buffer[8..12]);

                        totalData.CurrentPacketNumber = BitConverter.ToInt64(buffer[0..8]);
                        if (totalData.FirstRcvdPacketNumber == 0)
                        {
                            totalData.FirstRcvdPacketNumber = totalData.CurrentPacketNumber;
                        }

                        totalData.Sum += rcvdVal;
                        totalData.Count++;
                        totalData.Average = (double)totalData.Sum / (double)totalData.Count;
                        totalData.SquaredDeviation += Math.Pow(rcvdVal - totalData.Average, 2); // Квадрат отклонения от среднего значения                                               

                        if (totalData.dictValCount.ContainsKey(rcvdVal))
                        {
                            totalData.dictValCount[rcvdVal]++;
                        }
                        else
                        {
                            totalData.dictValCount.Add(rcvdVal, 1);
                        }

                        // Вычисление Моды на лету
                        if (totalData.MaxKeyCount < totalData.dictValCount[rcvdVal])
                        {
                            totalData.MaxKeyCount = totalData.dictValCount[rcvdVal];
                            totalData.Moda = rcvdVal;
                        }

                        totalDataMutex.ReleaseMutex();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }
    }
}
