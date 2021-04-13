﻿using ServerCore;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ServerCore
{
    public abstract class PacketSession : Session
    {
        public static readonly int HeaderSize = 2;

        public sealed override int OnRecv(ArraySegment<byte> buffer)
        {
            int processLen = 0;

            while(true)
            {
                //최소한 헤더 파싱 가능한지 체크
                if (buffer.Count < HeaderSize)
                    break;

                //패킷이 완전하게 도착했는지 확인
                ushort dataSize = BitConverter.ToUInt16(buffer.Array, buffer.Offset);
                if (buffer.Count < dataSize)
                    break;

                //여기 도달하면 패킷을 조립
                OnRecvPacket(new ArraySegment<byte>(buffer.Array, buffer.Offset, dataSize));

                processLen += dataSize;
                buffer = new ArraySegment<byte>(buffer.Array, buffer.Offset + dataSize, buffer.Count - dataSize);
            }
            return processLen;
        }

        public abstract void OnRecvPacket(ArraySegment<byte> buffer);
    }



    //TODO : 
    // 1.registerSend()를 할 때나 receive할 때 내가 어느정도 보냈는지를 추적해서 너무 양이 많으면 쉬어주는 처리 필요
    // DDos같이 무의미한 데이터가 계속 들어오면 receive할 때 체크해서 disconnect 해줘야함
    public abstract class Session
    {
        Socket _socket;
        int _disconnected = 0;

        RecvBuffer _recvBuffer = new RecvBuffer(1024);

        object _lock = new object();
        Queue<ArraySegment<byte>> _sendQueue = new Queue<ArraySegment<byte>>();
        List<ArraySegment<byte>> _pendingList = new List<ArraySegment<byte>>();
        SocketAsyncEventArgs _sendArgs = new SocketAsyncEventArgs();
        SocketAsyncEventArgs _recvArgs = new SocketAsyncEventArgs();

        public abstract void OnConnected(EndPoint endPoint);
        public abstract int OnRecv(ArraySegment<byte> buffer);
        public abstract void OnSend(int numOfBytes);
        public abstract void OnDisconnected(EndPoint endPoint);

        public void Start(Socket InSocket)
        {
            _socket = InSocket;
            _recvArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnRecvCompleted);
      

            _sendArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnSendCompleted);

            RegisterRecv();
        }

        //여기서 작은 버퍼말고 많은 데이터를 한번에 보내야하나? 라는 점을 고려해보자
        //ex) 엔진에서는 패킷모아보내기하지 않고 컨텐츠에서 같은 행위를 기록해서 한번에 보내기 등
        public void Send(ArraySegment<byte> sendBuff)
        {
            lock (_lock)
            {
                _sendQueue.Enqueue(sendBuff);
                if (_pendingList.Count == 0)
                    RegisterSend();
            }
        }

        public void Disconnect()
        {
            if (Interlocked.Exchange(ref _disconnected, 1) == 1)
                return;

            OnDisconnected(_socket.RemoteEndPoint);
            _socket.Shutdown(SocketShutdown.Both);
            _socket.Close();
        }

        #region 네트워크 통신
        void RegisterSend()
        {

            while (_sendQueue.Count > 0)
            {
                ArraySegment<byte> buff = _sendQueue.Dequeue();
                _pendingList.Add(buff);
            }

            _sendArgs.BufferList = _pendingList;

            bool pending = _socket.SendAsync(_sendArgs);
            if (pending == false)
                OnSendCompleted(null, _sendArgs);
        }

        void OnSendCompleted(Object sender, SocketAsyncEventArgs args)
        {
            lock (_lock)
            {
                if (args.BytesTransferred > 0 && args.SocketError == SocketError.Success)
                {
                    try
                    {
                        _sendArgs.BufferList = null;
                        _pendingList.Clear();

                        OnSend(_sendArgs.BytesTransferred);

                        //누군가가 내가 하는동안 예약을 한경우 큐에 남아있으면 한번더 registerSend() 호출
                        if (_sendQueue.Count > 0)
                            RegisterSend();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"OnSendCompleted Failed {e}");
                    }
                }
                else
                {
                    Disconnect();
                }
            }
        }

        void RegisterRecv()
        {
            _recvBuffer.Clean();
            ArraySegment<byte> segment = _recvBuffer.WriteSegment;
            _recvArgs.SetBuffer(segment.Array, segment.Offset, segment.Count);

            bool pending = _socket.ReceiveAsync(_recvArgs);
            if (pending == false)
                OnRecvCompleted(null, _recvArgs);
        }

        void OnRecvCompleted(Object sender, SocketAsyncEventArgs args)
        {
            if (args.BytesTransferred > 0 && args.SocketError == SocketError.Success)
            {
                try
                {
                    //Write 커서 이동
                    if(_recvBuffer.OnWrite(args.BytesTransferred)==false)
                    {
                        Disconnect();
                        return;
                    }
                    
                    //컨텐츠 쪽으로 데이터 넘겨줌, 얼마나 처리했는지 받음
                    int processLength = OnRecv(_recvBuffer.ReadSegment);
                    if(processLength<0 || _recvBuffer.DataSize < processLength)
                    {
                        Disconnect();
                        return;
                    }

                    //Read 커서 이동
                    if(_recvBuffer.OnRead(processLength)==false)
                    {
                        Disconnect();
                        return;
                    }

                    RegisterRecv();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"OnRecvCompleted Failed {e}");
                }
            }
            else
            {
                Disconnect();
            }
        }
        #endregion
    }
}
