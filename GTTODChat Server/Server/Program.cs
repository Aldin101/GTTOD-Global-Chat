using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    class Start
    {
        static void Main(string[] args)
        {
            Program program = new Program();
            program.Start(args).GetAwaiter().GetResult();
        }
    }

    class Program
    {
        private TcpListener _server;
        private readonly List<TcpClient> _clients = new List<TcpClient>();
        private readonly List<TcpClient> _clientsMarkedForDeletion = new List<TcpClient>();

        public async Task Start(string[] args)
        {
            _server = new TcpListener(IPAddress.Any, 25565);
            _server.Start();
            Console.WriteLine("Server has started");
            await AcceptClients();
        }

        private async Task AcceptClients()
        {
            while (true)
            {
                TcpClient client = await _server.AcceptTcpClientAsync();
                _clients.Add(client);
                Console.WriteLine("Client has connected");

                _ = ReceiveMessages(client);
            }
        }

        private async Task ReceiveMessages(TcpClient client)
        {
            while (true)
            {
                if (!client.Connected)
                {
                    _clientsMarkedForDeletion.Add(client);
                    break;
                }
                NetworkStream stream = client.GetStream();
                try
                {
                    if (!stream.DataAvailable) continue;
                    byte[] buffer = new byte[1024];
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    SendMessage(message);
                }
                catch
                {
                    _clientsMarkedForDeletion.Add(client);
                    break;
                }
                Thread.Sleep(100);
            }
        }

        private void SendMessage(string message)
        {
            foreach (TcpClient client in _clients)
            {
                try
                {
                    NetworkStream stream = client.GetStream();
                    byte[] buffer = Encoding.UTF8.GetBytes(message);
                    stream.Write(buffer, 0, buffer.Length);
                    stream.Flush();
                }
                catch
                {
                    _clientsMarkedForDeletion.Add(client);
                }
            }
        }
    }
}
