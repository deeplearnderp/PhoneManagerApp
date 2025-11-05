// Version 1.0 - Basic WinForms UI with AndroidConnector integration
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using PhoneManagerApp.Core;

namespace PhoneManagerApp
{
    public partial class Form1 : Form
    {
        private Button btnConnect;
        private TreeView treeFiles;
        private Label lblStatus;
        private IDeviceConnector? connector;

        public Form1()
        {
            InitializeComponent();
            InitializeUI();
        }

        private void InitializeUI()
        {
            // ===== Button =====
            btnConnect = new Button();
            btnConnect.Text = "Connect Phone";
            btnConnect.Location = new Point(10, 10);
            btnConnect.Size = new Size(150, 30);
            btnConnect.Click += BtnConnect_Click;
            Controls.Add(btnConnect);

            // ===== TreeView =====
            treeFiles = new TreeView();
            treeFiles.Location = new Point(10, 50);
            treeFiles.Size = new Size(760, 400);
            treeFiles.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(treeFiles);

            // ===== Label =====
            lblStatus = new Label();
            lblStatus.Text = "Status: Disconnected";
            lblStatus.Location = new Point(10, 460);
            lblStatus.AutoSize = true;
            Controls.Add(lblStatus);

            // ===== Form Settings =====
            Text = "Phone File Explorer";
            Size = new Size(800, 550);
        }

        private async void BtnConnect_Click(object sender, EventArgs e)
        {
            lblStatus.Text = "Connecting...";
            connector = new AndroidConnector();
            bool connected = await connector.ConnectAsync();

            if (connected)
            {
                lblStatus.Text = "Connected to Android device!";
                var files = await connector.GetFilesAsync();

                treeFiles.Nodes.Clear();
                foreach (var f in files)
                    treeFiles.Nodes.Add(new TreeNode(f));
            }
            else
            {
                lblStatus.Text = "No device found.";
                MessageBox.Show("Could not detect a connected Android device.", "Connection Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
