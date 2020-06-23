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

        private readonly Queue<double> queue;

        public struct TolalData
        {
            public double Count;
            public double Sum;
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
                double retVal = totalData.Count != 0 ? totalData.Sum / totalData.Count : 0;
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
                double retVal = totalData.Count - 1 != 0 && totalData.Count != 0 ? Math.Sqrt(totalData.SquaredDeviation / (totalData.Count - 1)) : 0;
                totalDataMutex.ReleaseMutex();

                return retVal;
            }
        }

        public MuliCastClient(IPAddress multiCastAddress, int port, int delayMilliSeconds)
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint remoteEp = new IPEndPoint(IPAddress.Any, port);
            socket.Bind(remoteEp);
            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(multiCastAddress, IPAddress.Any));

            this.delayMilliSeconds = delayMilliSeconds;
            queue = new Queue<double>();
        }

        public void StartListen()
        {
            byte[] buffer = new byte[8];

            while (true)            
            {                
                socket.Receive(buffer);                    

                queueMutex.WaitOne();
                queue.Enqueue(BitConverter.ToDouble(buffer));
                queueMutex.ReleaseMutex();
            }
        }

        public void CalcData()
        {
            bool doCalc;

            totalData = new TolalData();

            while (true)
            {
                queueMutex.WaitOne();
                doCalc = queue.TryDequeue(out double val);
                queueMutex.ReleaseMutex();                

                if (doCalc)
                {
                    totalDataMutex.WaitOne();

                    totalData.Sum += val;
                    totalData.Count++;

                    double squaredDeviation = Math.Pow(val - (totalData.Sum / totalData.Count), 2); // Квадрат отклонения от среднего значения
                    totalData.SquaredDeviation += squaredDeviation;

                    totalDataMutex.ReleaseMutex();
                }                
            }            
        }
    }
}
