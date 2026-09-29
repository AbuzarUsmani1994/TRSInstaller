using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

namespace Itim.TRS.InstallerLib.Configuration
{
    public class SqlScriptConfigElement:ConfigurationElement
    {
        public SqlScriptConfigElement()
        {

        }

        public SqlScriptConfigElement(string file)
        {
            File = file;
        }
        //default value is there just to avoid the regex from failing. The .NET code checks the regex on the default value
        //first
        [ConfigurationProperty("file", IsRequired=true, IsKey=true,DefaultValue="TRSPatch.sql")]        
        [RegexStringValidator(@".+\.sql")]
        public string File
        {
            get
            {
                return (string)this["file"];
            }
            set
            {
                this["file"] = value;
            }
        }        
    }
}
