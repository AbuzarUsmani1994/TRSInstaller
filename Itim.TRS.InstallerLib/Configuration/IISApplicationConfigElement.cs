using System;

namespace Itim.TRS.InstallerLib.Configuration
{
    /// <summary>
    /// One &lt;Application&gt; entry from fix.manifest's &lt;IISApplications&gt; block - an IIS virtual
    /// application (and optional app pool) attached under an already-existing Site.
    /// </summary>
    public class IISApplicationConfigElement
    {
        public string SiteName { get; set; }
        public string Path { get; set; }
        public string PhysicalPath { get; set; }
        public string ApplicationPool { get; set; }
        public string PoolFramework { get; set; }
        public string PoolPipelineMode { get; set; }
    }
}
