using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

namespace Itim.TRS.InstallerLib.Configuration
{

    public class SqlScriptCollection : ConfigurationElementCollection
    {
        public override ConfigurationElementCollectionType CollectionType
        {
            get
            {
                return ConfigurationElementCollectionType.AddRemoveClearMap;
            }
        }

        protected override ConfigurationElement CreateNewElement()
        {
            return new SqlScriptConfigElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((SqlScriptConfigElement)element).File;
        }
    }
}
