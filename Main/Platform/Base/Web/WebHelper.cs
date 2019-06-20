//-----------------------------------------------------------------------------
// FILE:        WebHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements miscellaneous web related utility methods.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Text;
using System.Web;
using System.Web.Hosting;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;
using System.Xml;
using System.Xml.Linq;

using LillTek.Common;
using LillTek.Service;

namespace LillTek.Web
{
    /// <summary>
    /// Implements miscellaneous web related utility methods.
    /// </summary>
    public static class WebHelper
    {
        private static WebServiceHost serviceHost = null;     // The service host (or null)

        /// <summary>
        /// The name of the unique visitor cookie (<b>__lt-uv</b>).
        /// </summary>
        public const string UniqueVisitorCookie = "__lt-uv";

        /// <summary>
        /// Name used to retrieve the name to cookie dictionary from the <see cref="HttpContext" />'s
        /// <see cref="HttpContext.Items" /> collection.
        /// </summary>
        private const string CookieDictionaryItemName = "__lt-cookies";

        /// <summary>
        /// Exception message for methods that must be called within the context of an HTTP
        /// request, but aren't.
        /// </summary>
        internal const string NoHttpContextMsg = "The current thread is not processing a HTTP request.";

        /// <summary>
        /// Performs basic initialization of the LillTek platform for an ASP.NET
        /// application.
        /// </summary>
        /// <param name="entryAssembly">The assembly containing the application's entry point.</param>
        /// <remarks>
        /// <para>
        /// This method or one of the alternatives should be called within the web
        /// application's <b>Application_Start()</b> event handler in the <b>Global.asax</b>
        /// file.
        /// </para>
        /// <note>
        /// This version of the method passes the <b>HostingEnvironment.SiteName</b>
        /// to <see cref="Helper.InitializeWebApp" /> as the application name.
        /// </note>
        /// </remarks>
        public static void PlatformInitialize(Assembly entryAssembly)
        {
            if (Helper.IsInitialized)
                return;

            Helper.InitializeWebApp(entryAssembly, HostingEnvironment.ApplicationPhysicalPath, HostingEnvironment.SiteName);
            Config.SetConfigPath(HostingEnvironment.ApplicationPhysicalPath + "Web.ini");

            LoadWebSettings();
        }

        /// <summary>
        /// Performs the basic initialization of the LillTek platform for an ASP.NET
        /// application and then instantiates a service host for the service instance passed
        /// and starts the service.
        /// </summary>
        /// <param name="entryAssembly">The assembly containing the application's entry point.</param>
        /// <param name="service">The service instance.</param>
        /// <remarks>
        /// <para>
        /// This method or one of the alternatives should be called within the web
        /// application's <b>Application_Start()</b> event handler in the <b>Global.asax</b>
        /// file.
        /// </para>
        /// <note>
        /// This version of the method passes the string returned by the
        /// service instance's <see cref="IService.Name" /> property 
        /// to <see cref="Helper.InitializeWebApp" /> as the application name.
        /// </note>
        /// </remarks>
        public static void PlatformInitialize(Assembly entryAssembly, IService service)
        {
            if (Helper.IsInitialized)
                return;

            string      appPath;
            string      binPath;

            if (HostingEnvironment.ApplicationHost == null)
            {
                // $hack: 
                //
                // Azure doesn't appear to have initialized the hosting environment
                // at the time it calls the roles RoleEntryPoint.OnStart() method.
                // I'm going to hack the determination of the root path and set
                // a generic application name.  The current directory is set to the
                // BIN folder beneath the application root.  So, we'll just strip
                // off the BIN folder.

                binPath = Environment.CurrentDirectory;
                appPath = Path.GetFullPath(Path.Combine(binPath, ".."));
                Helper.InitializeWebApp(entryAssembly, appPath, "Web Site");

                // $hack:
                //
                // Starting with the Azure 1.8 SDK, it appears that the Azure Tools
                // no longer copy the web site content files to [approot] folder when
                // running web roles in the emulator.  This is a problem when we need
                // to load INI files within OnStart().
                //
                // To hack around this, web roles will need to mark all INI files as
                // [Copy if newer) which will copy the files to the [approot\bin] folder.
                // Then, the code below will copy any INI files from the [bin] folder that
                // are not already present to [approot].

                try
                {
                    foreach (var iniPath in Directory.GetFiles(binPath, "*.ini", SearchOption.TopDirectoryOnly))
                    {
                        var fileName = Path.GetFileName(iniPath);

                        if (!File.Exists(Path.Combine(appPath, fileName)))
                            File.Copy(iniPath, Path.Combine(appPath, fileName));
                    }
                }
                catch (Exception e)
                {

                    SysLog.LogException(e);
                }
            }
            else
            {
                appPath = HostingEnvironment.ApplicationPhysicalPath;
                Helper.InitializeWebApp(entryAssembly, appPath, HostingEnvironment.SiteName);
            }

            Config.SetConfigPath(Path.Combine(appPath, "Web.ini"));
        }

