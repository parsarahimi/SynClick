using System;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Input;
namespace MultiClient
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

        private static readonly Socket ClientSocket = new Socket
            (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private static string Name;
        private static IPAddress serverip;
        private const int PORT = 8000;

        private static bool EnableClick = true;
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
        static void MonitorKeypress()
        {
            while (true)
            {
                short MonitorME = GetAsyncKeyState(System.Windows.Forms.Keys.XButton1);
                short MonitorME2 = GetAsyncKeyState(System.Windows.Forms.Keys.XButton2);
                //Console.WriteLine("MonitorME:" + MonitorME.ToString());
                if (MonitorME < 0)//when key is pressed its -32768 otherwise 0
                {
                    byte[] buffer = Encoding.ASCII.GetBytes("shoot");
                    ClientSocket.Send(buffer, 0, buffer.Length, SocketFlags.None);
                    Thread.Sleep(150);
                }
                if (MonitorME2 < 0)
                {
                    EnableClick = !EnableClick;
                    if (EnableClick)
                    {
                        Console.WriteLine("Click is enabled");
                        System.Media.SoundPlayer player = new System.Media.SoundPlayer("enabled.wav");
                        player.Play();
                    }
                    else
                    {
                        Console.WriteLine("Click is disabled");
                        System.Media.SoundPlayer player = new System.Media.SoundPlayer("disabled.wav");
                        player.Play();
                    }
                    Thread.Sleep(150);
                }
                Thread.Sleep(1);
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
            Console.Title = "Client";
            Name = "";
            while (Name=="")
            {
                Console.Write("Enter name: ");
                Name = Console.ReadLine();
            }
            Console.Write("Enter IP: ");
            serverip = IPAddress.Parse(Console.ReadLine());
            ConnectToServer();
            RequestLoop();
            Exit();
        }

        private static void ConnectToServer()
        {
            int attempts = 0;
            Stopwatch stopwatch = new Stopwatch();
            while (!ClientSocket.Connected)
            {
                try
                {
                    attempts++;
                    Console.WriteLine("Connection attempt " + attempts);
                    // Change IPAddress.Loopback to a remote IP to connect to a remote host.
                    stopwatch.Start();
                    ClientSocket.Connect(serverip, PORT);
                }
                catch (SocketException)
                {
                    Console.Clear();
                }
            }
            string howlong = ((1000 * (double)stopwatch.ElapsedTicks) / Stopwatch.Frequency).ToString();
            Console.Clear();
            Console.WriteLine("ping: "+ howlong + " ms");
            Console.WriteLine("Open cmd and ping the server again to get a more reliable number, like: ping 127.0.0.1");
            Console.WriteLine("Connected");
            Console.WriteLine("Type in ping0.34 for your ping to be set to 340ms, it isn't just that ping though ^ its that plus the ping of the game server, so hold tab in game or whatever to check that too");
            byte[] buffer = Encoding.ASCII.GetBytes(howlong);
            ClientSocket.Send(buffer, 0, buffer.Length, SocketFlags.None);
        }

        private static void RequestLoop()
        {
            Console.WriteLine(@"<Type ""exit"" to properly disconnect client>");
            Thread t1 = new Thread(SendRequest);
            Thread t2 = new Thread(ReceiveResponse);
            Thread t3 = new Thread(MonitorKeypress);
            t1.Start();
            t2.Start();
            t3.Start();
            t1.Join();
        }

        /// <summary>
        /// Close socket and exit program.
        /// </summary>
        private static void Exit()
        {
            SendString("exit"); // Tell the server we are exiting
            ClientSocket.Shutdown(SocketShutdown.Both);
            ClientSocket.Close();
            Environment.Exit(0);
        }

        private static void SendRequest()
        {
            while (true)
            {
                //Console.Write("Send a request: ");
            string request = Name+": "+Console.ReadLine();
            SendString(request);
            if (request == (Name + ": " + "sync"))
            {
                    request = Name + ": " + GetTime().ToString();
                    Console.WriteLine("myTime: "+ GetTime().ToString());
                    SendString(request);
            }
            if (request == (Name + ": " + "exit"))
            {
                Exit();
            }
            }
        }

        /// <summary>
        /// Sends a string to the server with ASCII encoding.
        /// </summary>
        private static void SendString(string text)
        {
            byte[] buffer = Encoding.ASCII.GetBytes(text);
            ClientSocket.Send(buffer, 0, buffer.Length, SocketFlags.None);
        }

        private static void ReceiveResponse()
        {
            while (true)
            {
                var buffer = new byte[2048];
                int received = ClientSocket.Receive(buffer, SocketFlags.None);
                if (received == 0) return;
                var data = new byte[received];
                Array.Copy(buffer, data, received);
                string text = Encoding.ASCII.GetString(data);//fireinmilisec
                if ((text.IndexOf("fireinmilisec") > -1) && EnableClick)
                {
                    double delay = double.Parse(text.Substring(13 + text.IndexOf("fireinmilisec")));
                    Thread t = new Thread(() => Shoot(delay));
                    t.Start();
                    System.Media.SoundPlayer player = new System.Media.SoundPlayer("firing.wav");
                    player.Play();
                }
                Console.WriteLine(text);
            }
        }
        private static void Shoot(double howlong)
        {
            Console.WriteLine("Recieved fire order " + howlong.ToString()+" seconds");
            BusySleep(howlong);
            mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
        }
    }
}
//IPAddress ipAddress = IPAddress.Parse("172.21.5.99");
//IPEndPoint remoteEP = new IPEndPoint(ipAddress, 11000);
//Socket sender = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
//byte[] buffer = new byte[1024];
//sender.Send(buffer);
//int size = sender.Receive(buffer);
//sender.Close();