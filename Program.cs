using System;
using System.Windows.Forms;
using System.IO;

namespace ytd2large
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] arguments)
        {
            if (arguments == null)
            {
                Console.WriteLine("No arguments passed, continuing with execution as usual.");

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new Main(false));
            }
            else
            {
                Console.WriteLine("Arguments passed, running it in debug mode...");

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new Main(true));
            }
        }
    }
}
