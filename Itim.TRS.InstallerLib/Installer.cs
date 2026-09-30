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
										   IISFiles = GetItemsFromElement(fix, "IISFiles", "file")

									   }).ToList()[0];

				if (_patchConfigSection == null)
					throw new ConfigurationErrorsException(Resources.ERR_PREREQ_CONFIG);

				_patchConfigSection.IISSites = ResolveIISSites(_patchConfigSection.IISFiles);
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
		/// Reads each file listed in IISFiles (from the package's IIS\ folder) and returns the combined
		/// list of IIS site definitions. Files that don't exist are skipped here; RunPreChecks is
		/// responsible for validating that declared files are actually present.
		/// </summary>
		private List<IISSiteConfigElement> ResolveIISSites(List<string> iisFiles)
		{
			List<IISSiteConfigElement> sites = new List<IISSiteConfigElement>();

			if (iisFiles == null || iisFiles.Count == 0)
				return sites;

			string iisFolder = Path.Combine(this._sourceDir, "IIS");

			foreach (string fileName in iisFiles)
			{
				string filePath = Path.Combine(iisFolder, fileName);
				if (File.Exists(filePath))
					sites.AddRange(ParseIISSiteFile(filePath));
			}

			return sites;
		}

		/// <summary>
		/// Parses a simple "Key=Value" text file into IIS site definitions. Blank lines separate one
		/// site block from the next; lines starting with '#' are comments.
		/// </summary>
		private static List<IISSiteConfigElement> ParseIISSiteFile(string filePath)
		{
			List<IISSiteConfigElement> sites = new List<IISSiteConfigElement>();
			Dictionary<string, string> current = null;
			string[] lines = File.ReadAllLines(filePath);

			for (int i = 0; i <= lines.Length; i++)
			{
				string line = (i < lines.Length) ? lines[i].Trim() : string.Empty;
				bool isEndOfFile = (i == lines.Length);

				if (line.Length == 0 || isEndOfFile)
				{
					if (current != null && current.Count > 0)
					{
						IISSiteConfigElement site = new IISSiteConfigElement();
						site.WebsiteName = GetDictionaryValue(current, "WebsiteName");
						site.PhysicalPath = GetDictionaryValue(current, "PhysicalPath");
						site.Pool = GetDictionaryValue(current, "Pool");
						site.PoolFramework = GetDictionaryValue(current, "PoolFramework");
						site.PoolPipelineMode = GetDictionaryValue(current, "PoolPipelineMode");
						site.BindingProtocol = GetDictionaryValue(current, "BindingProtocol");
						site.BindingInfo = GetDictionaryValue(current, "BindingInfo");
						sites.Add(site);
					}
					current = null;
					continue;
				}

				if (line.StartsWith("#"))
					continue;

				int separatorIndex = line.IndexOf('=');
				if (separatorIndex <= 0)
					continue;

				if (current == null)
					current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

				current[line.Substring(0, separatorIndex).Trim()] = line.Substring(separatorIndex + 1).Trim();
			}

			return sites;
		}

		private static string GetDictionaryValue(Dictionary<string, string> dictionary, string key)
		{
			string value;
			return dictionary.TryGetValue(key, out value) ? value : null;
		}
		#endregion
	}
}
