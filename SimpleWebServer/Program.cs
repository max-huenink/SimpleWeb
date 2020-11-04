using System;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Runtime.CompilerServices;

namespace SimpleWebServer
{
    class Program
    {
        private static readonly string webRoot = Path.Combine(Directory.GetCurrentDirectory(), "www");
        //static int requestCounter;
        static async Task Main(string[] args)
        {
            int port = 8888;
            var ipAddress = new IPAddress(new byte[] { 0, 0, 0, 0 });
            var endPoint = new IPEndPoint(ipAddress, port);

            using var listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(endPoint);
            listener.Listen(10);

            int connections = 0;
            var tasks = new List<Task>();
            /*
            //var requests = new List<SocketRequest>();
            //var sw = new SpinWait();
            while (true)
            {
                //while (requests.Count() < 10)
                while (requestCounter < 10)
                {
                    //var request = new SocketRequest(connections++, listener);
                    var request2 = new SocketRequest2(connections++, listener);
                    Interlocked.Increment(ref requestCounter);
                    //requests.Add(request);
                    //listener.BeginAccept(myAccept, request);
                    Task.Run(async () => { request2.Server = await listener.AcceptAsync(); await myAccept2(request2); });
                }
                //requests.RemoveAll(r => r.Complete);
                //SpinWait.SpinUntil(() => requestCounter < 10);
                //await Task.WhenAny(tasks);
                //tasks.RemoveAll(t => t.IsCompleted);
            }
            */
            Console.WriteLine("Press Control + D to gracefully exit when there are existing connection requests");
            Console.WriteLine("Press Control + C to forcefully exit");
            Console.WriteLine($"Listening at {ipAddress}:{port}");
            ConsoleKeyInfo keyInfo = new ConsoleKeyInfo(' ', ConsoleKey.F24, false, false, false);
            // Control+D to exit
            do
            {
                //tasks.RemoveAll(t => t.IsCompleted);
                var server = await listener.AcceptAsync();
                //tasks.Add(Task.Run(async () => await myFunction(server, connections++)));
                //await Task.Run(async () => await myFunction(server, connections++)).ConfigureAwait(false);
                //_ = Task.Run(async () => await myFunction(server, connections++).ConfigureAwait(false)).ConfigureAwait(false);
                tasks.Add(Task.Run(async () => await ConnectAndHandle(server, connections++)));
                if (Console.KeyAvailable)
                {
                    keyInfo = Console.ReadKey(true);
                }
            }
            while (keyInfo.Key == ConsoleKey.F24 || !(keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control) && keyInfo.Key.ToString().ToLower() == "d"));
            await Task.WhenAll(tasks);
            Console.WriteLine($"Executed {connections} connections.");
        }

        private static async Task ConnectAndHandle(Socket server, int connectionNumber)
        {
            var receivedData = new byte[20000];
            int bytesReceived = await server.ReceiveAsync(receivedData, SocketFlags.None);

            var stringData = receivedData[..bytesReceived].ToOutputString();
            var data = stringData.Split(" ");
            Console.WriteLine($"Connection {connectionNumber} Received: {stringData}");

            byte[] sendBuffer;

            string myOkMessage = $"HTTP/1.1 200 OK\r\nContent-Type: text/html\r\nContent-length: ";
            var myFilePath = Path.Combine(webRoot, data[1][1..]);

            if (data[0] == "GET" && data[1].Equals("/"))
            {
                var fileList = $"<html>" +
                    $"<head>" +
                    $"<title>index</title>" +
                    $"</head>" +
                    $"<body>" +
                    $"{string.Join("", Directory.GetFiles(webRoot).Select(f => $@"<p><a href=""{Path.GetFileName(f)}"">{Path.GetFileName(f)}</a></p>"))}" +
                    $"</body>";
                myOkMessage += $"{fileList.Length}\r\n\n{fileList}";
                sendBuffer = myOkMessage.ToByteArray();
            }
            else if (data[0] == "GET" && File.Exists(myFilePath))
            {
                var file = File.ReadAllText(myFilePath);
                myOkMessage += $"{file.Length}\r\n\n{file}";
                sendBuffer = myOkMessage.ToByteArray();
            }
            else
            {
                var file = File.ReadAllText(Path.Combine(webRoot, "404.html"));
                sendBuffer = $"HTTP/1.1 404 Not Found\r\nContent-Type: text/html\r\nContent-length: {file.Length}\r\n\n{file}".ToByteArray();
            }

            int bytesSent = await server.SendAsync(sendBuffer, SocketFlags.None);

            string message = $"Connection: {connectionNumber} ";
            if (bytesSent == sendBuffer.Length)
            {
                message += $"Successfully sent {bytesSent} bytes of data";
            }
            else
            {
                message += $"Failed to send data";
            }
            Console.WriteLine(message);
            server.Close();
        }

