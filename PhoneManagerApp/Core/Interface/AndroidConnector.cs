// Version 1.2 - Improved Android device detection and debug logging
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.WindowsAPICodePack.Shell;
using Microsoft.WindowsAPICodePack.Shell.PropertySystem;
using PhoneManagerApp.Core.Interface;

namespace PhoneManagerApp.Core
{
    public class AndroidConnector : IDeviceConnector
    {
        public string Name => "Android Device";
        public bool IsConnected { get; private set; }

        private ShellObject? _device;

        public async Task<bool> ConnectAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Access the root "This PC" shell namespace
                    var myComputer = (ShellFolder)KnownFolders.Computer;

                    Debug.WriteLine("=== Scanning for connected devices under 'This PC' ===");

                    // Iterate through devices under "This PC"
                    foreach (var item in myComputer)
                    {
                        Debug.WriteLine($"Found: {item.Name}");

                        // Android phones typically show up here via MTP mode
                        // We match common names (Android, Galaxy, Pixel, Phone) 
                        // plus user-specific or model identifiers (S21, Cory, etc.)
                        if (!item.ParsingName.Contains("::{") &&
                            (item.Name.Contains("Android", StringComparison.OrdinalIgnoreCase) ||
                             item.Name.Contains("Galaxy", StringComparison.OrdinalIgnoreCase) ||
                             item.Name.Contains("Pixel", StringComparison.OrdinalIgnoreCase) ||
                             item.Name.Contains("Phone", StringComparison.OrdinalIgnoreCase) ||
                             item.Name.Contains("S21", StringComparison.OrdinalIgnoreCase) ||
                             item.Name.Contains("Cory", StringComparison.OrdinalIgnoreCase)))
                        {
                            _device = item;
                            IsConnected = true;
                            Debug.WriteLine($"✅ Connected to device: {item.Name}");
                            return true;
                        }
                    }

                    Debug.WriteLine("❌ No Android device found.");
                    return false;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"⚠️ Error connecting to device: {ex.Message}");
                    return false;
                }
            });
        }

        public async Task<IEnumerable<string>> GetFilesAsync(string? path = null)
        {
            return await Task.Run(() =>
            {
                var results = new List<string>();

                try
                {
                    if (!IsConnected || _device == null)
                    {
                        results.Add("No device connected.");
                        return results;
                    }

                    Debug.WriteLine($"📂 Listing top-level folders for {_device.Name}");

                    // List subfolders and files from the connected device
                    foreach (var item in (ShellFolder)_device)
                    {
                        Debug.WriteLine($"Found: {item.Name}");
                        results.Add(item.Name);
                    }
                }
                catch (Exception ex)
                {
                    results.Add($"Error reading device contents: {ex.Message}");
                    Debug.WriteLine($"⚠️ Error reading device contents: {ex.Message}");
                }

                return results;
            });
        }
    }
}
