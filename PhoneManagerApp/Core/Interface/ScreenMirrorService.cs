using System.Diagnostics;

namespace PhoneManagerApp;

/// <summary>
///     Handles live screen mirroring using ADB and FFplay.
///     Streams video from the Android device via "adb exec-out screenrecord"
///     and pipes it into FFplay for display.
/// </summary>
public class ScreenMirrorService
{
    private readonly string _adbPath = "adb.exe";
    private readonly string _ffplayPath = "ffplay.exe";
    private Process _adbProcess;
    private Process _ffplayProcess;

    public bool IsRunning => _ffplayProcess != null && !_ffplayProcess.HasExited;

    /// <summary>
    ///     Starts screen mirroring asynchronously.
    /// </summary>
    public async Task StartAsync()
    {
        if (IsRunning)
        {
            Stop();
            await Task.Delay(500);
        }

        try
        {
            // ================================
            // 🔹 Start ADB screenrecord stream
            // ================================
            _adbProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _adbPath,
                    Arguments = "exec-out screenrecord --output-format=h264 -",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            _adbProcess.Start();

            // ================================
            // 🔹 Start FFplay to render stream
            // ================================
            _ffplayProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ffplayPath,
                    // ✅ Working arguments for raw H.264 stream
                    Arguments = "-f h264 -framerate 30 -window_title AndroidScreen -x 640 -y 360 -i -",
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    CreateNoWindow = false // Set to true if you want it hidden
                }
            };
            _ffplayProcess.Start();

            // ================================
            // 🔹 Pipe ADB stream to FFplay
            // ================================
            await Task.Run(async () =>
            {
                try
                {
                    await _adbProcess.StandardOutput.BaseStream.CopyToAsync(_ffplayProcess.StandardInput.BaseStream);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Stream error: {ex.Message}");
                }
                finally
                {
                    try
                    {
                        _ffplayProcess.StandardInput.Close();
                    }
                    catch
                    {
                    }
                }
            });

            Console.WriteLine("📺 Screen mirror started successfully.");
        }
        catch (Exception ex)
        {
            Stop();
            Console.WriteLine($"❌ ScreenMirrorService error: {ex.Message}");
        }
    }

    /// <summary>
    ///     Stops screen mirroring and kills any active processes.
    /// </summary>
    public void Stop()
    {
        try
        {
            _adbProcess?.Kill();
            _adbProcess?.Dispose();
        }
        catch
        {
        }

        try
        {
            _ffplayProcess?.Kill();
            _ffplayProcess?.Dispose();
        }
        catch
        {
        }

        _adbProcess = null;
        _ffplayProcess = null;

        Console.WriteLine("🛑 Screen mirror stopped.");
    }
}