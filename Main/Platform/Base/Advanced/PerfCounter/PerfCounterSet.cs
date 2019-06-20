//-----------------------------------------------------------------------------
// FILE:        PerfCounterSet.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Manages a set of performance counters belonging to a category

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security;

using LillTek.Common;

// $todo(jeff.lill): 
//
// I'm not super happy with how I'm implementing calculated counters
// using the Relate() method.  This handles the rolling up of counter 
// values but not anything else (like averages etc).

namespace LillTek.Advanced
{
    /// <summary>
    /// Manages the performance counters for a specific performance counter category.
    /// </summary>
    public sealed class PerfCounterSet
    {
        private Dictionary<string, PerfCounter> counters;
        private string                          categoryName;
        private string                          categoryHelp;
        private bool                            enabled;
        private bool                            installed;
        private bool                            installFailed;
        private bool                            multiInstance;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="multiInstance">True for multi-instance counter sets, false for single instance.</param>
        /// <param name="enabled"><c>true</c> to enable the performance counters.</param>
        /// <param name="categoryName">The performance counter category name.</param>
        /// <param name="categoryHelp">The performance counter category description.</param>
        /// <remarks>
        /// Pass enabled=<c>false</c> to create a performance counter set that will 
        /// not attempt to create the underlying Windows performance counters.
        /// </remarks>
        public PerfCounterSet(bool multiInstance, bool enabled, string categoryName, string categoryHelp)
        {
            this.enabled       = enabled;
            this.counters      = new Dictionary<string, PerfCounter>(StringComparer.InvariantCultureIgnoreCase);
            this.categoryName  = categoryName;
            this.categoryHelp  = categoryHelp;
            this.installed     = false;
            this.installFailed = false;
            this.multiInstance = multiInstance;
        }

        /// <summary>
        /// Returns <c>true</c> if the performance counters are enabled.
        /// </summary>
        public bool IsEnabled
        {
            get { return enabled; }
        }

        /// <summary>
        /// Returns <c>true</c> if the counter set is capable of holding multiple instances of counters.
        /// </summary>
        public bool IsMultiInstance
        {
            get { return multiInstance; }
        }

        /// <summary>
        /// Returns the performance counter set's category name.
        /// </summary>
        public string CategoryName
        {
            get { return categoryName ?? string.Empty; }
        }

        /// <summary>
        /// Returns the performance counter set's help information.
        /// </summary>
        public string CategoryHelp
        {
            get { return categoryHelp ?? string.Empty; }
        }

        /// <summary>
        /// Releases all performance counters in the set including any 
        /// associated unmanaged data.
        /// </summary>
        public void Clear()
        {
            foreach (var counter in counters.Values)
            {
                counter.Close();
            }

            counters.Clear();
        }

        /// <summary>
        /// Assigns zero to all performance counters.
        /// </summary>
        public void Zero()
        {
            foreach (var counter in counters.Values)
            {
                counter.RawValue = 0;
            }
        }

        /// <summary>
        /// Adds the performance counter definition to the set.  The definitions
        /// will be indexed by counter name.
        /// </summary>
        /// <param name="counter">The counter definition.</param>
        /// <remarks>
        /// <note>
        /// It is OK to add a performance counter with the same name
        /// as an existing one.  In this case, the call will be ignored.
        /// </note>
        /// </remarks>
        public void Add(PerfCounter counter)
        {
            string      key = counter.Name;
            PerfCounter existing;

            if (counters.TryGetValue(key, out existing))
            {
                counter.Counter = existing.Counter;
                return;
            }

            counters.Add(key, counter);
        }

