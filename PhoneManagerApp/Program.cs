using PhoneManagerApp.UI;

namespace PhoneManagerApp;

internal static class Program
{
    /// <summary>
    ///     The main entry point for the application.
    /// </summary>
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        // 🚀 Launch the new modular main window
        Application.Run(new MainWindow());
    }
}