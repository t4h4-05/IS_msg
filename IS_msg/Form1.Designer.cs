namespace IS_msg
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            panelUsers = new Panel();
            panelChatArea = new Panel();
            panelInput = new Panel();
            btnSend = new Button();
            txtMessageInput = new TextBox();
            txtChatHistory = new TextBox();
            panelChatArea.SuspendLayout();
            panelInput.SuspendLayout();
            SuspendLayout();
            // 
            // panelUsers
            // 
            panelUsers.Dock = DockStyle.Left;
            panelUsers.Location = new Point(0, 0);
            panelUsers.Name = "panelUsers";
            panelUsers.Size = new Size(200, 450);
            panelUsers.TabIndex = 0;
            // 
            // panelChatArea
            // 
            panelChatArea.Controls.Add(panelInput);
            panelChatArea.Controls.Add(txtChatHistory);
            panelChatArea.Dock = DockStyle.Fill;
            panelChatArea.Location = new Point(200, 0);
            panelChatArea.Name = "panelChatArea";
            panelChatArea.Size = new Size(600, 450);
            panelChatArea.TabIndex = 1;
            // 
            // panelInput
            // 
            panelInput.Controls.Add(btnSend);
            panelInput.Controls.Add(txtMessageInput);
            panelInput.Dock = DockStyle.Bottom;
            panelInput.Location = new Point(0, 410);
            panelInput.Name = "panelInput";
            panelInput.Size = new Size(600, 40);
            panelInput.TabIndex = 1;
            // 
            // btnSend
            // 
            btnSend.Dock = DockStyle.Right;
            btnSend.Location = new Point(530, 0);
            btnSend.Name = "btnSend";
            btnSend.Size = new Size(70, 40);
            btnSend.TabIndex = 1;
            btnSend.Text = "Send";
            btnSend.UseVisualStyleBackColor = true;
            btnSend.Click += btnSend_Click;
            // 
            // txtMessageInput
            // 
            txtMessageInput.Dock = DockStyle.Left;
            txtMessageInput.Font = new Font("Segoe UI", 10F);
            txtMessageInput.Location = new Point(0, 0);
            txtMessageInput.Name = "txtMessageInput";
            txtMessageInput.Size = new Size(524, 25);
            txtMessageInput.TabIndex = 0;
            // 
            // txtChatHistory
            // 
            txtChatHistory.Dock = DockStyle.Top;
            txtChatHistory.Location = new Point(0, 0);
            txtChatHistory.Multiline = true;
            txtChatHistory.Name = "txtChatHistory";
            txtChatHistory.ReadOnly = true;
            txtChatHistory.ScrollBars = ScrollBars.Both;
            txtChatHistory.Size = new Size(600, 404);
            txtChatHistory.TabIndex = 0;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(panelChatArea);
            Controls.Add(panelUsers);
            Name = "Form1";
            Text = "Form1";
            FormClosing += Form1_FormClosing;
            Load += Form1_Load;
            panelChatArea.ResumeLayout(false);
            panelChatArea.PerformLayout();
            panelInput.ResumeLayout(false);
            panelInput.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private Panel panelUsers;
        private Panel panelChatArea;
        private Panel panelInput;
        private TextBox txtChatHistory;
        private Button btnSend;
        private TextBox txtMessageInput;
    }
}
