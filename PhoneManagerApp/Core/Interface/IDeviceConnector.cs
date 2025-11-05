// Version 1.0 - Core interface for modular device connectors
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PhoneManagerApp.Core
{
    public interface IDeviceConnector
    {
        string Name { get; }
        bool IsConnected { get; }

        Task<bool> ConnectAsync();
        Task<IEnumerable<string>> GetFilesAsync(string? path = null);
    }
}