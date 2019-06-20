//-----------------------------------------------------------------------------
// FILE:        AppLoader.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Continues the NeonSwitch application load proceess begun by the
//              low-level LillTek.Telephony.NeonAppLoader assembly.

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using FreeSWITCH;
using FreeSWITCH.Native;

using LillTek.Common;
using LillTek.Telephony.Common;

namespace LillTek.Telephony.NeonSwitch
{
    /// <summary>
    /// <para>
    /// Continues the NeonSwitch application load proceess begun by the
    /// low-level LillTek.Telephony.NeonAppLoader assembly.
    /// </para>
    /// <note>
    /// This class is called internally during the application load process and
    /// is not intended to for direct use by NeonSwitch applications.
    /// </note>
    /// </summary>
    public class AppLoader
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Used to track the types derived from <see cref="SwitchApp" /> found in 
        /// the loaded assemblies.
        /// </summary>
        private class AppRef
        {
            public readonly Assembly Assembly;
            public readonly string TypeName;

            public AppRef(Assembly assembly, string typeName)
            {
                this.Assembly = assembly;
                this.TypeName = typeName;
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private SwitchApp app;    // The NeonSwitch application instance.

        /// <summary>
        /// Default constructor.
        /// </summary>
        public AppLoader()
        {
        }

        /// <summary>
        /// Continues the application load process by scanning the loaded assemblies
        /// for the class definition that derives from <see cref="SwitchApp" /> 
        /// and then constructing an instance.
        /// </summary>
        /// <param name="appName">Name to be used for the module.</param>
        /// <param name="appPath">Path to the application folder.</param>
        /// <param name="loaderDllName">Name of the NeonSwitch application loader DLL file.</param>
        /// <param name="appClassName">The optional application class name or <c>null</c>.</param>
        /// <exception cref="TypeLoadException">Thrown if a single derived <see cref="SwitchApp" /> type could not be found or instantiated.</exception>
        /// <exception cref="NotSupportedException">Thrown if the application is not running on a NeonSwitch enabled build of FreeSWITCH.</exception>
        /// <remarks>
        /// <note>
        /// If <paramref name="appClassName" /> is passed as <c>null</c> then only one type 
        /// deriving from <see cref="SwitchApp" /> may be defined across all of the loaded
        /// assemblies.  A <see cref="TypeLoadException" /> will be thrown if more than one 
        /// definitions are located.
        /// </note>
        /// </remarks>
        public void Load(string loaderDllName, string appName, string appPath, string appClassName)
        {
            var     thisAssembly    = Assembly.GetExecutingAssembly();
            var     appRefs         = new List<AppRef>();
            var     NeonSwitchValue = Switch.GetGlobal("NeonSwitch");
            bool    isNeonSwitch;

            // Verify that we're actually running on a NeonSwitch enabled version of FreeSwitch
            // by looking for the [NeonSwitch] global variable.

            if (NeonSwitchValue == null || !bool.TryParse(NeonSwitchValue, out isNeonSwitch) || !isNeonSwitch)
                throw new NotSupportedException("NeonSwitch applications require a NeonSwitch enabled FreeSWITCH build.  Load terminated.");

            // Continue the loading process.

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.IsDerivedFrom(typeof(SwitchApp)))
                        appRefs.Add(new AppRef(assembly, type.FullName));
                }
            }

            // Attempt to construct a specific type if one was specified.

            if (!string.IsNullOrWhiteSpace(appClassName))
            {
                foreach (var appRef in appRefs)
                    if (appClassName == appRef.TypeName)
                    {
                        app = (SwitchApp)appRef.Assembly.CreateInstance(appClassName);
                        app.Load(appName, loaderDllName, appPath);
                        return;
                    }

                throw new TypeLoadException(string.Format("Could not locate application class [{0}] in the loaded assemblies.", appClassName));
            }

            // Remove any references to test applications in this assembly.

            var delList = new List<AppRef>();

            foreach (var appRef in appRefs)
                if (appRef.Assembly == thisAssembly)
                    delList.Add(appRef);

            foreach (var appRef in delList)
                appRefs.Remove(appRef);

            // Make sure we found a single entry class in the assemblies.

            if (appRefs.Count == 0)
                throw new TypeLoadException("Could not find a NeonSwitch application class that derives from [LillTek.Telephony.NeonSwitch.SwitchApp].");
            else if (appRefs.Count > 1)
            {
                var sb = new StringBuilder();

                sb.AppendLine("Multiple NeonSwitch application classes defined:");
                sb.AppendLine();

                foreach (var appRef in appRefs)
                {
                    sb.AppendFormatLine("[{0}] in [{1}]", appRef.TypeName, appRef.Assembly.Location);
                }

                throw new TypeLoadException(sb.ToString());
            }

            // Instantiate the application's entry point class.  Note that the base
            // SwitchApp class will take over the remainder of the NeonSwitch
            // platform initialization.

            app = (SwitchApp)appRefs[0].Assembly.CreateInstance(appRefs[0].TypeName);
            app.Load(appName, loaderDllName, appPath);
        }

        /// <summary>
        /// Performs an action on a session.
        /// </summary>
        /// <param name="context">The application context.</param>
        public void RunSession(AppContext context)
        {
            if (app != null)
                app.OnNewCallSession(context);
        }

        /// <summary>
        /// Executes a command synchronously.
        /// </summary>
        /// <param name="context">The command context.</param>
        public void Execute(ApiContext context)
        {
            if (app != null)
                app.OnExecute(context);
        }

        /// <summary>
        /// Executes a command asynchronously.
        /// </summary>
        /// <param name="context">The command context.</param>
        public void ExecuteBackground(ApiBackgroundContext context)
        {
            if (app != null)
                app.OnExecuteBackground(context);
        }
    }
}
