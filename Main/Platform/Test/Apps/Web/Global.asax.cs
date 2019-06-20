//-----------------------------------------------------------------------------
// FILE:        Global.asax.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: 

using System;
using System.Collections;
using System.Configuration;
using System.Data;
using System.Reflection;
using System.Web;
using System.Web.Security;
using System.Web.SessionState;

using LillTek.Common;
using LillTek.Datacenter;
using LillTek.Messaging;

namespace LillTek.Test.Web
{
    public class Global : System.Web.HttpApplication
    {
        private static LeafRouter router = null;

        protected void Application_Start(object sender, EventArgs args)
        {
            LillTek.Web.WebHelper.PlatformInitialize(Assembly.GetExecutingAssembly());
            NetTrace.Start();

            //router = new LeafRouter();
            //router.Start();
        }

        protected void Application_End(object sender, EventArgs args)
        {
            //router.Stop();
            LillTek.Web.WebHelper.PlatformTerminate();
        }

        public static MsgRouter Router
        {
            get { return router; }
        }
    }
}