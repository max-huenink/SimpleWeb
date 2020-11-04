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
    class WebServer
    {
        private static readonly string webRoot = Path.Combine(Directory.GetCurrentDirectory(), "www");
        private static readonly Random rnd = new Random();

        static async Task Main(string[] args)
        {
            int port = 8888;
            var ipAddress = IPAddress.Parse("127.0.0.1");
            var endPoint = new IPEndPoint(ipAddress, port);

            // Create, bind, and listen on a socket
            using var listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(endPoint);
            listener.Listen(10);

            int connections = 0;
            var tasks = new List<Task>();

            Console.WriteLine("Press Control + D to gracefully exit when there are existing connection requests");
            Console.WriteLine("Press Control + C to forcefully exit");
            Console.WriteLine($"Listening at {ipAddress}:{port}");
            ConsoleKeyInfo keyInfo = new ConsoleKeyInfo();

            // Loop until ^D is pressed
            do
            {
                // Accept the incoming connection on Socket server
                var server = await listener.AcceptAsync();

                // Send and receive on the new socket
                tasks.Add(Task.Run(async () => await ReceiveAndRespond(server, connections++)));

                // Read in a key press, if any
                if (Console.KeyAvailable)
                {
                    keyInfo = Console.ReadKey(true);
                }
            }
            while (!(keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control) && keyInfo.Key.ToString().ToLower() == "d"));

            try
            {
                // Wait for all connections to complete
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

        /// <summary>
        /// Receives a message from a client, processes the request, and responds accordingly
        /// </summary>
        /// <param name="server">The Socket the connection is on</param>
        /// <param name="connectionNumber">The current connection number (used for output to console)</param>
        /// <returns>A Task</returns>
        private static async Task ReceiveAndRespond(Socket server, int connectionNumber)
        {
            // Read data
            var receivedData = new byte[20000];
            int bytesReceived = await server.ReceiveAsync(receivedData, SocketFlags.None);

            // Parse data
            var stringData = receivedData[..bytesReceived].ToOutputString();
            var data = stringData.Split(" ");
            var message = $"Connection {connectionNumber} Received: {stringData}";

            byte[] sendBuffer;

            string myOkMessage = $"HTTP/1.1 200 OK\r\nContent-Type: text/html\r\nContent-length: ";
            var myFilePath = Path.Combine(webRoot, data[1][1..]);

            // If the request matches GET /
            if (data[0] == "GET" && data[1].Equals("/"))
            {
                var fileList = $"<html>\n" +
                    $"<head>\n" +
                    $"    <title>index</title>\n" +
                    $"</head>\n" +
                    $"<body>\n" +
                    $"{string.Join("\n", Directory.GetFiles(webRoot).Select(f => $@"    <p><a href=""{Path.GetFileName(f)}"">{Path.GetFileName(f)}</a></p>"))}\n" +
                    $"</body>\n" +
                    $"</html>\n";
                myOkMessage += $"{fileList.Length}\r\n\n{fileList}";
                sendBuffer = myOkMessage.ToByteArray();
            }
            // If requested file exists
            else if (data[0] == "GET" && File.Exists(myFilePath))
            {
                var file = File.ReadAllText(myFilePath);
                myOkMessage += $"{file.Length}\r\n\n{file}";
                sendBuffer = myOkMessage.ToByteArray();
            }
            // File does not exist or not GET request, return 404
            else
            {
                var file = File.ReadAllText(Path.Combine(webRoot, "404.html"));
                sendBuffer = $"HTTP/1.1 404 Not Found\r\nContent-Type: text/html\r\nContent-length: {file.Length}\r\n\n{file}".ToByteArray();
            }

            // Send reponse
            int bytesSent = await server.SendAsync(sendBuffer, SocketFlags.None);

            // Check status of send
            if (bytesSent == sendBuffer.Length)
            {
                message += $"\nSuccessfully sent {bytesSent} bytes of data";
            }
            else
            {
                message += $"\nFailed to send data";
            }
            Console.WriteLine(message);
            server.Close();
        }

    }

    public static class MyExtensions
    {
        /// <summary>
        /// Turns a string into an array of bytes
        /// </summary>
        /// <returns>A byte[] representing the string</returns>
        public static byte[] ToByteArray(this string str) =>
            str.ToCharArray().Select(c => (byte)c).ToArray();

        /// <summary>
        /// Turns an array of bytes into a string
        /// </summary>
        /// <returns>A string representing the byte[]</returns>
        public static string ToOutputString(this byte[] arr) =>
            string.Join("", arr.Select(b => (char)b).ToArray());
    }
}
