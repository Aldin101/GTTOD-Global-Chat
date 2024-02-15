using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

namespace Server
{

    class Start
    {
        static void Main(string[] args)
        {
            Program program = new Program();
            program.start(args);
        }
    }

    class Program
    {
        public TcpListener server;
        public List<TcpClient> clients = new List<TcpClient>();

        public List<TcpClient> clientsMarkedForDeletion = new List<TcpClient>();
        
        public void start(string[] args)
        {
            server = new TcpListener(IPAddress.Any, 80);
            server.Start();
            Console.WriteLine("Server has started");
            acceptClients();
        }

        public async void acceptClients()
        {
            while (true)
            {
                TcpClient client = await server.AcceptTcpClientAsync();
                clients.Add(client);
                Console.WriteLine("Client has connected");

                receiveMessages(client);
            }
        }

        public async void receiveMessages(TcpClient client)
        {
            while (true)
            {
                if (!client.Connected)
                {
                    clientsMarkedForDeletion.Add(client);
                    break;
                }
                NetworkStream stream = client.GetStream();
                try
                {
                    if (!stream.DataAvailable) continue;
                    byte[] buffer = new byte[1024];
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    sendMessage(message);
                }
                catch
                {
                    clientsMarkedForDeletion.Add(client);
                    break;
                }
                Thread.Sleep(100);
            }
        }


        public void sendMessage(String message)
        {
            foreach (TcpClient client in clients)
            {
                try
                {
                    NetworkStream stream = client.GetStream();
                    byte[] buffer = Encoding.UTF8.GetBytes(message);
                    stream.Write(buffer, 0, buffer.Length);
                    stream.Flush();
                } catch
                {
                    clientsMarkedForDeletion.Add(client);
                }
            }
        }
    }
}