using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;

namespace FixInstaller
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
		private static readonly log4net.ILog _log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

		protected override void OnStartup(StartupEventArgs e)
		{
			log4net.Config.XmlConfigurator.Configure();
			base.OnStartup(e);
		}

        void AppStartup(object sender, StartupEventArgs e)
        {
            string fileName = String.Empty;

			if (e.Args.Length > 0)
				fileName = e.Args[0];
			else
			{
				MessageBox.Show("Cannot find fix file", "TRS Fix Installer", MessageBoxButton.OK, MessageBoxImage.Error);
				this.Shutdown();
				return;
			}

            // Create a window
			MainWindow window = new MainWindow(fileName, _log);

            // Open a window
            window.Show();
        }
    }
}
