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
            public long FirstRcvdPacketNumber;  // Номер первого полученного пакета
            public long CurrentPacketNumber;    // Номер текущего пакета
            public long Count;                  // Количество полученных значений
            public long Sum;                    // Сумма полученных значений
            public double Average;                  // Среднее значение
            public double SquaredDeviation;     // Квадрат отклонения от среднего значения
        }
        private TolalData totalData;

        /// <summary>
        /// Среднее значение всех полученных данных
        /// </summary>
        public double Average
        {
            get
            {
                totalDataMutex.WaitOne();
                double retVal = totalData.Average;
                totalDataMutex.ReleaseMutex();

                return retVal;
            }
        }

        /// <summary>
        /// Стандартное отклонение
        /// </summary>
        public double StandardDeviation
        {
            get
            {
                totalDataMutex.WaitOne();
                double retVal = totalData.Count > 1 ? Math.Sqrt(totalData.SquaredDeviation / (totalData.Count - 1)) : 0;
                totalDataMutex.ReleaseMutex();

                return retVal;
            }
        }

        /// <summary>
        /// Количество потерянных пакетов
        /// </summary>
        public long LostPackets
        {
            get
            {
                totalDataMutex.WaitOne();

                long retVal;
                if (totalData.FirstRcvdPacketNumber == 0)
                {
                    retVal = 0;
                }
                else
                {
                    retVal = totalData.CurrentPacketNumber - totalData.FirstRcvdPacketNumber - totalData.Count + 1;
                    retVal = retVal > 0 ? retVal : 0;
                }                

                totalDataMutex.ReleaseMutex();

                return retVal;
            }
        }


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

                    if ( (DateTime.Now - stamp).TotalSeconds > 1)
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
        public void CalcData()
        {
            bool doCalc;
            int rcvdVal;
            totalData = new TolalData();

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
