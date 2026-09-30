using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Web.Configuration;
using Itim.TRS.InstallerLib.Configuration;
using Itim.TRS.InstallerLib.Data;
using Itim.TRS.InstallerLib;
using Itim.TRS.InstallerLib.Properties;
using log4net;
using System.Text;
using Itim.TRS.InstallerLib.Events;
using ICSharpCode.SharpZipLib.Zip;
using System.Xml.Linq;

namespace Itim.TRS.InstallerLib
{
	public class Installer
	{
		private InstallerBase _fixInstaller;
		private PatchConfigurationSection _patchConfigSection;
		private string _sourceDir;
		private string _destDir;
		public static string _features;

		public event EventHandler<ProgressEventArgs> OnProgressChanged;
		public event EventHandler<NotificationEventArgs> OnNotify;
		public event EventHandler<UserDrivenNotificationEventArgs> OnUserDrivenNotify;
		
		public Installer(string sourceDir, string destDir)
		{
			this._sourceDir = sourceDir;
			this._destDir = destDir;

			InstallerBase.LogEvent("Starting reading manifest file");
			ReadConfiguration();

			InstallerBase.LogEvent("Creating fix object");
			switch (_patchConfigSection.Type.ToLower())
			{
				case "patch":
					_fixInstaller = new PatchInstaller(this._sourceDir, this._destDir);
					break;
				case "hotfix":
					_fixInstaller = new HotfixInstaller(this._sourceDir, this._destDir);
					break;
				default:
					break;
			}

			if (_fixInstaller == null)
				throw new Exception("Installation type not found. Setup manifest is corrupt.");

			_fixInstaller.LogEvent(NotificationType.Info, Resources.INF_READ_CONFIG);

			_fixInstaller.PatchConfig = _patchConfigSection;

			_fixInstaller.OnProgressChanged += new EventHandler<ProgressEventArgs>(_fixInstaller_OnProgressChanged);
			_fixInstaller.OnNotify += new EventHandler<NotificationEventArgs>(_fixInstaller_OnNotify);
			_fixInstaller.OnUserDrivenNotify += new EventHandler<UserDrivenNotificationEventArgs>(_fixInstaller_OnUserDrivenNotify);

			InstallerBase.LogEvent("Installer object created");
		}

		void _fixInstaller_OnUserDrivenNotify(object sender, UserDrivenNotificationEventArgs e)
		{
			if (this.OnUserDrivenNotify != null)
				OnUserDrivenNotify(sender, e);
		}

		void _fixInstaller_OnNotify(object sender, NotificationEventArgs e)
		{
			if (this.OnNotify != null)
				OnNotify(sender, e);
		}

		void _fixInstaller_OnProgressChanged(object sender, ProgressEventArgs e)
		{
			if (this.OnProgressChanged != null)
				OnProgressChanged(sender, e);
		}

		#region Public Methods
		public List<Server> GetTRSServers()
		{
			return DBManager.GetTRSServers();
		}

		public bool RunPreChecks()
		{
			return _fixInstaller.RunPreChecks();
		}

		public void ApplyPatch()
		{
			_fixInstaller.ApplyPatch();
		}

		public void ApplyPatchOnDB()
		{
			_fixInstaller.ApplyPatchOnDB();
		}
		#endregion

		#region Private Methods
		private void ReadConfiguration()
		{
			try
			{
				XElement manifest = XElement.Load(Path.Combine(this._sourceDir, "fix.manifest"));

				_patchConfigSection = (from fix in manifest.Descendants("Fix")
									   select new PatchConfigurationSection
									   {
										   Type = fix.Attribute("type").Value,
										   Version = fix.Attribute("version").Value,
										   Description = fix.Attribute("description").Value,
										   ReleaseDate = fix.Attribute("releaseDate").Value,
										   SqlScripts = GetItemsFromElement(fix, "SqlScripts", "file"),
                                           AppArtifacts = GetItemsFromElement(fix, "AppFiles", "file"),
                                           WebArtifacts = GetItemsFromElement(fix, "WebFiles", "file"),
                                           PreRequisites = GetItemsFromElement(fix, "PreRequisites", "version"),
										   CompatibleReleases = GetItemsFromElement(fix, "CompatibleReleases", "version"),
										   SupportedClients = GetItemsFromElement(fix, "SupportedClients", "name"),
										   IISApplications = GetIISApplicationsFromElement(fix, "IISApplications")

									   }).ToList()[0];

				if (_patchConfigSection == null)
					throw new ConfigurationErrorsException(Resources.ERR_PREREQ_CONFIG);
			}
			catch (Exception ex)
			{
				if (ex.InnerException != null)
					throw ex.InnerException;
				throw ex;
			}

		}

		private static List<string> GetItemsFromElement(XElement fix, string elementName, string attributeName)
		{
			return fix.Element(elementName) != null ? (from script in fix.Element(elementName).Descendants("add")
													   select script.Attribute(attributeName).Value).ToList() : new List<string>();
		}

		/// <summary>
		/// Reads the &lt;IISApplications&gt;&lt;Application .../&gt;...&lt;/IISApplications&gt; block
		/// directly from fix.manifest - the AutoHotfixGenerator build tool is responsible for
		/// translating the package's Web\IIS\ descriptor file(s) into this XML shape ahead of time.
		/// </summary>
		private static List<IISApplicationConfigElement> GetIISApplicationsFromElement(XElement fix, string elementName)
		{
			List<IISApplicationConfigElement> applications = new List<IISApplicationConfigElement>();
			XElement container = fix.Element(elementName);

			if (container == null)
				return applications;

			foreach (XElement app in container.Elements("Application"))
			{
				IISApplicationConfigElement application = new IISApplicationConfigElement();
				application.SiteName = GetAttributeValue(app, "siteName");
				application.Path = GetAttributeValue(app, "path");
				application.PhysicalPath = GetAttributeValue(app, "physicalPath");
				application.ApplicationPool = GetAttributeValue(app, "applicationPool");
				application.PoolFramework = GetAttributeValue(app, "poolFramework");
				application.PoolPipelineMode = GetAttributeValue(app, "poolPipelineMode");
				applications.Add(application);
			}

			return applications;
		}

		private static string GetAttributeValue(XElement element, string attributeName)
		{
			XAttribute attribute = element.Attribute(attributeName);
			return attribute != null ? attribute.Value : null;
		}
		#endregion
	}
}
