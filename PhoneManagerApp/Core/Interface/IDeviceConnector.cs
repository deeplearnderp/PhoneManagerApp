// Version 1.0 - Core interface for modular device connectors

namespace PhoneManagerApp.Core.Interface;

public interface IDeviceConnector
{
    string Name { get; }
    bool IsConnected { get; }

    Task<bool> ConnectAsync();
    Task<IEnumerable<string>> GetFilesAsync(string? path = null);
}