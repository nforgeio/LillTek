//-----------------------------------------------------------------------------
// FILE:        DefaultStyle.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Holds the XAML for an individual custom control's default style.

// Adapted from samples from Microsoft:
//
// (c) Copyright Microsoft Corporation.
//
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace LillTek.Tools.VSTasks
{
    /// <summary>
    /// DefaultStyle represents the XAML of an individual Control's default
    /// style (in particular its ControlTemplate) which can be merged with other
    /// default styles).  The XAML must have a ResourceDictionary as its root
    /// element and be marked with a DefaultStyle build action in Visual Studio.
    /// </summary>
    public partial class DefaultStyle
    {
        //---------------------------------------------------------------------
        // Private types

        public class ElementInfo
        {
            /// <summary>
            /// Used to track the order in which the element was encountered so
            /// that we can make sure we output the elements in the same order
            /// to maintain dependency constraints.
            /// </summary>
            public readonly int Order;

            /// <summary>
            /// The XAML element.
            /// </summary>
            public readonly XElement Element;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="element">The XAML element.</param>
            /// <param name="order">The order of the element.</param>
            public ElementInfo(XElement element, int order)
            {
                this.Element = element;
                this.Order = order;
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        /// <summary>
        /// Root element of both the default styles and the merged generic.xaml.
        /// </summary>
        private const string RootElement = "ResourceDictionary";

        /// <summary>
        /// Gets or sets the file path of the default style.
        /// </summary>
        public string DefaultStylePath { get; set; }

        /// <summary>
        /// Gets the namespaces imposed on the root element of a default style
        /// (including explicitly declared namespaces as well as those inherited
        /// from the root ResourceDictionary element).
        /// </summary>
        public SortedDictionary<string, string> Namespaces { get; private set; }

        /// <summary>
        /// Gets the elements in the XAML that include both styles and shared
        /// resources.
        /// </summary>
        public SortedDictionary<string, ElementInfo> Resources { get; private set; }

        /// <summary>
        /// Gets or sets the history tracking which resources originated from
        /// which files.
        /// </summary>
        private Dictionary<string, string> MergeHistory { get; set; }

        /// <summary>
        /// Initializes a new instance of the DefaultStyle class.
        /// </summary>
        protected DefaultStyle()
        {
            Namespaces   = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Resources    = new SortedDictionary<string, ElementInfo>(StringComparer.OrdinalIgnoreCase);
            MergeHistory = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Load a DefaultStyle from the a project item.
        /// </summary>
        /// <param name="path">
        /// Path of the default style which is used for reporting errors.
        /// </param>
        /// <returns>The DefaultStyle.</returns>
        public static DefaultStyle Load(string path)
        {
            var style = new DefaultStyle();
            var xaml  = File.ReadAllText(path);
            var root  = XElement.Parse(xaml, LoadOptions.PreserveWhitespace);

            style.DefaultStylePath = path;

            if (root.Name.LocalName == RootElement)
            {
                // Get the namespaces

                foreach (XAttribute attribute in root.Attributes())
                {
                    if (attribute.Name.LocalName == "xmlns")
                        style.Namespaces.Add("", attribute.Value);
                    else if (attribute.Name.NamespaceName == XNamespace.Xmlns.NamespaceName)
                        style.Namespaces.Add(attribute.Name.LocalName, attribute.Value);
                }

                // Get the styles and shared resources
                foreach (XElement element in root.Elements())
                {
                    string      name;
                    string      targetType;
                    string      elementName;
                    string      elementKey;

                    name = string.Empty;

                    if (element.Name.LocalName == "Style")
                    {
                        targetType  = GetAttribute(element, "TargetType");
                        elementName = GetAttribute(element, "Name");
                        elementKey  = GetAttribute(element, "Key");

                        name = "Style(";
                        if (targetType != null)
                            name += "TargetType=" + targetType;

                        if (elementName != null)
                        {
                            if (targetType != null)
                                name += ",";

                            name += "Name=" + name;
                        }

                        if (elementKey != null)
                        {
                            if (targetType != null || elementName != null)
                                name += ",";

                            name += "Key=" + elementKey;
                        }

                        name += ")";
                    }
                    else
                    {
                        elementName = GetAttribute(element, "Name");
                        elementKey = GetAttribute(element, "Key");

                        if (elementName != null)
                            name += "Name=" + name;

                        if (elementKey != null)
                        {
                            if (elementName != null)
                                name += ",";

                            name += "Key=" + elementKey;
                        }
                    }

                    if (style.Resources.ContainsKey(name))
                    {
                        throw new InvalidOperationException(string.Format(
                            CultureInfo.InvariantCulture,
                            "Resource \"{0}\" is defined multiple times in: {1}",
                            name,
                            path));
                    }

                    style.Resources.Add(name, new ElementInfo(element, MergeDefaultStylesTask.Order++));
                    style.MergeHistory[name] = path;
                }
            }

            return style;
        }

        /// <summary>
        /// Get the value of the first attribute that is defined.
        /// </summary>
        /// <param name="element">Element with the attributes defined.</param>
        /// <param name="attributes">
        /// Local names of the attributes to find.
        /// </param>
        /// <returns>Value of the first attribute found.</returns>
        private static string GetAttribute(XElement element, params string[] attributes)
        {
            foreach (string name in attributes)
            {
                string value =
                    (from a in element.Attributes()
                     where a.Name.LocalName == name
                     select a.Value)
                     .FirstOrDefault();

                if (value != null)
                    return value;
            }
            return null;
        }

        /// <summary>
        /// Merge a sequence of DefaultStyles into a single style.
        /// </summary>
        /// <param name="styles">Sequence of DefaultStyles.</param>
        /// <returns>Merged DefaultStyle.</returns>
        public static DefaultStyle Merge(IEnumerable<DefaultStyle> styles)
        {
            var combined = new DefaultStyle();

            if (styles != null)
            {
                foreach (DefaultStyle style in styles)
                    combined.Merge(style);
            }

            return combined;
        }

        /// <summary>
        /// Merge with another DefaultStyle.
        /// </summary>
        /// <param name="other">Other DefaultStyle to merge.</param>
        private void Merge(DefaultStyle other)
        {
            // Merge or lower namespaces
            foreach (KeyValuePair<string, string> ns in other.Namespaces)
            {
                string value = null;

                if (!Namespaces.TryGetValue(ns.Key, out value))
                    Namespaces.Add(ns.Key, ns.Value);
                else if (value != ns.Value)
                    other.LowerNamespace(ns.Key);
            }

            // Merge the resources
            foreach (KeyValuePair<string, ElementInfo> resource in other.Resources)
            {
                if (Resources.ContainsKey(resource.Key))
                {
                    throw new InvalidOperationException(string.Format(
                        CultureInfo.InvariantCulture,
                        "Resource \"{0}\" is used by both {1} and {2}!",
                        resource.Key,
                        MergeHistory[resource.Key],
                        other.DefaultStylePath));
                }

                Resources[resource.Key] = resource.Value;
                MergeHistory[resource.Key] = other.DefaultStylePath;
            }
        }

        /// <summary>
        /// Lower a namespace from the root ResourceDictionary to its child
        /// resources.
        /// </summary>
        /// <param name="prefix">Prefix of the namespace to lower.</param>
        private void LowerNamespace(string prefix)
        {
            // Get the value of the namespace
            string @namespace;

            if (!Namespaces.TryGetValue(prefix, out @namespace))
                return;

            // Push the value into each resource
            foreach (var resource in Resources)
            {
                // Don't push the value down if it was overridden locally or if
                // it's the default namespace (as it will be lowered automatically)
                if (((from e in resource.Value.Element.Attributes()
                      where e.Name.LocalName == prefix
                      select e).Count() == 0) &&
                    !string.IsNullOrEmpty(prefix))
                {
                    resource.Value.Element.Add(new XAttribute(XName.Get(prefix, XNamespace.Xmlns.NamespaceName), @namespace));
                }
            }
        }

        /// <summary>
        /// Generate the XAML markup for the default style.
        /// </summary>
        /// <returns>Generated XAML markup.</returns>
        public string GenerateXaml()
        {
            // Create the ResourceDictionary
            string defaultNamespace = XNamespace.Xml.NamespaceName;
            Namespaces.TryGetValue("", out defaultNamespace);
            XElement resources = new XElement(XName.Get(RootElement, defaultNamespace));

            // Add the shared namespaces
            foreach (KeyValuePair<string, string> @namespace in Namespaces)
            {
                // The default namespace will be added automatically
                if (string.IsNullOrEmpty(@namespace.Key))
                    continue;

                resources.Add(new XAttribute(
                    XName.Get(@namespace.Key, XNamespace.Xmlns.NamespaceName),
                    @namespace.Value));
            }

            // Add the resources

            foreach (KeyValuePair<string, ElementInfo> element in Resources.OrderBy((e) => e.Value.Order))
            {
                resources.Add(
                    new XText(Environment.NewLine + Environment.NewLine + "    "),
                    new XComment("  " + element.Key + "  "),
                    new XText(Environment.NewLine + "    "),
                    element.Value.Element);
            }

            resources.Add(new XText(Environment.NewLine + Environment.NewLine));

            // Create the document
            XDocument document = new XDocument(
                // TODO: Pull this copyright header from some shared location
                new XComment(Environment.NewLine +
                    "// (c) Copyright Microsoft Corporation." + Environment.NewLine +
                    "// This source is subject to the Microsoft Public License (Ms-PL)." + Environment.NewLine +
                    "// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details." + Environment.NewLine +
                    "// All other rights reserved." + Environment.NewLine +
                    Environment.NewLine +
                    "// Modified and extended by Jeffrey Lill." + Environment.NewLine +
                    "// (c) Copyright 2005-2012 by Jeffrey Lill. All other rights reserved." + Environment.NewLine),
                new XText(Environment.NewLine + Environment.NewLine),
                new XComment(Environment.NewLine +
                    "// WARNING:" + Environment.NewLine +
                    "// " + Environment.NewLine +
                    "// This XAML was automatically generated by merging the individual default control" + Environment.NewLine +
                    "// styles.  Changes to this file may cause incorrect behavior and will be lost" + Environment.NewLine +
                    "// if the XAML is regenerated." + Environment.NewLine),
                new XText(Environment.NewLine + Environment.NewLine),
                resources);

            return document.ToString();
        }

        /// <summary>
        /// Generate the XAML markup for the default style.
        /// </summary>
        /// <returns>Generated XAML markup.</returns>
        public override string ToString()
        {
            return GenerateXaml();
        }
    }
}
