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

            Console.WriteLine("Press Control + D to gracefully exit when there are existing connection requests");
            Console.WriteLine("Press Control + C to forcefully exit");
            Console.WriteLine($"Listening at {ipAddress}:{port}");
            ConsoleKeyInfo keyInfo = new ConsoleKeyInfo(' ', ConsoleKey.F24, false, false, false);

            do
            {
                var server = await listener.AcceptAsync();
                tasks.Add(Task.Run(async () => await ConnectAndHandle(server, connections++)));
                if (Console.KeyAvailable)
                {
                    keyInfo = Console.ReadKey(true);
                }
            }
            while (keyInfo.Key == ConsoleKey.F24 || !(keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control) && keyInfo.Key.ToString().ToLower() == "d"));

            try
            {
                await Task.WhenAll(tasks);
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Process failed with: {ex.Message}");
            }
            finally
            {
                Console.WriteLine($"Executed {connections} connections.");
            }
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

    }

    public static class MyExtensions
    {
        public static byte[] ToByteArray(this string str) =>
            str.ToCharArray().Select(c => (byte)c).ToArray();

        public static string ToOutputString(this byte[] arr) =>
            string.Join("", arr.Select(b => (char)b).ToArray());
    }
}
