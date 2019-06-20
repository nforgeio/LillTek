//-----------------------------------------------------------------------------
// FILE:        Sitemap.aspx.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Generates a test sitemap.

using System;
using System.Collections;
using System.Configuration;
using System.Data;
using System.Web;
using System.Web.Security;
using System.Web.SessionState;

using LillTek.Common;
using LillTek.Web;

namespace LillTek.Test.Web
{
    /// <summary>
    /// Generates a test sitemap.
    /// </summary>
    public partial class Sitemap : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs args)
        {
            WebHelper.ReturnSeoSitemap();
        }
    }
}