        /// <summary>
        /// Loads <see cref="WebSettings" /> from the application configuration and performs any
        /// related initialization.
        /// </summary>
        private static void LoadWebSettings()
        {
            WebSettings.Load();

            try
            {
                // Make sure that the temporary web folder exists and is empty.

                Helper.CreateFolderTree(WebSettings.WebTempFolder);
                Helper.DeleteFile(Path.Combine(WebSettings.WebTempFolder, "*.*"), true);
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }
        }

        /// <summary>
        /// Performs any necessary termination housekeeping for the LillTek Platform
        /// before a web application stops.
        /// </summary>
        /// <remarks>
        /// This method should be called within the application's <b>Application_End()</b>
        /// event handler.
        /// </remarks>
        public static void PlatformTerminate()
        {
            if (serviceHost != null)
            {
                serviceHost.Service.Stop();
                serviceHost = null;
            }

            try
            {
                // Empty the temporary web folder.

                Helper.DeleteFile(Path.Combine(WebSettings.WebTempFolder, "*.*"), true);
            }
            catch
            {
                // Ignoring errors
            }
        }

        /// <summary>
        /// Returns the website's physical root path.
        /// </summary>
        public static string RootPath
        {
            get { return HostingEnvironment.ApplicationPhysicalPath; }
        }

        /// <summary>
        /// Sets the <b>Content-Dispositon</b> header of the current HTTP response,
        /// performing any necessary escaping.
        /// </summary>
        /// <param name="fileName">The suggested file name.</param>
        /// <exception cref="InvalidOperationException">Thrown if the method is not called within the processing context of a HTTP request.</exception>
        /// <remarks>
        /// <para>
        /// This method will cause the client browser to prompt the user to save the response
        /// data to the local file system, rather than attempting to display response within
        /// the browser.  The <paramref name="fileName" /> will be suggested to the user as
        /// the default.
        /// </para>
        /// </remarks>
        public static void SetResponseContentDisposition(string fileName)
        {
            var context = HttpContext.Current;

            if (context == null)
                throw new InvalidOperationException(NoHttpContextMsg);

            context.Response.Headers["Content-Disposition"] = string.Format("attachment; filename=\"{0}\"", fileName);
        }

        /// <summary>
        /// Returns the calling client's IP address for the current HTTP request.
        /// </summary>
        /// <returns>The IP address as a string or an empty string if the address could not be determined.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the method is not called within the processing context of a HTTP request.</exception>
        /// <remarks>
        /// The client address is determined by first looking at the <b>X-Forwarded-For</b> header and
        /// returning the most recent IP address in the list.  Then, if no address was found, the 
        /// actual IP address of the calling client will be returned.
        /// </remarks>
        public static string GetClientAddress()
        {
            var context = HttpContext.Current;

            if (context == null)
                throw new InvalidOperationException(NoHttpContextMsg);

            var request      = context.Request;
            var forwardedFor = request.ServerVariables["HTTP_X_FORWARDED_FOR"];
            var remoteAddr   = request.ServerVariables["REMOTE_ADDR"];
            var address      = !string.IsNullOrEmpty(forwardedFor) ? forwardedFor : remoteAddr;

            return address ?? string.Empty;
        }

        /// <summary>
        /// Returns the cached cookie collection for the current HTTP request, creating one
        /// if necessary.
        /// </summary>
        /// <returns>The cookie collection.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the method is not called within the processing context of a HTTP request.</exception>
        private static Dictionary<string, HttpCookie> GetCookieCollection()
        {
            HttpContext                     context = HttpContext.Current;
            Dictionary<string, HttpCookie>  cookies;

            if (context == null)
                throw new InvalidOperationException(NoHttpContextMsg);

            cookies = HttpContext.Current.Items[CookieDictionaryItemName] as Dictionary<string, HttpCookie>;
            if (cookies == null)
            {
                cookies = new Dictionary<string, HttpCookie>();
                context.Items[CookieDictionaryItemName] = cookies;

                foreach (var key in context.Request.Cookies.AllKeys)
                    cookies[key] = context.Request.Cookies.Get(key);
            }

            return cookies;
        }

