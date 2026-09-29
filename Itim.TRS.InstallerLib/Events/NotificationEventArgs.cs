using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Itim.TRS.InstallerLib;

namespace Itim.TRS.InstallerLib
{
   

    public class NotificationEventArgs:EventArgs
    {

        public NotificationEventArgs(NotificationType type,string message)
        {
            Type = type;
            Message = message;
        }

        
        public NotificationEventArgs(NotificationType type, string message,Exception error):this(type,message)
        {
            ErrorDetails = error;
        }

        
        public NotificationType Type {get;set;}
        public string Message {get;set;}        
        public Exception ErrorDetails { get; set; } 

    }
}
