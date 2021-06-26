using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Linq;
using System.Windows.Forms;

namespace MultiServer
{
    class Program
    {
        [DllImport("user32.dll")]
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, uint dwExtraInfo);
        private const int MOUSEEVENTF_LEFTDOWN = 0x02;
        private const int MOUSEEVENTF_LEFTUP = 0x04;
        private const int MOUSEEVENTF_RIGHTDOWN = 0x08;
        private const int MOUSEEVENTF_RIGHTUP = 0x10;
        [DllImport("User32.dll")]
        private static extern short GetAsyncKeyState(System.Windows.Forms.Keys vKey);

        private static readonly Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private static readonly List<Socket> clientSockets = new List<Socket>();
        private static List<double> clientPings = new List<double>();
        private static List<bool> clientPremission = new List<bool>();
        private static string password;
        private const int BUFFER_SIZE = 2048;
        private static int PORT = 8000;
        private static readonly byte[] buffer = new byte[BUFFER_SIZE];
        static void BusySleep(double duration)
        {
            Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            while (true)
            {
                if ((((double)stopwatch.ElapsedTicks) / Stopwatch.Frequency) >= duration)
                {
                    return;
                }
                //Thread.Sleep(50);
            }
        }
        static double GetTime()
        {
            DateTime unixStart = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            long unixTimeStampInTicks;
            double timestamp;
            unixTimeStampInTicks = (DateTime.UtcNow - unixStart).Ticks;
            timestamp = (double)unixTimeStampInTicks / TimeSpan.TicksPerSecond;
            return timestamp;
        }
        static void Main()
        {
            Console.Title = "Server";
            Console.WriteLine("Write password for shoot");
            password = Console.ReadLine();
            Console.WriteLine("Write server port for shoot, write nothing to use port 8000 (default)");
            string userinp = Console.ReadLine();
            if (userinp!="")
            {
                PORT = Int32.Parse(userinp);
            }
            SetupServer();
            Console.ReadLine(); // When we press enter close everything
            CloseAllSockets();
        }

        private static void SetupServer()
        {
            Console.WriteLine(String.Format("Setting up server on port {0}...",PORT));
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, PORT));
            serverSocket.Listen(0);
            serverSocket.BeginAccept(AcceptCallback, null);
            Console.WriteLine("Server setup complete");
        }

        /// <summary>
        /// Close all connected client (we do not need to shutdown the server socket as its connections
        /// are already closed with the clients).
        /// </summary>
        private static void CloseAllSockets()
        {
            foreach (Socket socket in clientSockets)
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
            }

            serverSocket.Close();
        }

        private static void AcceptCallback(IAsyncResult AR)
        {
            Socket socket;

            try
            {
                socket = serverSocket.EndAccept(AR);
            }
            catch (ObjectDisposedException) // I cannot seem to avoid this (on exit when properly closing sockets)
            {
                return;
            }

            clientSockets.Add(socket);
            clientPings.Add(0.0);
            clientPremission.Add(false);
            socket.BeginReceive(buffer, 0, BUFFER_SIZE, SocketFlags.None, ReceiveCallback, socket);
            Console.WriteLine("Client connected, waiting for request...");
            serverSocket.BeginAccept(AcceptCallback, null);
        }

        private static void ReceiveCallback(IAsyncResult AR)
        {
            Socket current = (Socket)AR.AsyncState;
            int received;

            try
            {
                received = current.EndReceive(AR);
            }
            catch (SocketException)
            {
                Console.WriteLine("Client forcefully disconnected");
                // Don't shutdown because the socket may be disposed and its disconnected anyway.
                current.Close();
                clientPremission.Remove(clientPremission[clientSockets.IndexOf(current)]);
                clientPings.Remove(clientPings[clientSockets.IndexOf(current)]);
                clientSockets.Remove(current);
                return;
            }

            byte[] recBuf = new byte[received];
            Array.Copy(buffer, recBuf, received);
            string text = Encoding.ASCII.GetString(recBuf);
            Console.WriteLine("Received Text: " + text);

            if (text.IndexOf("sync") > -1) // Client requested time
            {
                Console.WriteLine("Text is a get time request");
                byte[] data = Encoding.ASCII.GetBytes("ServerTime: " +GetTime().ToString());//DateTime.Now.ToLongTimeString());
                //current.Send(data);
                foreach (Socket socket in clientSockets)
                {
                    socket.Send(data);
                }
                Console.WriteLine("Time sent to client");
            }
            else if (text.IndexOf("ping") > -1) // Client requested time
            {
                //Console.WriteLine("Text is a get time request");
                //byte[] data = Encoding.ASCII.GetBytes("ServerTime: " + GetTime().ToString());//DateTime.Now.ToLongTimeString());
                //current.Send(data);
                for (int i=0;i<clientSockets.Count;i++)
                {
                    Socket socket = clientSockets[i];
                    if (socket == current)
                    {
                        Console.WriteLine(text.Substring(4+text.IndexOf("ping")));
                        //Console.ReadLine();
                        clientPings[i] = double.Parse(text.Substring(4+text.IndexOf("ping")));
                        Console.WriteLine("Ping recieved to be: "+ clientPings[i].ToString());
                    }

                }
                //Console.WriteLine("Time sent to client");
            }
            else if (text.IndexOf("shoot") > -1 && clientPremission[clientSockets.IndexOf(current)])
            {
                double slowest = clientPings.Max();
                Console.WriteLine("Shoot recieved with slowest being "+slowest.ToString());
                for (int i = 0; i < clientSockets.Count; i++)
                {
                    Socket socket = clientSockets[i];
                    byte[] data = Encoding.ASCII.GetBytes("fireinmilisec" + ((slowest-clientPings[i])/2).ToString());
                    socket.Send(data);
                }
            }
            else if (text.ToLower() == "exit") // Client wants to exit gracefully
            {
                // Always Shutdown before closing
                current.Shutdown(SocketShutdown.Both);
                current.Close();
                clientPremission.Remove(clientPremission[clientSockets.IndexOf(current)]);
                clientPings.Remove(clientPings[clientSockets.IndexOf(current)]);
                clientSockets.Remove(current);
                Console.WriteLine("Client disconnected");
                return;
            }
            else if (text.IndexOf(password) > -1) // Client wants to exit gracefully
            {
                clientPremission[clientSockets.IndexOf(current)] = true;
            }
            else
            {
                //Console.WriteLine("Text is an invalid request");
                //byte[] data = Encoding.ASCII.GetBytes("Invalid request");
                foreach (Socket socket in clientSockets)
                {
                    if (socket != current)
                    {
                        socket.Send(recBuf);
                    }
                        
                }
                    
                //Console.WriteLine("Warning Sent");
            }

            current.BeginReceive(buffer, 0, BUFFER_SIZE, SocketFlags.None, ReceiveCallback, current);
        }
    }
}

//IPHostEntry host = Dns.GetHostEntry("localhost");
//IPAddress ipAddress = host.AddressList[0];
//IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 11000);
//Socket listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
//listener.Bind(localEndPoint);
//listener.Listen(10);
//Socket handler = listener.Accept();
//byte[] buffer = new byte[1024];
//int bytesRec = handler.Receive(buffer);
//handler.Send(buffer);
//handler.Shutdown(SocketShutdown.Both);
//handler.Close();