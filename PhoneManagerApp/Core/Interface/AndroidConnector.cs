// Version 1.1 - Android device connector using WindowsAPICodePack
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.WindowsAPICodePack.Shell;
using Microsoft.WindowsAPICodePack.Shell.PropertySystem;

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

                    // Iterate through connected devices under "This PC"
                    foreach (var item in myComputer)
                    {
                        if (!item.ParsingName.Contains("::{") &&
                            (item.Name.Contains("Android", StringComparison.OrdinalIgnoreCase) ||
                             item.Name.Contains("Galaxy", StringComparison.OrdinalIgnoreCase) ||
                             item.Name.Contains("Pixel", StringComparison.OrdinalIgnoreCase) ||
                             item.Name.Contains("Phone", StringComparison.OrdinalIgnoreCase)))
                        {
                            _device = item;
                            IsConnected = true;
                            Debug.WriteLine($"Connected to: {item.Name}");
                            return true;
                        }
                    }

                    Debug.WriteLine("No Android device found.");
                    return false;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error connecting to device: {ex.Message}");
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

                    // Enumerate top-level directories and files
                    foreach (var item in (ShellFolder)_device)
                    {
                        results.Add(item.Name);
                    }
                }
                catch (Exception ex)
                {
                    results.Add($"Error reading device: {ex.Message}");
                }

                return results;
            });
        }
    }
}
