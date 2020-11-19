using Nancy;
using Nancy.Json;
using System;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using ZabbixSender;
using ZabbixSender.Async;

namespace zabbixEventLogger
{
    class Program
    {
        static void Main(string[] args)
        {
            string zabbixIP = "#zabbixIP";
            int zabbixPORT = 00000;

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            string json = serializer.Serialize(
                new
                {
                    request = "sender data",
                    data = new[]
                    {
                        new
                        {
                            host = "TestHost",
                            key = "TestItem",
                            value = DateTime.Now.ToString(),
                            timestamp = "1589987440",
                            source = "Source",
                            severity = "0",
                            eventid = "42"
                        }
                    }
                    , ns = 625917767
                }) ;
            byte[] header = Encoding.ASCII.GetBytes("ZBXD\x01");
            byte[] length = BitConverter.GetBytes((long)json.Length);
            byte[] data = Encoding.ASCII.GetBytes(json);

            byte[] all = new byte[header.Length + length.Length + data.Length];

            System.Buffer.BlockCopy(header, 0, all, 0, header.Length);
            System.Buffer.BlockCopy(length, 0, all, header.Length, length.Length);
            System.Buffer.BlockCopy(data, 0, all, header.Length + length.Length, data.Length);

            using(var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                client.Connect(zabbixIP, zabbixPORT);
                client.Send(all);

                byte[] buffer = new byte[5];
                _Receive(client, buffer, 0, buffer.Length, 10000);

                if ("ZBXD\x01" != Encoding.ASCII.GetString(buffer, 0, buffer.Length))
                    throw new Exception("Invalid response");

                buffer = new byte[8];
                _Receive(client, buffer, 0, buffer.Length, 10000);
                int dataLength = BitConverter.ToInt32(buffer, 0);

                if (dataLength == 0)
                    throw new Exception("Invvalid response");

                buffer = new byte[dataLength];
                _Receive(client, buffer, 0, buffer.Length, 10000);

                Console.WriteLine(Encoding.ASCII.GetString(buffer, 0, buffer.Length));
            }
        }

        private static void _Receive(Socket socket, byte[] buffer, int offset, int size, int timeout)
        {
            int startTickCount = Environment.TickCount;
            int received = 0;
            do
            {
                if (Environment.TickCount > startTickCount + timeout)
                    throw new Exception("Timeout");
                try
                {
                    received += socket.Receive(buffer, offset + received, size - received, SocketFlags.None);
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode == SocketError.WouldBlock || ex.SocketErrorCode == SocketError.IOPending || ex.SocketErrorCode == SocketError.NoBufferSpaceAvailable)
                    {
                        Thread.Sleep(30);
                    }
                    else
                    {
                        throw ex;
                    }
                }
            }
            while (received < size);
        }
    }
}
