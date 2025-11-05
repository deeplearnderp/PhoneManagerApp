// Version 2.0 - Dual mode (MTP + ADB) Android Manager
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
        private Button btnToggleNotifications;
        private ComboBox modeSelector;
        private TreeView treeFiles;
        private Label lblStatus;

        private IDeviceConnector? connector;
        private AdbConnector? adbConnector;

        public Form1()
        {
            InitializeComponent();
            InitializeUI();
        }

        private void InitializeUI()
        {
            // ===== Mode Selector =====
            modeSelector = new ComboBox();
            modeSelector.Items.AddRange(new string[] { "File Explorer (MTP)", "ADB Control Mode" });
            modeSelector.SelectedIndex = 0;
            modeSelector.DropDownStyle = ComboBoxStyle.DropDownList;
            modeSelector.Location = new Point(10, 10);
            modeSelector.Size = new Size(200, 25);
            Controls.Add(modeSelector);

            // ===== Connect Button =====
            btnConnect = new Button();
            btnConnect.Text = "Connect Phone";
            btnConnect.Location = new Point(220, 10);
            btnConnect.Size = new Size(130, 25);
            btnConnect.Click += BtnConnect_Click;
            Controls.Add(btnConnect);

            // ===== Toggle Notifications Button =====
            btnToggleNotifications = new Button();
            btnToggleNotifications.Text = "Toggle Notifications";
            btnToggleNotifications.Location = new Point(360, 10);
            btnToggleNotifications.Size = new Size(150, 25);
            btnToggleNotifications.Enabled = false;
            btnToggleNotifications.Click += BtnToggleNotifications_Click;
            Controls.Add(btnToggleNotifications);

            // ===== TreeView =====
            treeFiles = new TreeView();
            treeFiles.Location = new Point(10, 50);
            treeFiles.Size = new Size(760, 400);
            treeFiles.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(treeFiles);

            // ===== Status Label =====
            lblStatus = new Label();
            lblStatus.Text = "Status: Disconnected";
            lblStatus.Location = new Point(10, 460);
            lblStatus.AutoSize = true;
            Controls.Add(lblStatus);

            // ===== Form Settings =====
            Text = "Phone Manager App";
            Size = new Size(800, 550);
        }

        private async void BtnConnect_Click(object sender, EventArgs e)
        {
            lblStatus.Text = "Connecting...";
            treeFiles.Nodes.Clear();
            btnToggleNotifications.Enabled = false;

            string mode = modeSelector.SelectedItem?.ToString() ?? "File Explorer (MTP)";

            if (mode.Contains("MTP"))
            {
                connector = new AndroidConnector();
                bool connected = await connector.ConnectAsync();

                if (connected)
                {
                    lblStatus.Text = "Connected (MTP Mode)";
                    var files = await connector.GetFilesAsync();

                    foreach (var f in files)
                        treeFiles.Nodes.Add(new TreeNode(f));
                }
                else
                {
                    lblStatus.Text = "No MTP device found.";
                    MessageBox.Show("Could not detect an MTP device.", "Connection Failed",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            else if (mode.Contains("ADB"))
            {
                adbConnector = new AdbConnector();
                bool connected = await adbConnector.ConnectAsync();

                if (connected)
                {
                    lblStatus.Text = "Connected (ADB Mode)";
                    btnToggleNotifications.Enabled = true;

                    var packages = await adbConnector.GetFilesAsync();
                    foreach (var p in packages)
                    {
                        if (!string.IsNullOrWhiteSpace(p))
                            treeFiles.Nodes.Add(new TreeNode(p.Trim()));
                    }
                }
                else
                {
                    lblStatus.Text = "No ADB device found.";
                    MessageBox.Show("Could not detect an ADB-enabled Android device.",
                        "Connection Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private async void BtnToggleNotifications_Click(object sender, EventArgs e)
        {
            if (adbConnector == null)
            {
                MessageBox.Show("No ADB device connected.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            bool success = await adbConnector.ToggleNotificationsAsync(false);
            if (success)
            {
                MessageBox.Show("Notifications turned OFF.", "Success",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                lblStatus.Text = "Notifications: OFF";
            }
            else
            {
                MessageBox.Show("Failed to change notification settings.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
