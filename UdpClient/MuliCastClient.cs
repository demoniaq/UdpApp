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
        static readonly Mutex mutex1 = new Mutex();
        static readonly Mutex mutex2 = new Mutex();

        private readonly Socket socket;
        private readonly int delayMilliSeconds;

        private readonly Queue<double> queue;

        const int currentBufferSize = 1000000;       
        private List<double> currentBuffer;             
        private List<double> totalBuffer;              
        
        /// <summary>
        /// Среднее значение всех полученных данных
        /// </summary>
        public double Average
        {
            get
            {
                double retVal = 0;

                mutex2.WaitOne();
                if (totalBuffer.Count > 0)
                {
                    retVal = totalBuffer.Average() / currentBufferSize;
                }
                else if (currentBuffer.Count > 0)
                {
                    retVal = currentBuffer.Average();
                }
                mutex2.ReleaseMutex();

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
                Thread.Sleep(delayMilliSeconds);
                socket.Receive(buffer);
                mutex1.WaitOne();
                queue.Enqueue(BitConverter.ToDouble(buffer));
                mutex1.ReleaseMutex();
            }
        }

        public void CalcData()
        {
            currentBuffer = new List<double>();
            totalBuffer = new List<double>();
            
            while (true)
            {
                mutex1.WaitOne();
                if (queue.TryDequeue(out double val))
                {
                    mutex2.WaitOne();
                    currentBuffer.Add(val);
                    if (currentBuffer.Count == currentBufferSize)
                    {
                        // Вычисление стандартного отклонения по буфферу
                        double avg = currentBuffer.Average();
                        List<double> bufMinusAvg = currentBuffer.Select(x => x - avg).ToList();

                        totalBuffer.Add(currentBuffer.Sum());
                        currentBuffer.Clear();
                    }
                    mutex2.ReleaseMutex();
                }
                mutex1.ReleaseMutex();
            }            
        }
    }
}
