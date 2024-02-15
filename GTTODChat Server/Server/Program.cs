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
            receiveMessages();
        }

        public async void acceptClients()
        {
            while (true)
            {
                TcpClient client = await server.AcceptTcpClientAsync();
                clients.Add(client);
                Console.WriteLine("Client has connected");
            }
        }

        public async void receiveMessages()
        {
            while (true)
            {
                try
                {
                    foreach (TcpClient client in clients)
                    {
                        if (!client.Connected)
                        {
                            clientsMarkedForDeletion.Add(client);
                        }
                        NetworkStream stream = client.GetStream();
                        try
                        {
                            if (!stream.DataAvailable) continue;
                            byte[] buffer = new byte[1024];
                            int bytesRead = stream.Read(buffer, 0, buffer.Length);
                            string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                            sendMessage(message);
                        }
                        catch
                        {
                            clientsMarkedForDeletion.Add(client);
                        }
                    }

                    foreach (TcpClient client in clientsMarkedForDeletion)
                    {
                        try
                        {
                            client.Close();
                        } catch
                        {
                            Console.WriteLine("Failed to close client connection, deleleting anyway...");
                        }

                        clients.Remove(client);
                        Console.WriteLine("Client has disconnected");
                    }

                    clientsMarkedForDeletion.Clear();
                } catch (Exception e)
                {
                    if (e is ObjectDisposedException || e is InvalidOperationException) continue;
                    Console.Error.WriteLine(e);
                }
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