        /// <summary>
        /// Installs the set of performance counters to the system if necessary.
        /// </summary>
        /// <remarks>
        /// <note>
        /// The calling process must have sufficient rights to perform this operation.  If
        /// this is not the case, then this method will fail silently.
        /// </note>
        /// </remarks>
        public void Install()
        {
            if (!enabled || installed)
            {
                return;
            }

            // Install the counters as necessary.

            if (!PerfCounter.ProcessLocalOnly && !PerformanceCounterCategory.Exists(categoryName))
            {
                var creationData = new CounterCreationDataCollection();

                foreach (var counter in counters.Values.OrderBy(c => c.Name.ToLowerInvariant()))
                {
                    creationData.Add(new CounterCreationData(counter.Name, counter.Help, counter.Type));
                }

                try
                {
                    PerformanceCounterCategory.Create(categoryName, categoryHelp,
                                                      multiInstance ? PerformanceCounterCategoryType.MultiInstance : PerformanceCounterCategoryType.SingleInstance,
                                                      creationData);
                }
                catch (InvalidOperationException e)
                {
                    // We can see this error sometimes when multiple processes attempt
                    // to create the performance counters at the same time and one 
                    // attempt failes because the counters already exist.  We're going
                    // log and ignore this.

                    SysLog.LogException(e, "This can happen if more than one process or thread is trying to register performance counters at the same time.  This is probably not a problem.");
                }
                catch (SecurityException)
                {
                    SysLog.LogWarning("Process does not have sufficient rights to install performance counters.  Falling back to simulated local performance counters.");

                    installFailed = true;
                }
                catch (UnauthorizedAccessException)
                {
                    SysLog.LogWarning("Process does not have sufficient rights to install performance counters.  Falling back to simulated local performance counters.");

                    installFailed = true;
                }
                catch (Exception e)
                {
                    SysLog.LogException(e, "Process was unable to install performance counters.  Falling back to simulated local performance counters.");

                    installFailed = true;
                }
            }

            // Mark the set as installed.

            installed = true;
        }

        /// <summary>
        /// Removes the performance counters from the system if present.
        /// </summary>
        /// <remarks>
        /// <note>
        /// The calling process must have sufficient rights to perform this operation.  If
        /// this is not the case, then this method will fail silently.
        /// </note>
        /// </remarks>
        public void Uninstall()
        {
            foreach (var counter in counters.Values)
            {
                counter.Close();
            }

            if (!PerfCounter.ProcessLocalOnly && PerformanceCounterCategory.Exists(categoryName))
            {
                try
                {
                    PerformanceCounterCategory.Delete(categoryName);
                }
                catch (SecurityException)
                {
                    SysLog.LogWarning("Process does not have sufficient rights to uninstall performance counters.");
                    return;
                }
                catch (UnauthorizedAccessException)
                {
                    SysLog.LogWarning("Process does not have sufficient rights to uninstall performance counters.");
                    return;
                }
                catch (Exception e)
                {
                    SysLog.LogException(e, "Process was not able to uninstall performance counters.");
                    return;
                }
            }

            installed = false;
        }

        /// <summary>
        /// Adds performance counter installers for each counter represented in this set
        /// to the installer passed.
        /// </summary>
        /// <param name="installer">The target installer.</param>
        public void AddInstallers(System.Configuration.Install.Installer installer)
        {
            PerformanceCounterInstaller ctrInstaller;

            ctrInstaller = new PerformanceCounterInstaller();
            ctrInstaller.CategoryName = this.categoryName;
            ctrInstaller.CategoryHelp = this.categoryHelp;

            foreach (var counter in counters.Values)
            {
                ctrInstaller.Counters.Add(new CounterCreationData(counter.Name, counter.Help, counter.Type));
            }

            installer.Installers.Add(ctrInstaller);
        }

        /// <summary>
        /// Relates two performance counters in the set by specifying that any
        /// changes in the source counter should also be copied to the 
        /// target counter.
        /// </summary>
        /// <param name="source">Source counter.</param>
        /// <param name="target">Target counter.</param>
        public void Relate(string source, string target)
        {
            if (installed)  // Ignore this call if the counters have already
                return;     // been initialized

            PerfCounter     s, t;
            string[]        related;

            if (!counters.TryGetValue(source, out s))
            {
                throw new InvalidOperationException("Source counter does not exist.");
            }

            if (!counters.TryGetValue(target, out t))
            {
                throw new InvalidOperationException("Target counter does not exist.");
            }

            if (s.RelatedCounters == null)
            {
                s.RelatedCounters = new string[] { target };
            }
            else
            {
                related = new string[s.RelatedCounters.Length + 1];

                Array.Copy(s.RelatedCounters, 0, related, 0, related.Length - 1);

                related[related.Length - 1] = target;
                s.RelatedCounters           = related;
            }
        }

