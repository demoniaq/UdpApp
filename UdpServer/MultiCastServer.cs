﻿using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
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

        public MultiCastServer(int rangeStart, int rangeEnd, IPAddress multiCastAddress, int port)
        {
            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(multiCastAddress));
                socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 2);

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
            Random rnd = new Random();

            while(true)
            {
                byte[] buffer = BitConverter.GetBytes(rnd.Next(rangeStart, rangeEnd));
                try
                {
                    socket.Send(buffer, buffer.Length, SocketFlags.None);
                }
                catch (Exception)
                {
                }                
            }
        }

        /// <summary>
        /// Закрытие сокета мультикаст рассылки
        /// </summary>
        public void Stop()
        {
            socket.Close();
        }
    }
}