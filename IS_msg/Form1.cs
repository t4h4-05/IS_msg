using System;
using System.Collections.Generic;
using System.Drawing;
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
        // Time (ms) after which a user is considered offline if no heartbeat received
        const int USER_TIMEOUT = 10000;

        private UdpClient? udpBroadcaster;
        private UdpClient? udpListener;
        private TcpListener? tcpListener;
        private int tcpPort;
        private bool isRunning = true;

        private string? myName;
        private Dictionary<string, UserInfo> discoveredUsers = new Dictionary<string, UserInfo>();
        private Dictionary<string, List<string>> chatHistory = new Dictionary<string, List<string>>();
        private string? currentChatUser = null;
        private Button? currentSelectedButton = null;
        private System.Windows.Forms.Timer userTimeoutTimer = new System.Windows.Forms.Timer();

        // Class to track user information including last seen time
        private class UserInfo
        {
            public IPEndPoint Endpoint { get; set; }
            public DateTime LastSeen { get; set; }

            public UserInfo(IPEndPoint endpoint)
            {
                Endpoint = endpoint;
                LastSeen = DateTime.Now;
            }
        }

        public Form1()
        {
            InitializeComponent();

            // Wire up events
            this.Load += Form1_Load;
            this.FormClosing += Form1_FormClosing;
            this.btnSend.Click += btnSend_Click;

            // Setup timer to check for inactive users
            userTimeoutTimer.Interval = 500; // Check every 1 second
            userTimeoutTimer.Tick += UserTimeoutTimer_Tick;
        }

        private void Form1_Load(object? sender, EventArgs e)
        {
            // Choose your display name - add random suffix to avoid conflicts on same machine
            myName = Environment.MachineName + "_" + new Random().Next(1000, 9999);
            this.Text = $"Chat - {myName}";

            try
            {
                // Initialize UDP components
                udpBroadcaster = new UdpClient();
                udpBroadcaster.EnableBroadcast = true;

                udpListener = new UdpClient();
                udpListener.Client.SetSocketOption(
                    SocketOptionLevel.Socket,
                    SocketOptionName.ReuseAddress,
                    true);
                udpListener.Client.Bind(new IPEndPoint(IPAddress.Any, UDP_PORT));

                // Start TCP listener on an ephemeral port
                tcpListener = new TcpListener(IPAddress.Any, 0);
                tcpListener.Server.SetSocketOption(
                    SocketOptionLevel.Socket,
                    SocketOptionName.ReuseAddress,
                    true);
                tcpListener.Start();
                tcpPort = ((IPEndPoint)tcpListener.LocalEndpoint).Port;

                // Start background loops
                Thread broadcastThread = new Thread(UdpBroadcastLoop) { IsBackground = true };
                Thread listenThread = new Thread(UdpListenLoop) { IsBackground = true };
                Thread tcpThread = new Thread(TcpListenLoop) { IsBackground = true };

                broadcastThread.Start();
                listenThread.Start();
                tcpThread.Start();

                // Start the timeout timer
                userTimeoutTimer.Start();

                // Immediately announce our presence three times (with our port)
                SendHello();
                Thread.Sleep(100);
                SendHello();
                Thread.Sleep(100);
                SendHello();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error initializing: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UserTimeoutTimer_Tick(object? sender, EventArgs e)
        {
            List<string> usernamesToRemove = new List<string>();

            // Find users that haven't been seen recently
            foreach (var pair in discoveredUsers)
            {
                TimeSpan timeSinceLastSeen = DateTime.Now - pair.Value.LastSeen;
                if (timeSinceLastSeen.TotalMilliseconds > USER_TIMEOUT)
                {
                    usernamesToRemove.Add(pair.Key);
                }
            }

            // Remove stale users
            foreach (string username in usernamesToRemove)
            {
                discoveredUsers.Remove(username);
                RemoveUserButton(username);
            }
        }

        /// <summary>
        /// Broadcasts a single HELLO packet announcing our name & port.
        /// </summary>
        private void SendHello()
        {
            try
            {
                if (myName == null || udpBroadcaster == null) return;

                string hello = $"HELLO|{myName}|{tcpPort}";
                byte[] data = Encoding.UTF8.GetBytes(hello);
                var ep = new IPEndPoint(IPAddress.Broadcast, UDP_PORT);
                udpBroadcaster.Send(data, data.Length, ep);
            }
            catch (Exception ex)
            {
                // Log error or notify user if needed
                Console.WriteLine("Error sending hello: " + ex.Message);
            }
        }

        private void UdpBroadcastLoop()
        {
            while (isRunning)
            {
                try
                {
                    SendHello();
                    Thread.Sleep(3000); // every 3 seconds
                }
                catch
                {
                    // Handle any exceptions to keep the thread alive
                    Thread.Sleep(3000);
                }
            }
        }

        private void UdpListenLoop()
        {
            while (isRunning)
            {
                try
                {
                    if (udpListener == null)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
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

                            if (discoveredUsers.TryGetValue(theirName, out UserInfo? existingUser))
                            {
                                // Update the last seen time and endpoint (if changed)
                                existingUser.LastSeen = DateTime.Now;
                                existingUser.Endpoint = ep;
                            }
                            else
                            {
                                // New user - add to dictionary and create UI element
                                discoveredUsers[theirName] = new UserInfo(ep);
                                this.BeginInvoke((MethodInvoker)(() => AddUserButton(theirName)));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Only log if still running to avoid spam during shutdown
                    if (isRunning)
                        Console.WriteLine("UDP Listen error: " + ex.Message);

                    Thread.Sleep(1000); // Prevent CPU spike on errors
                }
            }
        }

        private void AddUserButton(string userName)
        {
            try
            {
                // First check if button already exists before adding
                foreach (Control ctrl in panelUsers.Controls)
                {
                    if (ctrl is Button userButton && (string)userButton.Tag == userName)
                    {
                        // Button already exists, don't add duplicate
                        return;
                    }
                }

                // Add new button since it doesn't exist
                var newBtn = new Button
                {
                    Text = userName,
                    Dock = DockStyle.Top,
                    Height = 30,
                    Tag = userName,
                    BackColor = SystemColors.Control
                };
                newBtn.Click += (s, e) => OpenChat(userName);
                panelUsers.Controls.Add(newBtn);
                panelUsers.Controls.SetChildIndex(newBtn, 0); // Add at top
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error adding user button: " + ex.Message);
            }
        }

        private void RemoveUserButton(string userName)
        {
            try
            {
                this.BeginInvoke((MethodInvoker)(() =>
                {
                    // Find and remove the button
                    foreach (Control ctrl in panelUsers.Controls)
                    {
                        if (ctrl is Button btn && (string)btn.Tag == userName)
                        {
                            panelUsers.Controls.Remove(btn);
                            btn.Dispose();

                            // If this was the currently selected user, clear the chat
                            if (currentChatUser == userName)
                            {
                                currentChatUser = null;
                                currentSelectedButton = null;
                                txtChatHistory.Clear();
                            }
                            break;
                        }
                    }
                }));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error removing user button: " + ex.Message);
            }
        }

        private void OpenChat(string userName)
        {
            currentChatUser = userName;
            txtChatHistory.Clear();

            // Update button colors - reset all buttons then highlight selected one
            foreach (Control ctrl in panelUsers.Controls)
            {
                if (ctrl is Button btn)
                {
                    // Reset color
                    btn.BackColor = SystemColors.Control;

                    // If this is the selected button, highlight it
                    if ((string)btn.Tag == userName)
                    {
                        btn.BackColor = Color.LightGreen; // Highlight selected user
                        currentSelectedButton = btn;
                    }
                }
            }

            if (!chatHistory.ContainsKey(userName))
                chatHistory[userName] = new List<string>();

            foreach (string line in chatHistory[userName])
                txtChatHistory.AppendText(line + Environment.NewLine);

            txtMessageInput.Focus();
        }

        private void TcpListenLoop()
        {
            while (isRunning)
            {
                TcpClient? client = null;
                try
                {
                    if (tcpListener == null)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    client = tcpListener.AcceptTcpClient();
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

                        // Make sure we have this user in our discovered users list
                        if (!discoveredUsers.ContainsKey(sender))
                        {
                            var senderEndpoint = ((IPEndPoint)client.Client.RemoteEndPoint!);
                            var ep = new IPEndPoint(senderEndpoint.Address, senderEndpoint.Port);
                            discoveredUsers[sender] = new UserInfo(ep);
                            this.BeginInvoke((MethodInvoker)(() => AddUserButton(sender)));
                        }
                        else
                        {
                            // Update their last seen time
                            discoveredUsers[sender].LastSeen = DateTime.Now;
                        }

                        if (!chatHistory.ContainsKey(sender))
                            chatHistory[sender] = new List<string>();
                        chatHistory[sender].Add(line);

                        this.BeginInvoke((MethodInvoker)(() =>
                        {
                            if (currentChatUser == sender)
                                txtChatHistory.AppendText(line + Environment.NewLine);
                            else
                            {
                                // Visually flag the sender's button
                                foreach (Control ctrl in panelUsers.Controls)
                                {
                                    if (ctrl is Button senderBtn && (string)senderBtn.Tag == sender)
                                    {
                                        senderBtn.BackColor = Color.LightBlue;
                                        break;
                                    }
                                }
                            }
                        }));
                    }
                }
                catch (Exception ex)
                {
                    // Only log if still running to avoid spam during shutdown
                    if (isRunning)
                        Console.WriteLine("TCP Listen error: " + ex.Message);

                    Thread.Sleep(1000); // Prevent CPU spike on errors
                }
                finally
                {
                    client?.Close();
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
            //actual code to send
            string text = txtMessageInput.Text.Trim();//<-This is where Encryption will happen
            if (string.IsNullOrEmpty(text)) return;

            if (!discoveredUsers.TryGetValue(currentChatUser, out UserInfo? userInfo))
            {
                MessageBox.Show(
                    "User is no longer available.",
                    "User Unavailable",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            string full = $"MSG|{myName}|{text}";
            byte[] data = Encoding.UTF8.GetBytes(full);

            try
            {
                using (var client = new TcpClient())
                {
                    // Add timeout to prevent hanging
                    var connectResult = client.BeginConnect(userInfo.Endpoint.Address, userInfo.Endpoint.Port, null, null);
                    bool success = connectResult.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2));

                    if (!success)
                    {
                        throw new Exception("Connection timed out.");
                    }

                    client.EndConnect(connectResult);
                    client.GetStream().Write(data, 0, data.Length);
                }

                // Append to our chat and clear input
                string line = $"Me: {text}";
                if (!chatHistory.ContainsKey(currentChatUser))
                    chatHistory[currentChatUser] = new List<string>();

                chatHistory[currentChatUser].Add(line);
                txtChatHistory.AppendText(line + Environment.NewLine);
                txtMessageInput.Clear();
                txtMessageInput.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Send failed: " + ex.Message,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                // The user may have gone offline, check if we should remove them
                RemoveUserIfUnreachable(currentChatUser);
            }
        }

        private void RemoveUserIfUnreachable(string username)
        {
            try
            {
                if (!discoveredUsers.TryGetValue(username, out UserInfo? userInfo))
                    return;

                // Try to ping the user's TCP endpoint
                using (var client = new TcpClient())
                {
                    var result = client.BeginConnect(userInfo.Endpoint.Address, userInfo.Endpoint.Port, null, null);
                    bool success = result.AsyncWaitHandle.WaitOne(1000); // Short timeout

                    if (!success)
                    {
                        // User is unreachable, remove them
                        discoveredUsers.Remove(username);
                        RemoveUserButton(username);
                    }
                }
            }
            catch
            {
                // If any exception occurs during this test, assume user is offline
                discoveredUsers.Remove(username);
                RemoveUserButton(username);
            }
        }

        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            // Signal threads to stop
            isRunning = false;
            userTimeoutTimer.Stop();

            // Allow some time for threads to notice
            Thread.Sleep(100);

            // Close resources
            try
            {
                udpListener?.Close();
                udpBroadcaster?.Close();
                tcpListener?.Stop();
            }
            catch
            {
                // Suppress errors during shutdown
            }
        }

        private void txtMessageInput_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                e.Handled = true; // Prevent the beep sound
                btnSend_Click(sender, e);
            }
        }

        private void txtMessageInput_TextChanged(object sender, EventArgs e)
        {

        }
    }
}