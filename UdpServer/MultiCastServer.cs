﻿using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UdpServer
{
    internal class MultiCastServer
    {
        private readonly Socket socket;
        private readonly IPEndPoint remoteEp;
        private readonly int rangeStart;
        private readonly int rangeEnd;

        /// <summary>
        /// Конструктор
        /// </summary>
        /// <param name="rangeStart">Начало диапазона отправляемых чисел</param>
        /// <param name="rangeEnd">Конец диапазона отправляемых чисел</param>
        /// <param name="multiCastAddress"></param>
        /// <param name="port"></param>
        /// <param name="ttl"></param>
        public MultiCastServer(int rangeStart, int rangeEnd, IPAddress multiCastAddress, int port, byte ttl)
        {
            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(multiCastAddress));
                socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, ttl); 

                remoteEp = new IPEndPoint(multiCastAddress, port);
                socket.Connect(remoteEp);
            }
            catch (Exception)
            {
                throw;
            }

            this.rangeStart = rangeStart;
            this.rangeEnd = rangeEnd;
        }

        /// <summary>
        /// Запуск UDP мультикаст рассылки
        /// </summary>
        public void Start()
        {
            byte[] numberPacketBytes;
            byte[] rndValueBytes;
            Random rnd = new Random();
            long numberOfPacket = 1;

            while(true)            
            {
                numberPacketBytes = BitConverter.GetBytes(numberOfPacket);
                rndValueBytes = BitConverter.GetBytes(rnd.Next(rangeStart, rangeEnd));
                byte[] buffer = numberPacketBytes.Concat(rndValueBytes).ToArray();

                try
                {
                    socket.Send(buffer, buffer.Length, SocketFlags.None);
                    numberOfPacket++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        /// <summary>
        /// Закрытие сокета мультикаст рассылки
        /// </summary>
        public void Stop()
        {
            socket.Shutdown(SocketShutdown.Send);
            socket.Close();
        }
    }
}
