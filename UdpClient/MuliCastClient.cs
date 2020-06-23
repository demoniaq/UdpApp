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
        private readonly Queue<int> queue;

        const int currentBufferSize = 1000000;       // max buffer      4 294 967 298
        private List<int> currentBuffer;             // max             2 147 483 647
        private List<long> totalBuffer;              // max 9 223 372 036 854 775 807
        
        /// <summary>
        /// Среднее значение всех полученных данных
        /// </summary>
        public int Average
        {
            get
            {
                int retVal = 0;

                mutex2.WaitOne();
                if (totalBuffer.Count > 0)
                {
                    retVal = (int)(totalBuffer.Average() / currentBufferSize);
                }
                else if (currentBuffer.Count > 0)
                {
                    retVal = (int)currentBuffer.Average();
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
            queue = new Queue<int>();
        }

        public void StartListen()
        {
            byte[] buffer = new byte[10];

            while (true)            
            {
                socket.Receive(buffer);
                mutex1.WaitOne();
                queue.Enqueue(BitConverter.ToInt32(buffer));
                mutex1.ReleaseMutex();
            }
        }

        public void CalcData()
        {
            currentBuffer = new List<int>();
            totalBuffer = new List<long>();
            
            while (true)
            {
                mutex1.WaitOne();
                if (queue.TryDequeue(out int val))
                {
                    mutex2.WaitOne();
                    currentBuffer.Add(val);
                    if (currentBuffer.Count == currentBufferSize)
                    {
                        totalBuffer.Add(currentBuffer.Sum(x => (long)x));
                        currentBuffer.Clear();
                    }
                    mutex2.ReleaseMutex();
                }
                mutex1.ReleaseMutex();
            }            
        }
    }
}
