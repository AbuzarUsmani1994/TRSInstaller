using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Itim.TRS.InstallerLib
{
    public class ProgressEventArgs: EventArgs
    {        
        public ProgressEventArgs(int incrementAmount,string description)
        {
            IncrementAmount = incrementAmount;
            Description = description;
        }

        public int IncrementAmount { get; set; }
        public string Description { get; set; }
    }
}
