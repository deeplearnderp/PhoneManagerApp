using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace PhoneManagerApp
{
    /// <summary>
    /// Handles live screen mirroring using ADB and FFplay.
    /// Streams video from the Android device via "adb exec-out screenrecord"
    /// and pipes it into FFplay for display.
    /// </summary>
    public class ScreenMirrorService
    {
        private Process adbProcess;
        private Process ffplayProcess;

        private readonly string adbPath = "adb.exe";
        private readonly string ffplayPath = "ffplay.exe";

        public bool IsRunning => ffplayProcess != null && !ffplayProcess.HasExited;

        /// <summary>
        /// Starts screen mirroring asynchronously.
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
                adbProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = adbPath,
                        Arguments = "exec-out screenrecord --output-format=h264 -",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                adbProcess.Start();

                // ================================
                // 🔹 Start FFplay to render stream
                // ================================
                ffplayProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffplayPath,
                        // ✅ Working arguments for raw H.264 stream
                        Arguments = "-f h264 -framerate 30 -window_title AndroidScreen -x 640 -y 360 -i -",
                        RedirectStandardInput = true,
                        UseShellExecute = false,
                        CreateNoWindow = false // Set to true if you want it hidden
                    }
                };
                ffplayProcess.Start();

                // ================================
                // 🔹 Pipe ADB stream to FFplay
                // ================================
                await Task.Run(async () =>
                {
                    try
                    {
                        await adbProcess.StandardOutput.BaseStream.CopyToAsync(ffplayProcess.StandardInput.BaseStream);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Stream error: {ex.Message}");
                    }
                    finally
                    {
                        try { ffplayProcess.StandardInput.Close(); } catch { }
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
        /// Stops screen mirroring and kills any active processes.
        /// </summary>
        public void Stop()
        {
            try
            {
                adbProcess?.Kill();
                adbProcess?.Dispose();
            }
            catch { }

            try
            {
                ffplayProcess?.Kill();
                ffplayProcess?.Dispose();
            }
            catch { }

            adbProcess = null;
            ffplayProcess = null;

            Console.WriteLine("🛑 Screen mirror stopped.");
        }
    }
}
