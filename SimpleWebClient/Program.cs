using System;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

namespace SimpleWebClient
{
    class Program
    {
        static int requestCounter;
        static async Task Main(string[] args)
        {
            var tasks = new List<Task>();
            var requests = new List<ConnectionRequest>();
            var requestToSend = "GET /this_is_my_get_request".ToByteArray();
            var address = "localhost";
            var port = 8888;
            var ipAddresses = Dns.GetHostAddresses(address);
            for (int i = 0; i < 10000; i++)
            {
                //var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                //Interlocked.Increment(ref requestCounter);
                //var request = new ConnectionRequest(i, client);
                //requests.Add(request);
                //client.BeginConnect(address, port, myConnection, request);
                //tasks.Add(Task.Run(async () =>
                //{
                //    using var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                //    await myFunction(client, ipAddresses, port, requestToSend);
                //}));
                tasks.Add(Task.Run(async () =>
                {
                    using var tcpClient = new TcpClient(address, port);
                    using var stream = tcpClient.GetStream();
                    await myTCPFunction(stream, requestToSend);
                }));
            }
            await Task.WhenAll(tasks);
            Console.WriteLine($"Successfully completed {requestCounter} requests");
            //var sw = new SpinWait();
            //while(requestCounter>0)
            //{
            //    sw.SpinOnce();
            //}
        }
        private static async Task myTCPFunction(NetworkStream stream, byte[] sendBuffer)
        {
            try
            {
                await stream.WriteAsync(sendBuffer);
                var receivedData = new byte[20000];
                await stream.ReadAsync(receivedData);
                //Console.WriteLine($"Received: {receivedData.ToOutputString()}");
                Interlocked.Increment(ref requestCounter);
            }
            catch (Exception)
            {

            }
            finally
            {
                stream.Close();
            }
        }
        private static async Task myFunction(Socket client, IPAddress[] ipAddresses, int port, byte[] sentData)
        {
            await client.ConnectAsync(ipAddresses, port);
            var sentBytes = await client.SendAsync(sentData, SocketFlags.None);
            var receivedData = new byte[20000];
            var receivedBytes = await client.ReceiveAsync(receivedData, SocketFlags.None);
            //Console.WriteLine($"Sent {sentBytes} bytes and received {receivedBytes} bytes");
            Interlocked.Increment(ref requestCounter);
            client.Close();
        }
        private static void myConnection(IAsyncResult result)
        {
            var request = (ConnectionRequest)result.AsyncState;
            try
            {
                request.Client.EndConnect(result);
                Console.WriteLine($"Connection {request.ConnectionNumber} connected");
                request.SentData = $"GET /this_is_my_get_request".ToByteArray();
                request.Client.BeginSend(request.SentData, 0, request.SentData.Length, SocketFlags.None, mySend, request);
            }
            catch (SocketException e)
            {
                request.SocketConnectionFailure = e;
            }
            finally
            {
                if (!(request.SocketConnectionFailure is null))
                    Interlocked.Decrement(ref requestCounter);
            }
        }
        private static void mySend(IAsyncResult result)
        {
            var request = (ConnectionRequest)result.AsyncState;
            try
            {
                request.BytesSent = request.Client.EndSend(result);
                Console.WriteLine($"Connection {request.ConnectionNumber} sent {request.BytesSent} bytes\n{request.SentData.ToOutputString()}");
                request.ReceivedData = new byte[20000];
                request.Client.BeginReceive(request.ReceivedData, 0, request.ReceivedData.Length, SocketFlags.None, myReceive, request);
            }
            catch (SocketException e)
            {
                request.DataSendFailure = e;
            }
            finally
            {
                if (!(request.DataSendFailure is null))
                    Interlocked.Decrement(ref requestCounter);
            }
        }

        private static void myReceive(IAsyncResult result)
        {
            var request = (ConnectionRequest)result.AsyncState;
            try
            {
                request.BytesReceived = request.Client.EndReceive(result);
                Console.WriteLine($"Connection {request.ConnectionNumber} received {request.BytesReceived} bytes\n{request.ReceivedData.ToOutputString()}");
            }
            catch (SocketException e)
            {
                request.DataReceiveFailure = e;
            }
            finally
            {
                Interlocked.Decrement(ref requestCounter);
            }
        }
    }
    public class ConnectionRequest
    {
        public int ConnectionNumber { get; }
        public Socket Client { get; }
        public byte[] SentData { get; set; }
        public int BytesSent { get; set; }
        public byte[] ReceivedData { get; set; }
        public int BytesReceived { get; set; }
        public SocketException SocketConnectionFailure { get; set; }
        public SocketException DataSendFailure { get; set; }
        public SocketException DataReceiveFailure { get; set; }
        public bool Success { get => SocketConnectionFailure is null && DataSendFailure is null && DataReceiveFailure is null; }
        public ConnectionRequest(int connectionNumber, Socket client)
        {
            ConnectionNumber = connectionNumber;
            Client = client;
        }
    }
    public static class MyExtensions
    {
        public static byte[] ToByteArray(this string str) =>
            str.ToCharArray().Select(c => (byte)c).ToArray();
        public static string ToOutputString(this byte[] arr) =>
            string.Join("", arr.Select(b => (char)b).ToArray());

    }
}