        /// <summary>
        /// Returns a single instance performance counter with the  
        /// name passed.  Note that the lookup is case insensitive and that 
        /// the method will create the underlying Windows performance counter 
        /// if it doesn't already exist.
        /// </summary>
        /// <param name="name">The performance counter name.</param>
        /// <remarks>
        /// <note>
        /// This method verifies that the performance counters
        /// are installed on the system and goes ahead and installs them
        /// if necessary.
        /// </note>
        /// </remarks>
        public PerfCounter this[string name]
        {
            get
            {
                PerfCounter counter;

                if (!installed)
                {
                    Install();
                }

                if (multiInstance)
                {
                    throw new InvalidOperationException("Counter set is configured as multi-instance so an instance name must be specified.");
                }

                if (!counters.TryGetValue(name, out counter))
                {
                    throw new ArgumentException("Counter does not exist.", "name");
                }

                if (!enabled)
                {
                    return counter;
                }

                try
                {
                    // Instantiate the performance counter if necessary.

                    if (counter.Counter == null && !installFailed && !PerfCounter.ProcessLocalOnly)
                    {
                        counter.Counter = new PerformanceCounter(categoryName, counter.Name, false);
                    }

                    // Associate any related counters

                    if (counter.RelatedCounters != null && counter.Related == null)
                    {
                        var             list = new List<PerfCounter>();
                        PerfCounter     related;
                        PerfCounter[]   relatedList;

                        for (int i = 0; i < counter.RelatedCounters.Length; i++)
                        {
                            related = this[counter.RelatedCounters[i]];

                            if (related != null)
                            {
                                list.Add(related);
                            }
                        }

                        relatedList = new PerfCounter[list.Count];
                        for (int i = 0; i < list.Count; i++)
                        {
                            relatedList[i] = list[i];
                        }

                        counter.Related = relatedList;
                    }
                }
                catch (Exception e)
                {
                    // I'm going to fail gracefully if the performance counter could not
                    // be created.  The PerfCounter class handles null counters by simply
                    // ignoring calls to the counter modification members.

                    counter.Counter = null;
                    SysLog.LogException(e);
                }

                return counter;
            }
        }

        /// <summary>
        /// Returns a named instance of a performance counter.  Note that the
        /// lookup is case insensitive and that the method will create the
        /// underlying Windows performance counter if it doesn't already exist.
        /// </summary>
        /// <param name="name">The performance counter name.</param>
        /// <param name="instance">The instance name (or <c>null</c>).</param>
        /// <remarks>
        /// <note>
        /// Passing <paramref name="instance" /> as <c>null</c> or the empty string is 
        /// equivalent to passing no instance parameter.
        /// </note>
        /// </remarks>
        public PerfCounter this[string name, string instance]
        {
            get
            {
                if (!multiInstance)
                {
                    throw new InvalidOperationException("Counter set is configured as single-instance so an instance name cannot be specified.");
                }

                if (instance == null || instance == string.Empty)
                {
                    throw new ArgumentException("Invalid instance name.");
                }

                PerfCounter     counter;
                PerfCounter     template;
                string          key = string.Format("{0}[{1}]", name, instance);

                if (!counters.TryGetValue(name, out template))
                {
                    throw new ArgumentException("Counter does not exist.", "name");
                }

                if (counters.TryGetValue(key, out counter))
                {
                    return counter;
                }

                try
                {
                    // Instantiate the performance counter and add it 
                    // to the set.

                    counter = new PerfCounter(template.Name, template.Help, instance, template.Type);
                    counters.Add(key, counter);

                    if (!enabled)
                    {
                        return counter;
                    }

                    counter.Counter = new PerformanceCounter(categoryName, template.Name, instance, false);

                    // Associate any related counters

                    if (template.RelatedCounters != null && counter.Related == null)
                    {
                        var             list = new List<PerfCounter>();
                        PerfCounter     related;
                        PerfCounter[]   relatedList;

                        for (int i = 0; i < template.RelatedCounters.Length; i++)
                        {
                            related = this[template.RelatedCounters[i], instance];
                            if (related != null)
                            {
                                list.Add(related);
                            }
                        }

                        relatedList = new PerfCounter[list.Count];
                        for (int i = 0; i < list.Count; i++)
                        {
                            relatedList[i] = list[i];
                        }

                        counter.Related = relatedList;
                    }
                }
                catch (Exception e)
                {
                    // I'm going to fail gracefully if the performance counter could not
                    // be created.  The PerfCounter class handles null counters by implementing
                    // a process local simulation.

                    counter.Counter = null;
                    SysLog.LogException(e);
                }

                return counter;
            }
        }

