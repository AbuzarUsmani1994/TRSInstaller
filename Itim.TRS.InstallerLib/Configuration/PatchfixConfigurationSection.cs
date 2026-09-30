using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

namespace Itim.TRS.InstallerLib.Configuration
{
    public class PatchConfigurationSection
    {
        public string Type { get; set; }

        [RegexStringValidator(@"(\d+\.){3,4}\d+")]
        public string Version { get; set; }

        [StringValidator(MaxLength = 256)]
        public string Description { get; set; }

        [RegexStringValidator(@"\d{4}-\d{1,2}-\d{1,2}")]
        public string ReleaseDate { get; set; }

        public List<string> PreRequisites { get; set; }

        public List<string> CompatibleReleases { get; set; }

        public List<string> SupportedClients { get; set; }

        public List<string> SqlScripts { get; set; }

        public List<string> AppArtifacts { get; set; }
        public List<string> WebArtifacts { get; set; }

        /// <summary>
        /// IIS Application definitions (virtual application + app pool assignment under an existing
        /// Site), declared directly in fix.manifest under &lt;IISApplications&gt;.
        /// </summary>
        public List<IISApplicationConfigElement> IISApplications { get; set; }
    }
    
}