        /// <summary>
        /// Saves a cookie in the current <see cref="HttpContext" />'s <see cref="HttpResponse" />,
        /// overwriting any existing cookie.
        /// </summary>
        /// <param name="cookie">The cookie to be set.</param>
        /// <exception cref="InvalidOperationException">Thrown if the method is not called within the processing context of a HTTP request.</exception>
        public static void SetCookie(HttpCookie cookie)
        {
            if (HttpContext.Current == null)
                throw new InvalidOperationException(NoHttpContextMsg);

            var cookies = GetCookieCollection();

            HttpContext.Current.Request.Cookies.Add(cookie);
            HttpContext.Current.Response.Cookies.Add(cookie);
            cookies[cookie.Name] = cookie;
        }

        /// <summary>
        /// Returns a named cookie from the current <see cref="HttpContext" />'s <see cref="HttpRequest" />.
        /// </summary>
        /// <param name="name">Name of the cookie.</param>
        /// <returns>The <see cref="HttpCookie" /> if one was found, <c>null</c> otherwise.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the method is not called within the processing context of a HTTP request.</exception>
        public static HttpCookie GetCookie(string name)
        {
            if (HttpContext.Current == null)
                throw new InvalidOperationException(NoHttpContextMsg);

            var         request = HttpContext.Current.Request;
            var         cookies = GetCookieCollection();
            HttpCookie  cookie;

            if (cookies.TryGetValue(name, out cookie))
                return cookie;
            else
                return null;
        }

        /// <summary>
        /// Instructs the browser to remove the named cookie, if it exists.
        /// </summary>
        /// <param name="name">Name of the cookie.</param>
        /// <exception cref="InvalidOperationException">Thrown if the method is not called within the processing context of a HTTP request.</exception>
        public static void RemoveCookie(string name)
        {
            if (HttpContext.Current == null)
                throw new InvalidOperationException(NoHttpContextMsg);

            var cookies = GetCookieCollection();

            if (!cookies.ContainsKey(name))
                return;

            // The way to instruct the browser to remove a cookie is by 
            // adding a cooking with the same name but with an expiration
            // date in the past.

            HttpCookie cookie;

            cookie = new HttpCookie(name, string.Empty);
            cookie.Expires = new DateTime(1970, 1, 1);

            HttpContext.Current.Request.Cookies.Add(cookie);
            HttpContext.Current.Response.Cookies.Add(cookie);

            cookies.Remove(name);
        }

        /// <summary>
        /// Returns the <see cref="UniqueVisitor" /> instance from the current 
        /// HTTP context for the end user making the request generating a new
        /// instance as necessary.
        /// </summary>
        /// <returns>The <see cref="UniqueVisitor" /> instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the method is not called within the processing context of a HTTP request.</exception>
        /// <remarks>
        /// <para>
        /// This method retrieves the visitor identifier from the <b>__lt-uv</b>
        /// cookie in the request and if necessary, adds newly generated cookies
        /// to the HTTP response.
        /// </para>
        /// <note>
        /// Unique visitor instances <b>will not</b> be created for known web crawlers.
        /// </note>
        /// </remarks>
        public static UniqueVisitor GetUniqueVisitor()
        {
            HttpCookie      cookie;
            UniqueVisitor   visitor;

            if (HttpContext.Current == null)
                throw new InvalidOperationException(NoHttpContextMsg);

            if (HttpContext.Current.Request.Browser.Crawler)
                return null;

            cookie = GetCookie(UniqueVisitorCookie);
            if (cookie == null)
            {
                // There is no visitor cookie so generate a new visitor ID
                // and send it back in the HTTP response with an approximate
                // 100 year lifetime (essentially forever).

                visitor = new UniqueVisitor();
                cookie = new HttpCookie(UniqueVisitorCookie, visitor.ToString());
                cookie.Expires = DateTime.UtcNow + TimeSpan.FromDays(365 * 100);

                SetCookie(cookie);
                return visitor;
            }

            try
            {
                return new UniqueVisitor(cookie.Value);
            }
            catch (Exception e)
            {
                SysLog.LogException(e, "Probable invalid HTTP visitor cookie.  Generating a new cookie.");

                visitor        = new UniqueVisitor();
                cookie         = new HttpCookie(UniqueVisitorCookie, visitor.ToString());
                cookie.Expires = DateTime.UtcNow + TimeSpan.FromDays(365 * 100);

                SetCookie(cookie);
                return visitor;
            }
        }

