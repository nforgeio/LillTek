//-----------------------------------------------------------------------------
// FILE:        WebSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Holds global LillTek related web hosting settings parsed
//              from the LillTek.Web.Settings configuration section.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Net;
using System.Web;
using System.Web.Hosting;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.Remoting.Lifetime;

using LillTek.Common;
using LillTek.Service;

namespace LillTek.Web
{
    /// <summary>
    /// Holds global LillTek related web hosting settings parsed
    /// from the <b>LillTek.Web.Settings</b> configuration section.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is initialized within <see cref="Helper.InitializeWebApp" />
    /// and should not be relied upon until after this method is called.
    /// </para>
    /// <para>
    /// The following configuration settings are parsed and reasonable
    /// values are used when these settings are invalid or not available:
    /// </para>
    /// <div class="tablediv">
    /// <table class="dtTABLE" cellspacing="0" ID="Table1">
    /// <tr valign="top">
    /// <th width="1">Setting</th>        
    /// <th width="1">Default</th>
    /// <th width="90%">Description</th>
    /// </tr>
    /// <tr valign="top">
    ///     <td>WebTempFolder</td>
    ///     <td><b>$(AppPath)\WebTemp</b></td>
    ///     <td>
    ///     Specifies the absolute file system path where website related temporary
    ///     files are to be located.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>CompressOutput</td>
    ///     <td><c>true</c></td>
    ///     <td>
    ///     Controls whether calls to <see cref="WebHelper.CompressOutput" /> perform
    ///     <b>deflate</b> or <b>gzip</b> compression for website responses if the
    ///     client application indicates that it is capable.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>SeoSitemapPatterns</td>
    ///     <td>*.aspx;*.asp;*.htm;*.html</td>
    ///     <td>
    ///     The list of file patterns identifying web page files that should be included
    ///     in generated SEO sitemaps.  Multiple patterns should be separated by
    ///     semicolon (;) characters.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>SeoSitemapExclude</td>
    ///     <td>(none)</td>
    ///     <td>
    ///     List of relative or absolute HTTP URIs to be <b>excluded</b> from generated
    ///     SEO sitemaps.  Multiple URIs should be separated by semicolon (;) characters.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>SeoSitemapeInclude</td>
    ///     <td>(none)</td>
    ///     <td>
    ///     List of relative or absolute HTTP URIs to be <b>included</b> from generated
    ///     SEO sitemaps.  Multiple URIs should be separated by semicolon (;) characters.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>SeoSitemapTTL</td>
    ///     <td>1h</td>
    ///     <td>
    ///     The maximum time a generated SEO sitemap will be cached by the website.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>MimeMapping[ext] = MIME</td>
    ///     <td>(na)</td>
    ///     <td>
    ///     <para>
    ///     Specifies additional file extension to MIME mappings where <b>ext</b> specifies
    ///     the file type (with or without a leading period) and <b>MIME</b> specifies the
    ///     MIME type.
    ///     </para>
    ///     <note>
    ///     Several common MIME mappings are intialized by default.  See <see cref="MimeMappings" />
    ///     for a complete list.
    ///     </note>
    ///     </td>
    /// </tr>
    /// </table>
    /// </div>
    /// </remarks>
    public static class WebSettings
    {
        private const string NotInitializedMsg = "WebHelper.PlatformInitialize() must be called before accessing WebSettings.";

        private static bool                         isInitialized      = false;
        private static string                       webTempFolder      = null;
        private static bool                         compressOutput     = true;
        private static IList<string>                seoSitemapPatterns = null;
        private static IList<string>                seoSitemapExclude  = null;
        private static IList<string>                seoSitemapInclude  = null;
        private static TimeSpan                     seoSitemapTTL      = TimeSpan.Zero;
        private static Dictionary<string, string>   mimeMappings       = null;

