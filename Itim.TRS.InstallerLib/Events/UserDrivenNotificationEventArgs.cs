using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Itim.TRS.InstallerLib;

namespace Itim.TRS.InstallerLib.Events
{
    public class UserDrivenNotificationEventArgs:NotificationEventArgs
    {
        public UserDrivenNotificationEventArgs(NotificationType type, string message)
            : base(type, message)
        {
            
        }

        public bool CanContinue { get; set; }
    }
}
