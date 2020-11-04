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
    class WebClient
    {
        static int successfulRequests;
        private static string[] programArguments;
        static async Task Main(string[] args)
        {
            programArguments = args;
            var tasks = new List<Task>();

            // Gets the address from argument list or console
            var address = GetArgumentIfExist(0);
            if(address is null)
            {
                Console.Write("Enter a host address: ");
                address = Console.ReadLine();
            }

            // Gets the port from argument list or console
            if (!int.TryParse(GetArgumentIfExist(1), out int port))
            {
                do
                {
                    Console.Write("\nEnter a port number: ");
                }
                while (!int.TryParse(Console.ReadLine(), out port));
            }

            // Gets the fileName from argument list or console
            var fileName = GetArgumentIfExist(2);
            if(fileName is null)
            {
                Console.Write("\nComplete the GET request\nGET /");
                fileName = Console.ReadLine();
            }

            // Gets the number of attempts from argument list
            // Defaults to 1
            if(!int.TryParse(GetArgumentIfExist(3), out int attempts))
            {
                attempts = 1;
            }

            var requestToSend = $"GET /{fileName}".ToByteArray();

            Console.WriteLine($"\nAttempting to connect to {address}:{port} and perform the following request {attempts} times");
            Console.WriteLine(requestToSend.ToOutputString());

            for (int i = 0; i < attempts; i++)
            {
                // Run the client on a new thread
                tasks.Add(Task.Run(async () =>
                {
                    // The client
                    using var tcpClient = new TcpClient(address, port);
                    // The stream representing the client's IO
                    using var stream = tcpClient.GetStream();
                    // Request data and receive a response from the stream
                    await RequestAndReceive(stream, requestToSend);
                }));
            }

            try
            {
                // Wait for all clients to comlete
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Process failed with: {ex.Message}");
            }
            finally
            {
                Console.WriteLine($"Successfully completed {successfulRequests} requests");
            }
        }

        /// <summary>
        /// Request a file from the server and receive a response
        /// </summary>
        /// <param name="stream">The network stream to send/receive data from</param>
        /// <param name="sendBuffer">The request to send to the server</param>
        /// <returns></returns>
        private static async Task RequestAndReceive(NetworkStream stream, byte[] sendBuffer)
        {
            try
            {
                // Send data to the server
                await stream.WriteAsync(sendBuffer);

                // Receive data from the server
                var receivedData = new byte[20000];
                var bytesReceived = await stream.ReadAsync(receivedData);

                // Output data and safely incremenet successful connection counter
                Console.WriteLine($"\n{receivedData[..bytesReceived].ToOutputString()}\n");
                Interlocked.Increment(ref successfulRequests);
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

        /// <summary>
        /// Gets the argument from the argument list if it exists
        /// </summary>
        /// <param name="index">The index of the argument in the list</param>
        /// <returns>A string representing the argument, null if it does not exist</returns>
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