        /// <summary>
        /// Called by <b>WebHelper.PlatformInitialize()</b> to load the settings from the application configuration.
        /// </summary>
        internal static void Load()
        {
            Config          config = new Config("LillTek.Web.Settings");
            string[]        items;
            List<string>    list;

            webTempFolder  = config.Get("WebTempFolder", Path.Combine(WebHelper.RootPath, "WebTemp"));
            compressOutput = config.Get("CompressOutput", compressOutput);

            // Parse the SEO sitemap file patterns, filtering out any that don't look reasonable.

            items = Helper.StripCRLF(config.Get("SeoSitemapPatterns", "*.aspx;*.asp;*.htm;*.html")).Split(';');
            list  = new List<string>(items.Length);

            foreach (var item in items)
                if (Helper.HasExtension(item) && item.IndexOfAny(Helper.FileWildcards) != -1)
                    list.Add(item);

            seoSitemapPatterns = new ReadOnlyCollection<string>(list);

            // Parse the SEO sitemap exclude URIs, filtering out any that don't look reasonable.

            items = Helper.StripCRLF(config.Get("SeoSitemapExclude", string.Empty)).Split(';');
            list  = new List<string>(items.Length);

            foreach (var item in items)
            {
                var uri = item.Replace('\\', '/').Trim();

                if (string.IsNullOrWhiteSpace(uri))
                    continue;

                if (uri.ToLowerInvariant().StartsWith("http://"))
                {
                    try
                    {
                        new Uri(uri);
                        list.Add(uri);
                    }
                    catch
                    {
                        // Ignore bad URIs
                    }
                }
                else
                {
                    if (!uri.StartsWith("/"))
                        uri = "/" + uri;

                    list.Add(uri);
                }
            }

            seoSitemapExclude = new ReadOnlyCollection<string>(list);

            // Parse the SEO sitemap include URIs, filtering out any that don't look reasonable.

            items = Helper.StripCRLF(config.Get("SeoSitemapInclude", string.Empty)).Split(';');
            list  = new List<string>(items.Length);

            foreach (var item in items)
            {
                var uri = item.Replace('\\', '/').Trim();

                if (string.IsNullOrWhiteSpace(uri))
                    continue;

                if (uri.ToLowerInvariant().StartsWith("http://"))
                {
                    try
                    {
                        new Uri(uri);
                        list.Add(uri);
                    }
                    catch
                    {
                        // Ignore bad URIs
                    }
                }
                else
                {
                    if (!uri.StartsWith("/"))
                        uri = "/" + uri;

                    list.Add(uri);
                }
            }

            seoSitemapInclude = new ReadOnlyCollection<string>(list);

            // Parse the SEO sitemap TTL.

            seoSitemapTTL = config.Get("SeoSitemapTTL", TimeSpan.FromHours(1));

            // Initialize common MIME mappings and then load additional mappings from config.

            // $todo(jeff.lill): 
            //
            // Delete this code once we port to .NET Framework 4.5 and fully implement 
            // WebHelper.GetMimeMapping().

            mimeMappings = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

            var defMappings =
@"
.aaf	application/octet-stream
.aif	audio/x-aiff
.aifc	audio/aiff
.aiff	audio/aiff
.asf	video/x-ms-asf
.asr	video/x-ms-asf
.asx	video/x-ms-asf
.atom	application/atom+xml
.au		audio/basic
.avi	video/x-msvideo
.bin	application/octet-stream
.bmp	image/bmp
.cab	application/octet-stream
.class	application/x-java-applet
.css	text/css
.csv	application/octet-stream
.dll	application/x-msdownload
.doc	application/msword
.docx	application/vnd.openxmlformats-officedocument.wordprocessingml.document
.exe	application/octet-stream
.fla	application/octet-stream
.gif	image/gif
.gtar	application/x-gtar
.gz		application/x-gzip
.htm	text/html
.html	text/html
.ico	image/x-icon
.config	text/xml
.inf	application/octet-stream
.ini	text/plain
.jpe	image/jpeg
.jpeg	image/jpeg
.jpg	image/jpeg
.js		application/x-javascript
.jsx	text/jscript
.mid	audio/mid
.midi	audio/mid
.mov	video/quicktime
.mp2	video/mpeg
.mp3	audio/mpeg
.mpa	video/mpeg
.mpe	video/mpeg
.mpeg	video/mpeg
.mpg	video/mpeg
.mpv2	video/mpeg
.pdf	application/pdf
.png	image/png
.pnz	image/png
.ppt	application/vnd.ms-powerpoint
.pptx	application/vnd.openxmlformats-officedocument.presentationml.presentation
.qt		video/quicktime
.rtf	application/rtf
.swf	application/x-shockwave-flash
.tiff	image/tiff
.txt	text/plain
.vcf	text/x-vcard
.vsd	application/vnd.visio
.wav	audio/wav
.wm		video/x-ms-wm
.wma	audio/x-ms-wma
.wmv	video/x-ms-wmv
.xaml	application/xaml+xml
.xap	application/x-silverlight-app
.xls	application/vnd.ms-excel
.xlsx	application/vnd.openxmlformats-officedocument.spreadsheetml.sheet
.z		application/x-compress
.zip	application/x-zip-compressed
";
            using (var reader = new StringReader(defMappings))
            {
                for (var line = reader.ReadLine(); line != null; line = reader.ReadLine())
                {
                    line = line.Trim();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var pos = line.IndexOf(' ');

                    if (pos == -1)
                        continue;

                    var ext  = line.Substring(0, pos).Trim();
                    var mime = line.Substring(pos + 1).Trim();

                    if (ext.Length == 0 || mime.Length == 0)
                        continue;

                    if (ext[0] != '.')
                        ext = "." + ext;

                    mimeMappings[ext] = mime;
                }
            }

            // Now load custom MIME mappings from the web configuration.

            var mimeDictionary = config.GetDictionary("MimeMapping");

            foreach (var entry in mimeDictionary)
            {
                var ext  = entry.Key.Trim();
                var mime = entry.Value.Trim();

                if (ext.Length == 0 || mime.Length == 0)
                    continue;

                if (ext[0] != '.')
                    ext = "." + ext;

                mimeMappings[ext] = mime;
            }

            // We're done.

            isInitialized = true;
        }