        /// <summary>
        /// Returns the <see cref="UniqueVisitor" /> ID if present, <c>null</c> otherwise.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the method is not called within the processing context of a HTTP request.</exception>
        /// <remarks>
        /// <note>
        /// Unique visitor instances <b>are not</b> be created for known web crawlers so this
        /// will return <c>null</c> in these cases.
        /// </note>
        /// </remarks>
        public static Guid? UniqueVisitorID
        {
            get
            {
                var visitor = GetUniqueVisitor();

                if (visitor == null)
                    return null;
                else
                    return visitor.ID;
            }
        }

        /// <summary>
        /// Returns the <see cref="UniqueVisitor" /> ID as a <c>string</c> if present, <c>null</c> otherwise.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the method is not called within the processing context of a HTTP request.</exception>
        /// <remarks>
        /// <note>
        /// Unique visitor instances <b>are not</b> be created for known web crawlers so this
        /// will return <c>null</c> in these cases.
        /// </note>
        /// </remarks>
        public static string UniqueVisitorIDString
        {
            get
            {
                var visitor = GetUniqueVisitor();

                if (visitor == null)
                    return null;
                else
                    return visitor.ID.ToString();
            }
        }

        /// <summary>
        /// Generates an SEO sitemap XML file for the current website and returns
        /// the result as the response to the current HTTP request.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the method is not called within the processing context of a HTTP request.</exception>
        /// <remarks>
        /// <para>
        /// The sitemap XML returned is compatible with the standard defined
        /// at <a href="http://www.sitemaps.org" />.
        /// </para>
        /// <note>
        /// This method is designed to be called in the <b>Load()</b> event handler method of
        /// an ASPX page that has no HTML defined.
        /// </note>
        /// </remarks>
        public static void ReturnSeoSitemap()
        {
            ReturnSeoSitemap(null);
        }

        /// <summary>
        /// Generates an SEO sitemap XML file for the current website and returns
        /// the result as the response to the current HTTP request.
        /// </summary>
        /// <param name="dynamicIncludes">
        /// The set of dynamically generated absolute site URI strings to
        /// be included in the generated site map or <c>null</c>.
        /// </param>
        /// <exception cref="InvalidOperationException">Thrown if the method is not called within the processing context of a HTTP request.</exception>
        /// <remarks>
        /// <para>
        /// The sitemap XML returned is compatible with the standard defined
        /// at <a href="http://www.sitemaps.org" />.
        /// </para>
        /// <note>
        /// This method is designed to be called in the <b>Load()</b> event handler method of
        /// an ASPX page that has no HTML defined.
        /// </note>
        /// </remarks>
        public static void ReturnSeoSitemap(IEnumerable<string> dynamicIncludes)
        {
            // $todo(jeff.lill):
            //
            // This code should open the web page file and look for a NOINDEX in a metadata
            // element and exclude the page from the sitemap if this is found.

            if (HttpContext.Current == null)
                throw new InvalidOperationException("RenderSeoSitemap() is called outside the context of an HTTP request.");

            HttpRequest request = HttpContext.Current.Request;
            HttpResponse response = HttpContext.Current.Response;

            response.ContentType = "text/xml; charset=utf-8";

            // If the "SeoSitemap.xml" file exists in the temporary folder and it's not
            // too old, then simply return its data.

            string sitemapPath = Path.Combine(WebSettings.WebTempFolder, "SeoSiteMap.xml");

            if (File.Exists(sitemapPath) && DateTime.Now - File.GetCreationTimeUtc(sitemapPath) <= WebSettings.SeoSitemapTTL)
            {
                response.WriteFile(sitemapPath);
                response.End();
                return;
            }

            // We need to generate and cache the sitemap file.

            var         includes = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            var         excludes = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            string      host     = request.Url.Host;
            string      rootPath;

            foreach (var exclude in WebSettings.SeoSitemapExclude)
                if (!excludes.ContainsKey(exclude))
                    excludes.Add(exclude, true);

            foreach (var include in WebSettings.SeoSitemapInclude)
                if (!includes.ContainsKey(include))
                    includes.Add(include, DateTime.UtcNow);

            if (dynamicIncludes != null)
            {
                foreach (var include in dynamicIncludes)
                    if (!includes.ContainsKey(include))
                        includes.Add(include, DateTime.UtcNow);
            }

            rootPath = WebHelper.RootPath;
            rootPath = Helper.StripTrailingSlash(rootPath);

            foreach (var pattern in WebSettings.SeoSitemapPatterns)
                foreach (var path in Helper.GetFilesByPattern(Path.Combine(WebHelper.RootPath, pattern), SearchOption.AllDirectories))
                {
                    var relPath = path.Substring(rootPath.Length).Replace('\\', '/');

                    if (excludes.ContainsKey(relPath))
                        continue;   // Ignore excluded files

                    if (relPath.Contains("/Package/PackageTmp/"))
                        continue;   // Ignore build related files (generated by Publish...)

                    if (includes.ContainsKey(relPath))
                        continue;   // File already included

                    includes.Add(relPath, File.GetLastWriteTimeUtc(path));
                }

            var docElement  = new XDocument(new XDeclaration("1.0", "UTF-8", "true"));
            var rootElement = new XElement("urlset", new XAttribute("xmlns", "http://www.sitemaps.org/schemas/sitemap/0.9"));

            docElement.Add(rootElement);

            foreach (var entry in includes)
            {
                string uri;

                if (entry.Key.ToLowerInvariant().StartsWith("http://"))
                    uri = entry.Key;
                else
                    uri = new Uri(string.Format("http://{0}:{1}{2}", host, request.Url.Port, entry.Key)).ToString();

                rootElement.Add(
                    new XElement("url",
                        new XElement("loc", uri),
                        new XElement("lastmod", entry.Value.ToString("yyyy-MM-dd")),
                        new XElement("priority", "1.0")
                    )
                );
            }

            using (var writer = new XmlTextWriter(sitemapPath, new UTF8Encoding(false)))
                docElement.WriteTo(writer);

            response.WriteFile(sitemapPath);
            response.End();
        }

