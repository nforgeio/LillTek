//-----------------------------------------------------------------------------
// FILE:        ConfigRewriter.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements a configuration file rewriter for use by unit tests.

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace LillTek.Common
{
    /// <summary>
    /// An array of instances of this class are passed to <see cref="ConfigRewriter" />
    /// to specify the tag/text values to be used when rewriting a configuration file.
    /// </summary>
    public sealed class ConfigRewriteTag
    {
        internal readonly string Tag;
        internal readonly string ReplaceText;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="tag">The tag that will be used to match $replace(tag) comments.</param>
        /// <param name="replaceText">The replacement text.</param>
        public ConfigRewriteTag(string tag, string replaceText)
        {
            this.Tag         = tag;
            this.ReplaceText = replaceText;
        }
    }

    /// <summary>
    /// Implements a configuration file rewriter for use by unit tests.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unit tests often need to modify an application's configuration file to 
    /// be able to exercise the application's functionality properly.  This class
    /// provides an standard and easy way to do this.
    /// </para>
    /// <para>
    /// The configuration file to be modified is determined by the particular constructor
    /// use to create the instance.  Use <see cref="ConfigRewriter()" /> to specify
    /// the default configuration file for the current application or <see cref="ConfigRewriter(string)" />
    /// specify the configuration file name.
    /// </para>
    /// <para>
    /// Use the <see cref="Rewrite" /> method to save a copy of the configuration file and
    /// then modify specially marked lines in the file.  Then call <see cref="Restore" /> to
    /// restore the file back to its original state.
    /// </para>
    /// <para>
    /// <see cref="Rewrite" /> looks for comments in the configuration file formatted as:
    /// </para>
    /// <code language="none">
    /// // $rewrite(&lt;tag&gt;)
    /// </code>
    /// <para>
    /// where &lt;tag&gt; identifies the section.  <see cref="Rewrite" /> is passed an array
    /// of <see cref="ConfigRewriteTag" /> instances that specify the tags to be replaced as
    /// well as the replacement text.  <see cref="Rewrite" /> will replace the tagged comment
    /// lines in the configuration file with the corresponding replacement text.
    /// </para>
    /// <note>
    /// Note that the <b>$rewrite</b> marker and the tag are case insenstive.
    /// </note>
    /// </remarks>
    public sealed class ConfigRewriter
    {
        private string configFile;     // Config file path
        private string orgText;        // Original file contents

        /// <summary>
        /// Associates the rewriter with the current application's default 
        /// configuration file.
        /// </summary>
        public ConfigRewriter()
        {
            configFile = Config.ConfigPath;
            orgText = null;
        }

        /// <summary>
        /// Associates the rewriter with the specified configuration file.
        /// </summary>
        /// <param name="configFile">Path to the configuration file.</param>
        public ConfigRewriter(string configFile)
        {
            this.configFile = configFile;
        }

        /// <summary>
        /// Rewrites specially formatted and tagged comment lines in the configuration 
        /// with replacement text.
        /// </summary>
        /// <param name="tags">Specifies the tags and replacement text.</param>
        /// <remarks>
        /// <para>
        /// This method looks for comments in the configuration file formatted as:
        /// </para>
        /// <code language="none">
        /// // $rewrite(&lt;tag&gt;)
        /// </code>
        /// <para>
        /// where &lt;tag&gt; identifies the section. The tags parameter should be passed as an array
        /// of <see cref="ConfigRewriteTag" /> instances that specify the tags to be replaced as
        /// well as the replacement text.  <see cref="Rewrite" /> will replace the tagged comment
        /// lines in the configuration file with the corresponding replacement text.
        /// </para>
        /// <note>
        /// Note that the <b>$rewrite</b> marker and the tag are case insenstive.
        /// </note>
        /// </remarks>
        public void Rewrite(ConfigRewriteTag[] tags)
        {
            if (configFile == null)
                return;

            if (orgText != null)
                throw new InvalidOperationException("Cannot nest calls to Rewrite().");

            Dictionary<string, string>  htTags;
            TextReader                  reader = null;
            StreamWriter                writer = null;
            string                      line;
            string                      s, lwr;
            string                      tag;
            int                         pos;
            StringBuilder               sb;

            htTags = new Dictionary<string, string>();
            for (int i = 0; i < tags.Length; i++)
                htTags.Add(tags[i].Tag.ToLowerInvariant(), tags[i].ReplaceText);

            try
            {
                // Load the configuration file

                reader = new StreamReader(configFile);
                orgText = reader.ReadToEnd();
                reader.Close();
                reader = null;

                // Process the file into a string builder

                reader = new StringReader(orgText);
                sb = new StringBuilder();

                line = reader.ReadLine();
                while (line != null)
                {
                    string replaceText = null;

                    s = line.Trim();
                    if (s.StartsWith("//") || s.StartsWith("--"))
                    {
                        s = s.Substring(2).Trim();
                        lwr = s.ToLowerInvariant();
                        pos = lwr.IndexOf(')');

                        if (lwr.StartsWith("$replace(") && pos != -1)
                        {
                            tag = lwr.Substring(9, pos - 9).Trim();
                            if (htTags.ContainsKey(tag))
                                replaceText = htTags[tag];
                        }
                    }

                    if (replaceText != null)
                    {
                        if (replaceText.EndsWith("\r\n"))
                            sb.Append(replaceText);
                        else
                            sb.AppendLine(replaceText);
                    }
                    else
                        sb.AppendLine(line);

                    line = reader.ReadLine();
                }

                // Write the processed text back to the file

                writer = new StreamWriter(configFile);
                writer.Write(sb.ToString());
                writer.Close();
                writer = null;
            }
            finally
            {
                if (reader != null)
                    reader.Close();

                if (writer != null)
                    writer.Close();
            }
        }

        /// <summary>
        /// Restores the configuration file to its state before <see cref="Rewrite" />
        /// was called.
        /// </summary>
        /// <remarks>
        /// <note>
        /// It is not an error to call this if <see cref="Rewrite" /> has never
        /// been called or to call this more than once.
        /// </note>
        /// </remarks>
        public void Restore()
        {
            StreamWriter writer;

            if (orgText == null)
                return;

            writer = new StreamWriter(configFile);

            try
            {
                writer.Write(orgText);
                orgText = null;
            }
            finally
            {
                writer.Close();
            }
        }
    }
}
