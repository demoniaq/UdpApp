using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace UdpClient
{
    internal class MultiCastClient
    {
        private static readonly Mutex queueMutex = new Mutex();
        private static readonly Mutex totalDataMutex = new Mutex();

        private readonly Socket socket;
        private readonly int delayMilliSeconds;

        /// <summary>
        /// Очередь для получения/обработки данных
        /// </summary>
        private readonly Queue<byte[]> queue;

        /// <summary>
        /// Структура для накопления полученных данных
        /// </summary>
        private struct TotalData
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
        private TotalData totalData;

        public void CalcStats()
        {
            totalDataMutex.WaitOne();

            average = totalData.Average;
            standardDeviation = totalData.Count > 1 ? Math.Sqrt(totalData.SquaredDeviation / (totalData.Count - 1)) : 0;
            moda = totalData.Moda;

            mediana = 0;
            if (totalData.Count > 0)
            {
                Dictionary<int, long> orderedDictValCount = totalData.dictValCount.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);

                double tmpSum = 0;
                foreach (KeyValuePair<int, long> keyValuePair in orderedDictValCount)
                {
                    tmpSum += keyValuePair.Value / (double)totalData.Count;
                    if (tmpSum > 0.5)
                    {
                        mediana = keyValuePair.Key;
                        break;
                    }
                }
            }

            if (totalData.FirstRcvdPacketNumber == 0)
            {
                lostPackets = 0;
            }
            else
            {
                lostPackets = totalData.CurrentPacketNumber - totalData.FirstRcvdPacketNumber - totalData.Count + 1;
                lostPackets = LostPackets > 0 ? LostPackets : 0;
            }

            totalDataMutex.ReleaseMutex();
        }

        private double average;
        /// <summary>
        /// Среднее значение всех полученных данных
        /// </summary>
        public double Average => average;

        private double standardDeviation;
        /// <summary>
        /// Стандартное отклонение
        /// </summary>
        public double StandardDeviation => standardDeviation;

        private int moda;
        /// <summary>
        /// Мода
        /// </summary>
        public int Moda => moda;

        private int mediana;
        /// <summary>
        /// Медиана
        /// </summary>
        public int Mediana => mediana;

        private long lostPackets;
        /// <summary>
        /// Количество потерянных пакетов
        /// </summary>
        public long LostPackets => lostPackets;


        /// <summary>
        /// Конструктор
        /// </summary>
        /// <param name="multiCastAddress"></param>
        /// <param name="port"></param>
        /// <param name="delayMilliSeconds"></param>
        public MultiCastClient(IPAddress multiCastAddress, int port, int delayMilliSeconds)
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
            totalData = new TotalData
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
                        totalData.Average = totalData.Sum / (double)totalData.Count;
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
