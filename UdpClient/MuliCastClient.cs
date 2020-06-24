using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace UdpClient
{
    internal class MuliCastClient
    {
        static readonly Mutex queueMutex = new Mutex();
        static readonly Mutex totalDataMutex = new Mutex();

        private readonly Socket socket;
        private readonly int delayMilliSeconds;

        private long firstRcvdPacketNumber;
        private long currentPacketNumber;

        /// <summary>
        /// Очередь для получения/обработки данных
        /// </summary>
        private readonly Queue<int> queue;

        /// <summary>
        /// Структура для накопления полученных данных
        /// </summary>
        public struct TolalData
        {
            public long Count;             // Количество полученных значений
            public long Sum;               // Сумма полученных значений
            public double Avg;               // Среднее значение
            public double SquaredDeviation;  // Квадрат отклонения от среднего значения
        }
        public TolalData totalData;

        /// <summary>
        /// Среднее значение всех полученных данных
        /// </summary>
        public double Average
        {
            get
            {
                totalDataMutex.WaitOne();
                double retVal = totalData.Avg;
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

        public long LostPackets
        {
            get
            {
                long retVal;

                totalDataMutex.WaitOne();
                retVal = currentPacketNumber - firstRcvdPacketNumber - totalData.Count;
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
            queue = new Queue<int>();

            firstRcvdPacketNumber = 0;
        }

        /// <summary>
        /// Прослушивание мультикаст группы, помещение полученных данных в очередь
        /// </summary>
        public void StartListen()
        {            
            byte[] buffer = new byte[12];

            while (true)
            {
                try
                {
                    socket.Receive(buffer);

                    totalDataMutex.WaitOne();
                    currentPacketNumber = BitConverter.ToInt64(buffer[0..8]);
                    if (firstRcvdPacketNumber == 0)
                    {
                        firstRcvdPacketNumber = currentPacketNumber;
                    }
                    totalDataMutex.ReleaseMutex();

                    queueMutex.WaitOne();
                    queue.Enqueue(BitConverter.ToInt32(buffer[8..12]));
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
            totalData = new TolalData();

            while (true)
            {
                queueMutex.WaitOne();
                doCalc = queue.TryDequeue(out int val);
                queueMutex.ReleaseMutex();

                if (doCalc)
                {
                    totalDataMutex.WaitOne();

                    totalData.Sum += val;
                    totalData.Count++;
                    totalData.Avg = (double)totalData.Sum / (double)totalData.Count;
                    totalData.SquaredDeviation += Math.Pow(val - totalData.Avg, 2); // Квадрат отклонения от среднего значения

                    totalDataMutex.ReleaseMutex();
                }
            }
        }
    }
}
