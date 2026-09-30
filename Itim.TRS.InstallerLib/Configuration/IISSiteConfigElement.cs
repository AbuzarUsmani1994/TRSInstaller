using System;

namespace Itim.TRS.InstallerLib.Configuration
{
    public class IISSiteConfigElement
    {
        public string WebsiteName { get; set; }
        public string PhysicalPath { get; set; }
        public string Pool { get; set; }
        public string PoolFramework { get; set; }
        public string PoolPipelineMode { get; set; }
        public string BindingProtocol { get; set; }
        public string BindingInfo { get; set; }
    }
}
