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
        const int TCP_PORT = 5001;

        readonly UdpClient udpBroadcaster = new UdpClient();
        readonly UdpClient udpListener = new UdpClient(UDP_PORT);
        TcpListener tcpListener;

        string myName;
        Dictionary<string, IPEndPoint> discoveredUsers = new Dictionary<string, IPEndPoint>();
        Dictionary<string, List<string>> chatHistory = new Dictionary<string, List<string>>();
        string currentChatUser = null;

        public Form1()
        {
            InitializeComponent();
            // wire-up send button if not done in designer
            this.btnSend.Click += btnSend_Click;
            this.Load += Form1_Load;
            this.FormClosing += Form1_FormClosing;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // choose a display name
            myName = Environment.MachineName;

            // Immediately announce our presence three times
            SendHello();
            Thread.Sleep(100);
            SendHello();
            Thread.Sleep(100);
            SendHello();

            // start background threads
            new Thread(UdpBroadcastLoop) { IsBackground = true }.Start();
            new Thread(UdpListenLoop) { IsBackground = true }.Start();
            new Thread(TcpListenLoop) { IsBackground = true }.Start();
        }

        /// <summary>
        /// Broadcasts a single HELLO packet announcing our presence.
        /// </summary>
        private void SendHello()
        {
            string hello = $"HELLO|{myName}";
            byte[] data = Encoding.UTF8.GetBytes(hello);
            var ep = new IPEndPoint(IPAddress.Broadcast, UDP_PORT);
            udpBroadcaster.Send(data, data.Length, ep);
        }

        private void UdpBroadcastLoop()
        {
            while (true)
            {
                SendHello();
                Thread.Sleep(3000);  // every 3 seconds
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

                if (parts.Length == 2 && parts[0] == "HELLO" && parts[1] != myName)
                {
                    string theirName = parts[1];
                    if (!discoveredUsers.ContainsKey(theirName))
                    {
                        discoveredUsers[theirName] = remoteEP;
                        this.Invoke((MethodInvoker)(() => AddUserButton(theirName)));
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
            tcpListener = new TcpListener(IPAddress.Any, TCP_PORT);
            tcpListener.Start();

            while (true)
            {
                using (var client = tcpListener.AcceptTcpClient())
                {
                    var ns = client.GetStream();
                    byte[] buf = new byte[4096];
                    int len = ns.Read(buf, 0, buf.Length);
                    string msg = Encoding.UTF8.GetString(buf, 0, len);

                    string[] parts = msg.Split(new[] { '|' }, 3);
                    if (parts.Length == 3 && parts[0] == "MSG")
                    {
                        string sender = parts[1],
                               text = parts[2];
                        string line = $"{sender}: {text}";

                        if (!chatHistory.ContainsKey(sender))
                            chatHistory[sender] = new List<string>();
                        chatHistory[sender].Add(line);

                        this.Invoke((MethodInvoker)(() =>
                        {
                            if (currentChatUser == sender)
                                txtChatHistory.AppendText(line + Environment.NewLine);
                            // else you could highlight the sender’s button here
                        }));
                    }
                }
            }
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            if (currentChatUser == null)
            {
                MessageBox.Show("Select a user first!", "No User Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string text = txtMessageInput.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            string full = $"MSG|{myName}|{text}";
            byte[] data = Encoding.UTF8.GetBytes(full);
            var ep = discoveredUsers[currentChatUser];

            try
            {
                using (var client = new TcpClient(ep.Address.ToString(), TCP_PORT))
                {
                    client.GetStream().Write(data, 0, data.Length);
                }

                // update local chat history
                string line = $"Me: {text}";
                chatHistory[currentChatUser].Add(line);
                txtChatHistory.AppendText(line + Environment.NewLine);
                txtMessageInput.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Send failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
