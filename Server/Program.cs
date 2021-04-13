using ServerCore;
using System;
using System.Collections.Generic;
using System.Net;

namespace Server
{
    class Program
    {
        static Listener listener = new Listener();

        static void Main(string[] args)
        {
            //DNS
            string host = Dns.GetHostName();
            IPHostEntry ipHost = Dns.GetHostEntry(host);
            IPAddress ipAddr = ipHost.AddressList[0];
            IPEndPoint endPoint = new IPEndPoint(ipAddr, 7777);

            listener.Init(endPoint, () => { return new ClientSession(); });
            while (true)
            {

            }
        }
    }
}