        /// <summary>
        /// Generates XML for the application or system status passed and returns it as the response for the
        /// current HTTP request.
        /// </summary>
        /// <param name="status">The status</param>
        /// <exception cref="InvalidOperationException">Thrown if the method is not called within the processing context of a HTTP request.</exception>
        /// <remarks>
        /// <para>
        /// The sitemap XML returned is compatible with the standard defined
        /// at <a href="http://www.sitemaps.org" />.
        /// </para>
        /// <note>
        /// This method is designed to be called in the <b>Load()</b> event handler method of
        /// an ASPX page that has no HTML defined.
        /// </note>
        /// </remarks>
        public static void ReturnHeartbeat(HeartbeatStatus status)
        {
            HttpContext     context = HttpContext.Current;
            HttpResponse    response;

            if (context == null)
                throw new InvalidOperationException("RenderHeartbeat: Cannot be called outside the context of an HTTP request.");

            response = context.Response;
            response.TrySkipIisCustomErrors = true;
            response.StatusCode = status.Status == HealthStatus.Dead ? 500 : 200;
            response.ContentType = "text/xml";

            response.Write(status.ToElement());
            response.End();
        }

        // $todo(jeff.lill):
        //
        // Improve ProxyRequest() to handle other HTTP methods and also to forward headers
        // and data from the original request.
        //
        // I should probably also figure out a way to secure that so that random applications
        // on the NET can't take advantage of this for some bad purpose.

        /// <summary>
        /// Proxies an HTTP request on behalf of a client.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the method is not called within the processing context of a HTTP request.</exception>
        /// <remarks>
        /// <para>
        /// This is designed to be used for situations where Silverlight clients are not able
        /// to perform HTTP requests on their own due to the lack of a <b>clientaccesspolicy.xml</b>
        /// file on the target site, or other security related reasons.  This method is designed
        /// to be called within the <b>Page_Load()</b> method of ASPX code-behind classes.
        /// </para>
        /// <para>
        /// This method simply resubmits the request from the current <see cref="HttpContext" />,
        /// to the URI specified in the <b>X-Proxy-Uri</b> header.
        /// </para>
        /// <note>
        /// The current implementation is pretty simplistic and always submits the new request
        /// using the <b>GET</b> method and does not copy any headers or data from the originating
        /// request.
        /// </note>
        /// </remarks>
        public static void ProxyRequest()
        {
            HttpContext     context = HttpContext.Current;
            string          uri;
            HttpRequest     sourceRequest;
            HttpResponse    sourceResponse;

            if (context == null)
                throw new InvalidOperationException("ProxyRequest: Cannot be called outside the context of an HTTP request.");

            sourceRequest  = context.Request;
            sourceResponse = context.Response;

            uri = sourceRequest.Headers["X-Proxy-Uri"];
            if (string.IsNullOrWhiteSpace(uri))
                throw new InvalidOperationException("ProxyRequest: [X-Proxy-Uri] is missing in source request.");

            HttpWebRequest  proxyRequest;
            HttpWebResponse proxyResponse;

            try
            {
                proxyRequest  = (HttpWebRequest)HttpWebRequest.Create(uri);
                proxyResponse = (HttpWebResponse)proxyRequest.GetResponse();
            }
            catch (WebException e)
            {
                proxyResponse = (HttpWebResponse)e.Response;
            }

            // Copy the results back to the source response.

            sourceResponse.StatusCode = (int)proxyResponse.StatusCode;
            sourceResponse.StatusDescription = proxyResponse.StatusDescription;
            sourceResponse.ContentType = proxyResponse.ContentType;

            using (var proxyStream = new EnhancedStream(proxyResponse.GetResponseStream()))
                proxyStream.CopyTo(sourceResponse.OutputStream, -1);

            sourceResponse.End();
        }

