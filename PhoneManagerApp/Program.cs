using System;
using System.Windows.Forms;

namespace PhoneManagerApp
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            // 🚀 Launch the new modular main window
            Application.Run(new MainWindow());
        }
    }
}