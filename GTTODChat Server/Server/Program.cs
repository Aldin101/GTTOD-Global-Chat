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
        private TcpListener server = null!;
        private List<TcpClient> clients = new List<TcpClient>();
        private List<TcpClient> clientsMarkedForDeletion = new List<TcpClient>();

        public async Task Start(string[] args)
        {
            server = new TcpListener(IPAddress.Any, 80);
            server.Start();
            Console.WriteLine("Server has started");
            await AcceptClients();
        }

        private async Task AcceptClients()
        {
            while (true)
            {
                TcpClient client = await server.AcceptTcpClientAsync();
                clients.Add(client);
                Console.WriteLine("Client has connected");
                Task.Run(() => ReceiveMessages(client));
            }
        }

        private async Task ReceiveMessages(TcpClient client)
        {
            while (true)
            {
                if (!client.Connected)
                {
                    client.Close();
                    clients.Remove(client);
                    break;
                }
                try
                {
                    NetworkStream stream = client.GetStream();
                    if (!stream.DataAvailable)
                    {
                        async Task.Delay(100);
                        continue;
                    }
                    byte[] buffer = new byte[1024];
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    SendMessage(message);
                }
                catch
                {
                    client.Close();
                    clients.Remove(client);
                    break;
                }
                if (clientsMarkedForDeletion.Contains(client))
                {
                    clientsMarkedForDeletion.Remove(client);
                    client.Close();
                    clients.Remove(client);
                    break;
                }
            }
        }

        private void SendMessage(string message)
        {
            foreach (TcpClient client in clients)
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
                    clientsMarkedForDeletion.Add(client);
                }
            }
        }
    }
}