        /// <summary>
        /// Expands the contents of the literal control passed into a page <b>meta description</b>,
        /// using a macro processor.
        /// </summary>
        /// <param name="literal">The literal control.</param>
        /// <param name="processor">The macro processor.</param>
        public static void GenPageDescription(Literal literal, MacroProcessor processor)
        {
            literal.Text = string.Format(@"<meta name=""description"" content=""{0}"" />", processor.Expand(literal.Text));
        }

        /// <summary>
        /// Expands the contents of the literal control passed into page <b>meta keywords</b>,
        /// using a macro processor.
        /// </summary>
        /// <param name="literal">The literal control.</param>
        /// <param name="processor">The macro processor.</param>
        public static void GenPageKeywords(Literal literal, MacroProcessor processor)
        {
            literal.Text = string.Format(@"<meta name=""keywords"" content=""{0}"" />", processor.Expand(literal.Text));
        }

        /// <summary>
        /// Expands the contents of the label control passed using a macro processor.
        /// </summary>
        /// <param name="label">The label control.</param>
        /// <param name="processor">The macro processor.</param>
        public static void ExpandMacros(Label label, MacroProcessor processor)
        {
            label.Text = processor.Expand(label.Text);
        }

        /// <summary>
        /// Expands the contents of the literal control passed using a macro processor.
        /// </summary>
        /// <param name="literal">The literal control.</param>
        /// <param name="processor">The macro processor.</param>
        public static void ExpandMacros(Literal literal, MacroProcessor processor)
        {
            literal.Text = processor.Expand(literal.Text);
        }

        /// <summary>
        /// Replaces any dashes in a string with spaces.
        /// </summary>
        /// <param name="value">The input string.</param>
        /// <returns>The converted string.</returns>
        public static string DashToSpace(string value)
        {
            return value.Replace('-', ' ');
        }

        /// <summary>
        /// Replaces any spaces in a string with dashes.
        /// </summary>
        /// <param name="value">The input string.</param>
        /// <returns>The converted string.</returns>
        public static string SpaceToDash(string value)
        {
            return value.Replace(' ', '-');
        }

        /// <summary>
        /// Escapes any ampersand (<b>&amp;</b>) characters found in the source
        /// string with <b>&amp;</b>.
        /// </summary>
        /// <param name="value">The source string.</param>
        /// <returns>The escaped string.</returns>
        /// <remarks>
        /// This method is useful when generating URLs embedded within HTML that
        /// may include ampersands in their query string.
        /// </remarks>
        public static string HtmlEscapeAmpersand(string value)
        {
            return value.Replace("&", "&amp;");
        }

        /// <summary>
        /// Implements <b>deflate</b> or <b>gzip</b> compression of website responses.
        /// </summary>
        /// <param name="app">The web application.</param>
        /// <remarks>
        /// <para>
        /// This method is designed to be called within the <b>Application_PreRequestHandlerExecute()</b>
        /// event handler within the web application's <b>Global.asax.cs</b> file.  This method
        /// will compress the response if the client indicates that it is capable of decompressing
        /// and if the <see cref="WebSettings.CompressOutput"/> configuration setting is <c>true</c>.
        /// </para>
        /// </remarks>
        public static void CompressOutput(HttpApplication app)
        {
            var acceptEncoding = app.Request.Headers["Accept-Encoding"];
            var orgStream      = app.Response.Filter;

            if (!WebSettings.CompressOutput ||
                app.Context.CurrentHandler == null ||
                !(app.Context.CurrentHandler is Page || app.Context.CurrentHandler.GetType().Name == "SyncSessionlessHandler") ||
                app.Request["HTTP_X_MICROSOFTAJAX"] != null ||
                string.IsNullOrWhiteSpace(acceptEncoding))
            {
                return;     // Compression is disabled for one of several reasons
            }

            acceptEncoding = acceptEncoding.ToLowerInvariant();

            if (acceptEncoding.Contains("deflate") || acceptEncoding == "*")
            {
                app.Response.Filter = new DeflateStream(orgStream, CompressionMode.Compress);
                app.Response.AppendHeader("Content-Encoding", "deflate");
            }
            else if (acceptEncoding.Contains("gzip"))
            {
                app.Response.Filter = new GZipStream(orgStream, CompressionMode.Compress);
                app.Response.AppendHeader("Content-Encoding", "gzip");
            }
        }