        /// <summary>
        /// Returns the fully qualified path to the folder where temporary files
        /// generated by the website can be placed.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if <b>WebHelper.PlatformInitialize()</b> has not been called.</exception>
        public static string WebTempFolder
        {
            get
            {
                if (!isInitialized)
                    throw new InvalidOperationException(NotInitializedMsg);

                return webTempFolder;
            }
        }

        /// <summary>
        /// Controls whether calls to <see cref="WebHelper.CompressOutput" /> perform
        /// <b>deflate</b> or <b>gzip</b> compression for website responses if the
        /// client application indicates that it is capable.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if <b>WebHelper.PlatformInitialize()</b> has not been called.</exception>
        public static bool CompressOutput
        {
            get
            {
                if (!isInitialized)
                    throw new InvalidOperationException(NotInitializedMsg);

                return compressOutput;
            }
        }

        /// <summary>
        /// Returns the file name patterns with wildcards that specify the website files
        /// to be included in the SEO sitemap.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if <b>WebHelper.PlatformInitialize()</b> has not been called.</exception>
        public static IList<string> SeoSitemapPatterns
        {
            get
            {
                if (!isInitialized)
                    throw new InvalidOperationException(NotInitializedMsg);

                return seoSitemapPatterns;
            }
        }

        /// <summary>
        /// List of relative or absolute URIs to be <b>excluded</b> from the SEO sitemap
        /// generated by the <see cref="WebHelper.ReturnSeoSitemap()" /> method.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if <b>WebHelper.PlatformInitialize()</b> has not been called.</exception>
        public static IList<string> SeoSitemapExclude
        {
            get
            {
                if (!isInitialized)
                    throw new InvalidOperationException(NotInitializedMsg);

                return seoSitemapExclude;
            }
        }

        /// <summary>
        /// List of relative or absolute URIs to be <b>included</b> in the SEO sitemap
        /// generated by the <see cref="WebHelper.ReturnSeoSitemap()" /> method.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if <b>WebHelper.PlatformInitialize()</b> has not been called.</exception>
        public static IList<string> SeoSitemapInclude
        {
            get
            {
                if (!isInitialized)
                    throw new InvalidOperationException(NotInitializedMsg);

                return seoSitemapInclude;
            }
        }

        /// <summary>
        /// The maximum time a generated SEO sitemap should be cached.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if <b>WebHelper.PlatformInitialize()</b> has not been called.</exception>
        public static TimeSpan SeoSitemapTTL
        {
            get
            {
                if (!isInitialized)
                    throw new InvalidOperationException(NotInitializedMsg);

                return seoSitemapTTL;
            }
        }