        /// <summary>
        /// Walks an assembly calling any performance counter definition methods found.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <param name="perfPrefix">Prefix for the counter names added (or <c>null</c>).</param>
        /// <remarks>
        /// <para>
        /// The method looks for static methods tagged with [PerfCounterDefinition] having
        /// the following signature:
        /// </para>
        /// <code language="cs">
        /// public static void PerfCounterDef(PerfCounterSet perfCounters,string perfPrefix);
        /// </code>        
        /// <para>
        /// Each method found will be called, passing this performance counter set
        /// instance and the counter name prefix.  The definition functions will add the 
        /// performance counters they know about to the set.
        /// </para>
        /// <para>
        /// This is a handy way of allowing performance counter initialization to be
        /// distributed across the assembly in the class implementations that expose 
        /// counters.
        /// </para>
        /// </remarks>
        public void DefineCounters(Assembly assembly, string perfPrefix)
        {
            Type[]          types;
            MethodInfo[]    methods;
            ParameterInfo[] args;

            types = assembly.GetTypes();
            foreach (Type type in types)
            {
                methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);

                foreach (MethodInfo method in methods)
                {
                    if (method.GetCustomAttributes(typeof(PerfCounterDefinitionAttribute), false).Length == 0)
                    {
                        continue;
                    }

                    args = method.GetParameters();

                    if (method.ReturnType != typeof(void) ||
                        args.Length != 2 ||
                        args[0].ParameterType != typeof(PerfCounterSet) ||
                        !args[0].IsIn && args[0].IsOut &&
                        args[1].ParameterType != typeof(string) ||
                        !args[1].IsIn && args[1].IsOut)
                    {
                        throw new InvalidOperationException(string.Format("Invalid counter definition method signature for [{0}.{1}()].", type.FullName, method.Name));
                    }

                    method.Invoke(null, new object[] { this, perfPrefix });
                }
            }
        }

        /// <summary>
        /// Walks an assembly calling any performance counter initialization methods found.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <param name="perfPrefix">Prefix for the counter names added (or <c>null</c>).</param>
        /// <remarks>
        /// <para>
        /// The method looks for static methods tagged with [PerfCounterLoad] having
        /// the following signature:
        /// </para>
        /// <code language="cs">
        /// public static void PerfCounterLoad(PerfCounterSet perfCounters,string perfPrefix);
        /// </code>        
        /// <para>
        /// Each method found will be called, passing this performance counter set
        /// instance and the counter name prefix.  The definition functions will load 
        /// and initializes its performance counters in preparation for feeding them 
        /// with performance information.
        /// </para>
        /// <para>
        /// This is a handy way of allowing performance counter initialization to be
        /// distributed across the assembly in the class implementations that expose 
        /// counters.
        /// </para>
        /// </remarks>
        public void LoadCounters(Assembly assembly, string perfPrefix)
        {
            Type[]          types;
            MethodInfo[]    methods;
            ParameterInfo[] args;

            types = assembly.GetTypes();

            foreach (Type type in types)
            {
                methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);

                foreach (MethodInfo method in methods)
                {
                    if (method.GetCustomAttributes(typeof(PerfCounterLoadAttribute), false).Length == 0)
                    {
                        continue;
                    }

                    args = method.GetParameters();

                    if (method.ReturnType != typeof(void) ||
                        args.Length != 2 ||
                        args[0].ParameterType != typeof(PerfCounterSet) ||
                        !args[0].IsIn && args[0].IsOut &&
                        args[1].ParameterType != typeof(string) ||
                        !args[1].IsIn && args[1].IsOut)
                    {
                        throw new InvalidOperationException(string.Format("Invalid counter load method signature for [{0}.{1}()].", type.FullName, method.Name));
                    }

                    method.Invoke(null, new object[] { this, perfPrefix });
                }
            }
        }

        /// <summary>
        /// Returns the list of performance counters held by the set.
        /// </summary>
        /// <returns>The list.</returns>
        public List<PerfCounter> ToList()
        {
            var list = new List<PerfCounter>(counters.Count);

            foreach (var counter in counters.Values)
            {
                list.Add(counter);
            }

            return list;
        }
    }
}
