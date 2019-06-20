//-----------------------------------------------------------------------------
// FILE:        MergeDefaultStylesTask.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Visual Studio build task that merges XAML files with custom control
//              default style resources into generic.xaml.

// Adapted from samples from Microsoft:
//
// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace LillTek.Tools.VSTasks
{
    /// <summary>
    /// Visual Studio build task that merges XAML files with custom control
    /// default style resources into generic.xaml.
    /// </summary>
    public class MergeDefaultStylesTask : Task
    {
        /// <summary>
        /// Used internally so that merged resources will be output in the same
        /// order that they were encountered in the source files.
        /// </summary>
        internal static int Order = 0;

        /// <summary>
        /// Gets or sets the root directory of the project where the
        /// generic.xaml file resides.
        /// </summary>
        [Required]
        public string ProjectDirectory { get; set; }

        /// <summary>
        /// Gets or sets the project items marked with the "DefaultStyle" build
        /// action.
        /// </summary>
        [Required]
        public ITaskItem[] DefaultStyles { get; set; }

        /// <summary>
        /// Initializes a new instance of the MergeDefaultStylesTask class.
        /// </summary>
        public MergeDefaultStylesTask()
        {
        }

        /// <summary>
        /// Merge the project items marked with the "DefaultStyle" build action
        /// into a single generic.xaml file.
        /// </summary>
        /// <returns>
        /// A value indicating whether or not the task succeeded.
        /// </returns>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Task should not throw exceptions.")]
        public override bool Execute()
        {
            Order = 0;

            Log.LogMessage(MessageImportance.Low, "Merging default styles into generic.xaml.");

            // Get the original generic.xaml
            string originalPath = Path.Combine(ProjectDirectory, Path.Combine("themes", "generic.xaml"));
            if (!File.Exists(originalPath))
            {
                Log.LogError("{0} does not exist!", originalPath);
                return false;
            }
            Log.LogMessage(MessageImportance.Low, "Found original generic.xaml at {0}.", originalPath);
            string original = null;
            Encoding encoding = Encoding.Default;
            try
            {
                using (StreamReader reader = new StreamReader(File.Open(originalPath, FileMode.Open, FileAccess.Read)))
                {
                    original = reader.ReadToEnd();
                    encoding = reader.CurrentEncoding;
                }
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex);
                return false;
            }

            // Create the merged generic.xaml
            List<DefaultStyle> styles = new List<DefaultStyle>();
            foreach (ITaskItem item in DefaultStyles)
            {
                string path = Path.Combine(ProjectDirectory, item.ItemSpec);
                if (!File.Exists(path))
                {
                    Log.LogWarning("Ignoring missing DefaultStyle {0}.", path);
                    continue;
                }

                try
                {
                    Log.LogMessage(MessageImportance.Low, "Processing file {0}.", item.ItemSpec);
                    styles.Add(DefaultStyle.Load(path));
                }
                catch (Exception ex)
                {
                    Log.LogErrorFromException(ex);
                }
            }
            string merged = null;
            try
            {
                merged = DefaultStyle.Merge(styles).GenerateXaml();
            }
            catch (InvalidOperationException ex)
            {
                Log.LogErrorFromException(ex);
                return false;
            }

            // Write the new generic.xaml
            if (original != merged)
            {
                Log.LogMessage(MessageImportance.Low, "Writing merged generic.xaml.");

                try
                {
                    // Could interact with the source control system / TFS here
                    File.SetAttributes(originalPath, FileAttributes.Normal);
                    Log.LogMessage("Removed any read-only flag for generic.xaml.");

                    File.WriteAllText(originalPath, merged, encoding);
                    Log.LogMessage("Successfully merged generic.xaml.");
                }
                catch (Exception ex)
                {
                    Log.LogErrorFromException(ex);
                    return false;
                }
            }
            else
            {
                Log.LogMessage("Existing generic.xaml was up to date.");
            }

            return true;
        }
    }
}