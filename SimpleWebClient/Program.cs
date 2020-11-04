using System;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using System.Runtime.CompilerServices;

namespace SimpleWebClient
{
    class Program
    {
        static int requestCounter;
        private static string[] programArguments;
        static async Task Main(string[] args)
        {
            programArguments = args;
            var tasks = new List<Task>();

            var address = GetArgumentIfExist(0);
            if(address is null)
            {
                Console.WriteLine("Enter a host address: ");
                address = Console.ReadLine();
            }

            if (!int.TryParse(GetArgumentIfExist(1), out int port))
            {
                do
                {
                    Console.Write("Enter a port number: ");
                }
                while (!int.TryParse(Console.ReadLine(), out port));
            }

            var fileName = GetArgumentIfExist(2) ?? string.Empty;

            if(!int.TryParse(GetArgumentIfExist(3), out int attempts))
            {
                attempts = 1;
            }

            var requestToSend = $"GET /{fileName}".ToByteArray();

            Console.WriteLine($"Attempting to connect to {address}:{port} and perform the following request {attempts} times");
            Console.WriteLine(requestToSend.ToOutputString());

            for (int i = 0; i < attempts; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    using var tcpClient = new TcpClient(address, port);
                    using var stream = tcpClient.GetStream();
                    await SendAndReceive(stream, requestToSend);
                }));
            }
            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Process failed with: {ex.Message}");
            }
            finally
            {
                Console.WriteLine($"Successfully completed {requestCounter} requests");
            }
        }

        private static async Task SendAndReceive(NetworkStream stream, byte[] sendBuffer)
        {
            try
            {
                await stream.WriteAsync(sendBuffer);
                var receivedData = new byte[20000];
                var bytesReceived = await stream.ReadAsync(receivedData);
                Console.WriteLine($"\n{receivedData[..bytesReceived].ToOutputString()}\n");
                Interlocked.Increment(ref requestCounter);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection failed with exception message: {ex.Message}");
            }
            finally
            {
                stream.Close();
            }
        }

        private static string GetArgumentIfExist(int index)
        {
            string arg = null;
            if (programArguments.Length > index)
            {
                arg = programArguments[index];
            }
            return arg;
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
