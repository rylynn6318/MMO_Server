using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ServerCore
{
    class Listener
    {
        Socket listenSocket;
        Action<Socket> onAcceptHander;

        public void Init(IPEndPoint endPoint, Action<Socket> AcceptHander)
        {
            listenSocket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            onAcceptHander += AcceptHander;
            
            listenSocket.Bind(endPoint);

            listenSocket.Listen(10);

            SocketAsyncEventArgs args = new SocketAsyncEventArgs();
            args.Completed += new EventHandler<SocketAsyncEventArgs>(OnAcceptCompleted);
            RegisterAccept(args);
        }

        void RegisterAccept(SocketAsyncEventArgs args)
        {
            //기존 내용 제거
            args.AcceptSocket = null;

            bool pending = listenSocket.AcceptAsync(args);
            if (pending == false)
                OnAcceptCompleted(null, args);
        }
        void OnAcceptCompleted(Object sender, SocketAsyncEventArgs args)
        {
            if (args.SocketError == SocketError.Success)
            {
                onAcceptHander.Invoke(args.AcceptSocket);
            }
            else
            {
                Console.WriteLine(args.SocketError.ToString());
            }

            //다음을 위해 등록
            RegisterAccept(args);
        }
        public Socket Accept()
        {
            
            return listenSocket.Accept();
        }
    }
}