        /// <summary>
        /// Dictionary of file extensions to MIME types.
        /// </summary>
        /// <remarks>
        /// <note>
        /// The file extensions including a leading period and the dictionary performs
        /// case insensitive lookups.
        /// </note>
        /// <para>
        /// This table is initialized with common MIME mappings.  Additional mappings can be
        /// specified in the <b>LillTek.Web.Settings</b> section of the application's INI file
        /// using <b>MimeMapping</b> settings:
        /// </para>
        /// <example>
        /// #section LillTek.Web.Settings
        ///     
        ///     MimeMapping[.pdf] = application/pdf
        ///     MimeMapping[.xls] = application/x-msexcel
        /// 
        /// #endsection
        /// </example>
        /// <note>
        /// It is also possible to set custom MIME mappings in code but you need to be
        /// very sure to modify the collection only when its not possible that the 
        /// collection can be accessed by another thread.  You should restrict any
        /// direct modifications to global initialization routines after
        /// <see cref="WebHelper.PlatformInitialize(Assembly)" /> is called.
        /// </note>
        /// <para>
        /// Applications should use the <see cref="WebHelper.GetMimeMapping" /> method
        /// to actually map file names to MIME types rather than accessing this property
        /// directly to take advantage of the future capability to use the built-in IIS
        /// MIME mappings once .NET Framework 4.5 is released.
        /// </para>
        /// <para>
        /// Here's a list of the common MIME mappings:
        /// </para>
        /// <example>
        /// .aaf	application/octet-stream
        /// .aif	audio/x-aiff
        /// .aifc	audio/aiff
        /// .aiff	audio/aiff
        /// .asf	video/x-ms-asf
        /// .asr	video/x-ms-asf
        /// .asx	video/x-ms-asf
        /// .atom	application/atom+xml
        /// .au		audio/basic
        /// .avi	video/x-msvideo
        /// .bin	application/octet-stream
        /// .bmp	image/bmp
        /// .cab	application/octet-stream
        /// .class	application/x-java-applet
        /// .css	text/css
        /// .csv	application/octet-stream
        /// .dll	application/x-msdownload
        /// .doc	application/msword
        /// .docx	application/vnd.openxmlformats-officedocument.wordprocessingml.document
        /// .exe	application/octet-stream
        /// .fla	application/octet-stream
        /// .gif	image/gif
        /// .gtar	application/x-gtar
        /// .gz		application/x-gzip
        /// .htm	text/html
        /// .html	text/html
        /// .ico	image/x-icon
        /// .config	text/xml
        /// .inf	application/octet-stream
        /// .ini	text/plain
        /// .jpe	image/jpeg
        /// .jpeg	image/jpeg
        /// .jpg	image/jpeg
        /// .js		application/x-javascript
        /// .jsx	text/jscript
        /// .mid	audio/mid
        /// .midi	audio/mid
        /// .mov	video/quicktime
        /// .mp2	video/mpeg
        /// .mp3	audio/mpeg
        /// .mpa	video/mpeg
        /// .mpe	video/mpeg
        /// .mpeg	video/mpeg
        /// .mpg	video/mpeg
        /// .mpv2	video/mpeg
        /// .pdf	application/pdf
        /// .png	image/png
        /// .pnz	image/png
        /// .ppt	application/vnd.ms-powerpoint
        /// .pptx	application/vnd.openxmlformats-officedocument.presentationml.presentation
        /// .qt		video/quicktime
        /// .rtf	application/rtf
        /// .swf	application/x-shockwave-flash
        /// .tiff	image/tiff
        /// .txt	text/plain
        /// .vcf	text/x-vcard
        /// .vsd	application/vnd.visio
        /// .wav	audio/wav
        /// .wm		video/x-ms-wm
        /// .wma	audio/x-ms-wma
        /// .wmv	video/x-ms-wmv
        /// .xaml	application/xaml+xml
        /// .xap	application/x-silverlight-app
        /// .xls	application/vnd.ms-excel
        /// .xlsx	application/vnd.openxmlformats-officedocument.spreadsheetml.sheet
        /// .z		application/x-compress
        /// .zip	application/x-zip-compressed
        /// </example>
        /// </remarks>
        public static Dictionary<string, string> MimeMappings
        {
            get
            {
                if (!isInitialized)
                    throw new InvalidOperationException(NotInitializedMsg);

                return mimeMappings;
            }
        }
    }
}