        /*
        private static void myAccept(IAsyncResult result)
        {
            var request = (SocketRequest)result.AsyncState;
            try
            {
                request.Server = request.Listener.EndAccept(result);
                request.ReceivedData = new byte[20000];
                request.Server.BeginReceive(request.ReceivedData, 0, request.ReceivedData.Length, SocketFlags.None, myReceive, request);
            }
            catch (SocketException e)
            {
                request.SocketCreationFailure = e;
            }
            finally
            {
                request.SocketCreationComplete = true;

                if (!(request.SocketCreationFailure is null))
                    Interlocked.Decrement(ref requestCounter);
            }
        }
        private static async Task myAccept2(SocketRequest2 request)
        {
            try
            {
                request.ReceivedData = new byte[20000];
                request.BytesReceived = await request.Server.ReceiveAsync(request.ReceivedData, SocketFlags.None);
                await myReceive2(request);
            }
            catch (SocketException e)
            {
                request.SocketCreationFailure = e;
            }
            finally
            {
                request.SocketCreationComplete = true;

                if (!(request.SocketCreationFailure is null))
                    Interlocked.Decrement(ref requestCounter);
            }
        }

        private static void myReceive(IAsyncResult result)
        {
            var request = (SocketRequest)result.AsyncState;
            try
            {
                request.BytesReceived = request.Server.EndReceive(result);

                var data = request.ReceivedData.ToOutputString().Split(" ");
                Console.WriteLine($"Connection {request.ConnectionNumber}\nReceived:\n{request.ReceivedData.ToOutputString()}");

                byte[] sendBuffer;

                string myOkMessage = $"HTTP/1.1 200 OK\r\nContent-Type: text/html\r\nContent-length: ";
                var myFilePath = Path.Combine(webRoot, data[1][1..]);

                if (data[0] == "GET" && data[1].Equals("/"))
                {
                    var fileList = $"<html><head><title>index</title></head><body>{string.Join("", Directory.GetFiles(webRoot).Select(f => $@"<p><a href=""{Path.GetFileName(f)}"">{Path.GetFileName(f)}</a></p>"))}</body>";
                    myOkMessage += $"{fileList.Length}\r\n\n{fileList}";
                    sendBuffer = myOkMessage.ToByteArray();
                }
                else if (data[0] == "GET" && File.Exists(myFilePath))
                {
                    var file = File.ReadAllText(myFilePath);
                    myOkMessage += $"{file.Length}\r\n\n{file}";
                    sendBuffer = myOkMessage.ToByteArray();
                }
                else
                {
                    var file = File.ReadAllText(Path.Combine(webRoot, "404.html"));
                    sendBuffer = $"HTTP/1.1 404 Not Found\r\nContent-Type: text/html\r\nContent-length: {file.Length}\r\n\n{file}".ToByteArray();
                }

                request.SentData = sendBuffer;
                request.Server.BeginSend(sendBuffer, 0, sendBuffer.Length, SocketFlags.None, mySend, request);
            }
            catch (SocketException e)
            {
                request.DataReceiveFailure = e;
            }
            finally
            {
                request.DataReceiveComplete = true;

                if (!(request.DataReceiveFailure is null))
                    Interlocked.Decrement(ref requestCounter);
            }
        }

        private static async Task myReceive2(SocketRequest2 request)
        {
            try
            {
                var stringData = request.ReceivedData.ToOutputString();
                var data = stringData.Split(" ");
                Console.WriteLine($"Connection {request.ConnectionNumber}\nReceived:\n{stringData}");

                byte[] sendBuffer;

                string myOkMessage = $"HTTP/1.1 200 OK\r\nContent-Type: text/html\r\nContent-length: ";
                var myFilePath = Path.Combine(webRoot, data[1][1..]);

                if (data[0] == "GET" && data[1].Equals("/"))
                {
                    var fileList = $"<html><head><title>index</title></head><body>{string.Join("", Directory.GetFiles(webRoot).Select(f => $@"<p><a href=""{Path.GetFileName(f)}"">{Path.GetFileName(f)}</a></p>"))}</body>";
                    myOkMessage += $"{fileList.Length}\r\n\n{fileList}";
                    sendBuffer = myOkMessage.ToByteArray();
                }
                else if (data[0] == "GET" && File.Exists(myFilePath))
                {
                    var file = File.ReadAllText(myFilePath);
                    myOkMessage += $"{file.Length}\r\n\n{file}";
                    sendBuffer = myOkMessage.ToByteArray();
                }
                else
                {
                    var file = File.ReadAllText(Path.Combine(webRoot, "404.html"));
                    sendBuffer = $"HTTP/1.1 404 Not Found\r\nContent-Type: text/html\r\nContent-length: {file.Length}\r\n\n{file}".ToByteArray();
                }

                request.SentData = sendBuffer;
                request.BytesSent = await request.Server.SendAsync(sendBuffer, SocketFlags.None);
                await mySend2(request);
            }
            catch (SocketException e)
            {
                request.DataReceiveFailure = e;
            }
            finally
            {
                request.DataReceiveComplete = true;

                if (!(request.DataReceiveFailure is null))
                    Interlocked.Decrement(ref requestCounter);
            }
        }

        private static void mySend(IAsyncResult result)
        {
            var request = (SocketRequest)result.AsyncState;
            try
            {
                request.BytesSent = request.Server.EndSend(result);
                string message = $"Connection: {request.ConnectionNumber}\n";
                if (request.BytesSent == request.SentData.Length)
                {
                    message += $"Successfully sent {request.BytesSent} bytes of data";
                }
                else
                {
                    message += $"Failed to send data";
                }
                Console.WriteLine(message);
            }
            catch (SocketException e)
            {
                request.DataSendFailure = e;
            }
            finally
            {
                request.DataSendComplete = true;
                Interlocked.Decrement(ref requestCounter);
            }
        }

        private static async Task mySend2(SocketRequest2 request)
        {
            try
            {
                string message = $"Connection: {request.ConnectionNumber}\n";
                if (request.BytesSent == request.SentData.Length)
                {
                    message += $"Successfully sent {request.BytesSent} bytes of data";
                }
                else
                {
                    message += $"Failed to send data";
                }
                Console.WriteLine(message);
            }
            catch (SocketException e)
            {
                request.DataSendFailure = e;
            }
            finally
            {
                request.DataSendComplete = true;
                Interlocked.Decrement(ref requestCounter);
            }

        }
        */
    }
    /*
    public class SocketRequest
    {
        public int ConnectionNumber { get; }

        public Socket Listener { get; }
        public Socket Server { get; set; }

        public byte[] ReceivedData { get; set; }
        public int BytesReceived { get; set; }
        public byte[] SentData { get; set; }
        public int BytesSent { get; set; }

        public SocketException SocketCreationFailure { get; set; }
        public SocketException DataReceiveFailure { get; set; }
        public SocketException DataSendFailure { get; set; }

        public bool SocketCreationComplete { get; set; }
        public bool DataReceiveComplete { get; set; }
        public bool DataSendComplete { get; set; }

        public bool Failure { get => !(SocketCreationFailure is null && DataReceiveFailure is null && DataSendFailure is null); }
        public bool Success { get => SocketCreationComplete && DataReceiveComplete && DataSendComplete; }

        public bool Complete { get => Success || Failure; }

        public SocketRequest(int connectionNumber, Socket listener)
        {
            ConnectionNumber = connectionNumber;
            Listener = listener;
        }
    }

    public class SocketRequest2
    {
        public int ConnectionNumber { get; }

        public Socket Listener { get; }
        public Socket Server { get; set; }

        public byte[] ReceivedData { get; set; }
        public int BytesReceived { get; set; }
        public byte[] SentData { get; set; }
        public int BytesSent { get; set; }

        public SocketException SocketCreationFailure { get; set; }
        public SocketException DataReceiveFailure { get; set; }
        public SocketException DataSendFailure { get; set; }

        public bool SocketCreationComplete { get; set; }
        public bool DataReceiveComplete { get; set; }
        public bool DataSendComplete { get; set; }

        public bool Failure { get => !(SocketCreationFailure is null && DataReceiveFailure is null && DataSendFailure is null); }
        public bool Success { get => SocketCreationComplete && DataReceiveComplete && DataSendComplete; }

        public bool Complete { get => Success || Failure; }

        public SocketRequest2(int connectionNumber, Socket listener)
        {
            ConnectionNumber = connectionNumber;
            Listener = listener;
        }
    }
    */

    public static class MyExtensions
    {
        public static byte[] ToByteArray(this string str) =>
            str.ToCharArray().Select(c => (byte)c).ToArray();

        public static string ToOutputString(this byte[] arr) =>
            string.Join("", arr.Select(b => (char)b).ToArray());
    }
}
