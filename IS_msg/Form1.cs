using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace IS_msg
{
    public partial class Form1 : Form
    {
        const int UDP_PORT = 6000;

        readonly UdpClient udpBroadcaster = new UdpClient();
        readonly UdpClient udpListener;
        TcpListener tcpListener;
        int tcpPort;

        string myName;
        Dictionary<string, IPEndPoint> discoveredUsers = new Dictionary<string, IPEndPoint>();
        Dictionary<string, List<string>> chatHistory = new Dictionary<string, List<string>>();
        string currentChatUser = null;

        public Form1()
        {
            InitializeComponent();

            // Allow multiple instances to bind UDP port 6000 on the same machine
            udpListener = new UdpClient();
            udpListener.Client.SetSocketOption(
                SocketOptionLevel.Socket,
                SocketOptionName.ReuseAddress,
                true);
            udpListener.Client.Bind(new IPEndPoint(IPAddress.Any, UDP_PORT));

            // Wire up events
            this.Load += Form1_Load;
            this.FormClosing += Form1_FormClosing;
            this.btnSend.Click += btnSend_Click;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // Choose your display name
            myName = Environment.MachineName;

            // Start TCP listener on an ephemeral port
            tcpListener = new TcpListener(IPAddress.Any, 0);
            tcpListener.Server.SetSocketOption(
                SocketOptionLevel.Socket,
                SocketOptionName.ReuseAddress,
                true);
            tcpListener.Start();
            tcpPort = ((IPEndPoint)tcpListener.LocalEndpoint).Port;

            // Immediately announce our presence three times (with our port)
            SendHello();
            Thread.Sleep(100);
            SendHello();
            Thread.Sleep(100);
            SendHello();

            // Start background loops
            new Thread(UdpBroadcastLoop) { IsBackground = true }.Start();
            new Thread(UdpListenLoop) { IsBackground = true }.Start();
            new Thread(TcpListenLoop) { IsBackground = true }.Start();
        }

        /// <summary>
        /// Broadcasts a single HELLO packet announcing our name & port.
        /// </summary>
        private void SendHello()
        {
            string hello = $"HELLO|{myName}|{tcpPort}";
            byte[] data = Encoding.UTF8.GetBytes(hello);
            var ep = new IPEndPoint(IPAddress.Broadcast, UDP_PORT);
            udpBroadcaster.Send(data, data.Length, ep);
        }

        private void UdpBroadcastLoop()
        {
            while (true)
            {
                SendHello();
                Thread.Sleep(3000); // every 3 seconds
            }
        }

        private void UdpListenLoop()
        {
            while (true)
            {
                IPEndPoint remoteEP = null;
                byte[] data = udpListener.Receive(ref remoteEP);
                string msg = Encoding.UTF8.GetString(data);
                string[] parts = msg.Split('|');

                // Expect: HELLO|TheirName|TheirPort
                if (parts.Length == 3 && parts[0] == "HELLO" && parts[1] != myName)
                {
                    string theirName = parts[1];
                    if (int.TryParse(parts[2], out int theirPort))
                    {
                        var ep = new IPEndPoint(remoteEP.Address, theirPort);
                        if (!discoveredUsers.ContainsKey(theirName))
                        {
                            discoveredUsers[theirName] = ep;
                            this.Invoke((MethodInvoker)(() => AddUserButton(theirName)));
                        }
                    }
                }
            }
        }

        private void AddUserButton(string userName)
        {
            var btn = new Button
            {
                Text = userName,
                Dock = DockStyle.Top,
                Height = 30
            };
            btn.Click += (s, e) => OpenChat(userName);
            panelUsers.Controls.Add(btn);
        }

        private void OpenChat(string userName)
        {
            currentChatUser = userName;
            txtChatHistory.Clear();

            if (!chatHistory.ContainsKey(userName))
                chatHistory[userName] = new List<string>();

            foreach (string line in chatHistory[userName])
                txtChatHistory.AppendText(line + Environment.NewLine);
        }

        private void TcpListenLoop()
        {
            while (true)
            {
                using (var client = tcpListener.AcceptTcpClient())
                {
                    var ns = client.GetStream();
                    byte[] buf = new byte[4096];
                    int len = ns.Read(buf, 0, buf.Length);
                    string msg = Encoding.UTF8.GetString(buf, 0, len);

                    // Expect: MSG|SenderName|The text…
                    string[] parts = msg.Split(new[] { '|' }, 3);
                    if (parts.Length == 3 && parts[0] == "MSG")
                    {
                        string sender = parts[1], text = parts[2];
                        string line = $"{sender}: {text}";

                        if (!chatHistory.ContainsKey(sender))
                            chatHistory[sender] = new List<string>();
                        chatHistory[sender].Add(line);

                        this.Invoke((MethodInvoker)(() =>
                        {
                            if (currentChatUser == sender)
                                txtChatHistory.AppendText(line + Environment.NewLine);
                            // else you could visually flag the sender’s button
                        }));
                    }
                }
            }
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            if (currentChatUser == null)
            {
                MessageBox.Show(
                    "Select a user first!",
                    "No User Selected",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            string text = txtMessageInput.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            string full = $"MSG|{myName}|{text}";
            byte[] data = Encoding.UTF8.GetBytes(full);

            var ep = discoveredUsers[currentChatUser];
            try
            {
                using (var client = new TcpClient(ep.Address.ToString(), ep.Port))
                {
                    client.GetStream().Write(data, 0, data.Length);
                }

                // Append to our chat and clear input
                string line = $"Me: {text}";
                chatHistory[currentChatUser].Add(line);
                txtChatHistory.AppendText(line + Environment.NewLine);
                txtMessageInput.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Send failed: " + ex.Message,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            udpListener.Close();
            udpBroadcaster.Close();
            tcpListener?.Stop();
        }
    }
}