        /// <summary>
        /// Renders an HTTP request into a string suitable for logging.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The rendered string or <c>null</c> if the request should not be logged.</returns>
        /// <remarks>
        /// <note>
        /// This method will return <c>null</c> if it determines that the request
        /// should not be logged due to the fact that the request is made by an
        /// automated server heartbeat or health detection service.
        /// </note>
        /// </remarks>
        public static string GetRequestLogEntry(HttpRequest request)
        {
            // $todo(jeff.lill):
            //
            // Might want to add optional support for logging request data.

            if (Serialize.Parse(request.Headers["X-Health-Check"], false))
                return null;

            var sb = new StringBuilder(512);

            sb.AppendFormatLine("{0} {1} {2}", request.HttpMethod, request.RawUrl, request.ServerVariables["SERVER_PROTOCOL"]);

            foreach (var key in request.Headers.AllKeys)
                sb.AppendFormatLine("{0}: {1}", key, request.Headers[key]);

            // Add synthesized headers that describe the source IP address(s).

            string ipAddress;

            if (request.Headers["X-Remote-Address"] == null)
            {
                ipAddress = request.ServerVariables["REMOTE_ADDR"];
                if (!string.IsNullOrWhiteSpace(ipAddress))
                    sb.AppendFormatLine("X-Remote-Address: {0}", ipAddress);
            }

            if (request.Headers["X-Forwarded-For"] == null)
            {
                ipAddress = request.ServerVariables["HTTP_X_FORWARDED_FOR"];
                if (!string.IsNullOrWhiteSpace(ipAddress))
                    sb.AppendFormatLine("X-Forwarded-For: {0}", ipAddress);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Returns the a short string suitable for tagging a log entry that
        /// categorizes a HTTP request.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The request logging tag.</returns>
        /// <remarks>
        /// <para>
        /// This method currently return the following tags:
        /// </para>
        /// <list type="table">
        ///     <item>
        ///         <term>PageView</term>
        ///         <description>
        ///         The request appears to be a page view from a normal user.
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term>BotView:&lt;bot-name&gt;</term>
        ///         <description>
        ///         The request appears to be from a web crawler.  The of the crawler
        ///         will be added to the tag if it can be determined.
        ///         </description>
        ///     </item>
        /// </list>
        /// </remarks>
        public static string GetRequestLogTag(HttpRequest request)
        {
            if (request.Browser.Crawler)
                return "BotView:" + request.Browser.Browser;
            else
                return "PageView";
        }

        /// <summary>
        /// Determines whether it appears that the current request is being hosted by
        /// the Visual Studio development server.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the property is referenced outside of a HTTP request.</exception>
        public static bool IsDevelopmentServer
        {
            get
            {
                var context = HttpContext.Current;

                if (context == null)
                    throw new InvalidOperationException("WebHelper.IsDevelopmentServer cannot be called outside of a HTTP request.");

                // I'm going to assume that requests to the LOCALHOST with whacky port
                // numbers must be the development server.

                var uri = context.Request.Url;

                if (String.Compare("localhost", uri.Host, true) != 0)
                    return false;

                return uri.Port != NetworkPort.HTTP && uri.Port != NetworkPort.SSL;
            }
        }

        /// <summary>
        /// Ensures that the form action for a page has a leading "/" so that the postbacks
        /// will work correctly for pages that had their URLs rewritten.
        /// </summary>
        /// <param name="page">The ASPX page.</param>
        /// <param name="form">The form.</param>
        /// <param name="queryString">The query string including the leading "?" (or <c>null</c>).</param>
        /// <remarks>
        /// <para>
        /// Web pages that implement <see cref="IEnhancedPage" /> can customize the postback URL
        /// by implementing the <see cref="IEnhancedPage" />.<see cref="IEnhancedPage.VirtualPagePath" />
        /// property.
        /// </para>
        /// <note>
        /// The Visual Studio Development Web server does not support POSTing to the
        /// root site URL and perhaps has other limitations.  This method attempts to
        /// detect whether the site is currently being hosted by the development server
        /// and if it is, this method <b>will not</b> call the <see cref="IEnhancedPage" />.<see cref="IEnhancedPage.VirtualPagePath" />
        /// property to implement URI re-writing for the root page.
        /// </note>
        /// </remarks>
        public static void NormalizeFormAction(Page page, HtmlForm form, string queryString)
        {
            IEnhancedPage   enhancedPage = page as IEnhancedPage;
            string          absolutePath;

            if ((!IsDevelopmentServer || enhancedPage.VirtualPagePath != "/") && enhancedPage != null)
                absolutePath = enhancedPage.VirtualPagePath;
            else
                absolutePath = page.AppRelativeVirtualPath.Substring(1);

            if (queryString == null)
                form.Action = absolutePath;
            else
                form.Action = absolutePath + queryString;
        }

        /// <summary>
        /// Parses the URL encoded form parameters from a HTTP request.
        /// </summary>
        /// <param name="request">The source request.</param>
        /// <returns>A dictionary containing the form name/value pairs.</returns>
        /// <exception cref="FormatException">Thrown if the request content is not encoded as <b>application/x-www-form-urlencoded</b>.</exception>
        public static Dictionary<string, string> ParseFormArgs(HttpRequest request)
        {
            // Somewhere along the line, Helper.ParseFormArgs() as been deleted so I'm
            // going to treat this as unimplemented until I restore it.

            throw new NotImplementedException();

#if TODO
            if (String.Compare(request.ContentType,"application/x-www-form-urlencoded",true) != 0)
                throw new FormatException("[application/x-www-form-urlencoded] content encoding expected.");

            using (var contentStream = new EnhancedStream(request.InputStream)) 
            {
                var content = contentStream.ReadAllText(Encoding.UTF8);   // $hack(jeff.lill): Hardcoding the encoding to UTF-8

                return Helper.ParseFormArgs(content);
            }
#endif
        }

        /// <summary>
        /// Maps a file name with an extension to a MIME type.
        /// </summary>
        /// <param name="fileName">The file name with extension or an extension by itself with a leading period.</param>
        /// <returns>The MIME type if a mapping exists, <c>null</c> otherwise.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="fileName" /> is <c>null</c>.</exception>
        /// <remarks>
        /// This method uses the <see cref="WebSettings" />.<see cref="WebSettings.MimeMappings" />
        /// table to perform the lookup.  This table is initialized to common mappings which can
        /// also be extended by the application via its configuration file or custom initializion
        /// code.
        /// </remarks>
        public static string GetMimeMapping(string fileName)
        {
            // $todo(jeff.lill):
            //
            // Use the MimeMapping.GetMimeMapping() method when .NET Framework 4.5 is released.

            string mime;

            if (fileName == null)
                throw new ArgumentNullException("fileName");

            if (WebSettings.MimeMappings.TryGetValue(Path.GetExtension(fileName), out mime))
                return mime;
            else
                return null;
        }

        /// <summary>
        /// Adds a Javascript <b>keypress</b> event handler to a <see cref="TextBox" /> that checks for
        /// an <b>enter</b> key and initiates an ASP.NET postback for for a specified control when 
        /// an <b>enter</b> key is pressed while the text box has the focus.  This is useful for situations 
        /// where non-button controls (such as an <see cref="ImageButton" /> or <see cref="HyperLink" />) 
        /// need to be fired when the user presses <b>enter</b>.
        /// </summary>
        /// <param name="textBox">The text box control.</param>
        /// <param name="defControl">The control to be clicked when <b>enter</b> is pressed.</param>
        /// <param name="script">Optional additional Javascript that will be called when <b>enter</b> is pressed (or <c>null</c>).</param>
        public static void SetEnterHandler(TextBox textBox, Control defControl, string script)
        {
            if (script == null)
                script = string.Empty;

            script = script.Trim();
            if (!string.IsNullOrWhiteSpace(script) && script[script.Length - 1] != ';')
                script += ";";

            textBox.Attributes.Add("onKeyPress", string.Format("javascript: if (event.keyCode==13) {{ {0}; {1} return false; }} else return true;", textBox.Page.ClientScript.GetPostBackEventReference(defControl, string.Empty), script));
        }
    }
}
