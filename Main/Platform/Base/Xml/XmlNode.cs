//-----------------------------------------------------------------------------
// FILE:        XmlNode.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the class that holds the state of an XML node.

using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;

using LillTek.Common;

namespace LillTek.Xml
{
    /// <summary>
    /// Defines the class that holds the state of an XML node.
    /// </summary>
    [Obsolete("This is ancient (2005) code originally developed for WinCE devices.  Use Linq-to-XML instead.")]
    public sealed class XmlNode
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Parses an XML tree from a a string.
        /// </summary>
        /// <param name="text">The source XML text.</param>
        /// <returns>The root node of the parsed tree.</returns>
        public static XmlNode Parse(string text)
        {
            return new XmlNode(text, Stub.Param);
        }

        //---------------------------------------------------------------------
        // Instance members

        private string          name;               // Name of the node
        private XmlNode         parent;             // Parent node (or null if this is the root)
        private XmlPropArray    props;              // Node properties
        private XmlNodeArray    children;           // Child nodes
        private string          innerXml = null;    // XML text to render between the open/closing tags.

        /// <summary>
        /// This constructor initializes a node.
        /// </summary>
        public XmlNode()
        {
            this.parent   = null;
            this.props    = new XmlPropArray();
            this.children = new XmlNodeArray();
        }

        /// <summary>
        /// This constructor initalizes the node.
        /// </summary>
        /// <param name="name">The node's name.</param>
        public XmlNode(string name)
            : this()
        {
            this.name = name;
        }

        /// <summary>
        /// This constructor initializes the node.
        /// </summary>
        /// <param name="name">The node's name.</param>
        /// <param name="value">The node's text value.</param>
        public XmlNode(string name, string value)
            : this()
        {
            this.name = name;
            this.props.Add(new XmlProp("", value));
        }

        /// <summary>
        /// This method initializes the node and its subtree from the
        /// .NET <see cref="XmlElement" /> tree passed.
        /// </summary>
        /// <param name="tree">The source tree.</param>
        public XmlNode(XmlElement tree)
            : this()
        {
            Load(tree);
        }

        /// <summary>
        /// This constructor initializes the XML node from the byte array 
        /// and the encoding passed.
        /// </summary>
        /// <param name="buf">The source byte array.</param>
        /// <param name="encoding">The encoding.</param>
        public XmlNode(byte[] buf, Encoding encoding)
            : this(buf, 0, buf.Length, encoding)
        {
        }

        /// <summary>
        /// This constructor initializes the XML node from the specified
        /// range of bytes in the buffer and the encoding passed.
        /// </summary>
        /// <param name="buf">The source byte array.</param>
        /// <param name="index">Index of the first byte.</param>
        /// <param name="count">Number of bytes to process.</param>
        /// <param name="encoding">The encoding.</param>
        public XmlNode(byte[] buf, int index, int count, Encoding encoding)
            : this()
        {
            XmlDocument xmlDoc = new XmlDocument();
            string      xmlStr;

            xmlStr = encoding.GetString(buf, index, count);
            xmlDoc.Load(new StringReader(xmlStr));
            this.Load(xmlDoc.DocumentElement);
        }

        /// <summary>
        /// Constructs a node tree by parsing the text passed.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="stub">Stub parameter.</param>
        private XmlNode(string text, Stub stub)
            : this()
        {
            var xmlDoc = new XmlDocument();

            xmlDoc.Load(new StringReader(text));
            this.Load(xmlDoc.DocumentElement);
        }

        /// <summary>
        /// This method walks the element tree passed and initializes
        /// this node tree.
        /// </summary>
        /// <param name="tree">The source tree.</param>
        private void Load(XmlElement tree)
        {
            this.name = tree.Name;

            // Load the properties

            for (int i = 0; i < tree.Attributes.Count; i++)
            {
                var attr = tree.Attributes[i];

                this.props.Add(new XmlProp(attr.Name, attr.Value));
            }

            // Load the children and the node text

            for (int i = 0; i < tree.ChildNodes.Count; i++)
            {
                object      o = tree.ChildNodes[i];
                XmlElement  element;
                XmlText     text;

                element = o as XmlElement;
                if (element != null)
                {
                    this.children.Add(new XmlNode(element));
                    continue;
                }

                text = o as XmlText;
                if (text != null)
                {
                    this.props.Add(new XmlProp(string.Empty, text.Value));
                    continue;
                }
            }
        }

        /// <summary>
        /// This property accesses the node's name.
        /// </summary>
        public string Name
        {
            get { return this.name; }
            set { this.name = value; }
        }

        /// <summary>
        /// This property accesses the node's text value.  This is a shortcut
        /// for accessing Properties[""].
        /// </summary>
        public string Value
        {
            get
            {
                var prop = props[string.Empty];

                if (prop == null)
                    return string.Empty;

                return prop.Value;
            }

            set
            {
                var prop = props[string.Empty];

                if (prop == null)
                    props.Add(new XmlProp(string.Empty, value));
                else
                    prop.Value = value;
            }
        }

        /// <summary>
        /// This property access's the node's parent.  Set this property to <c>null</c>
        /// if this node is the root.
        /// </summary>
        public XmlNode Parent
        {

            get { return this.parent; }
            set { this.parent = value; }
        }

        /// <summary>
        /// This property sets the XML text to write between the beginning
        /// and ending tags of this node.  If this is not <c>null</c>, then this
        /// property will be written instead of walking the child nodes.
        /// </summary>
        public string InnerXml
        {
            get { return innerXml; }
            set { innerXml = value; }
        }

        /// <summary>
        /// This method generates the inner XML text.  This method differs from
        /// the <see cref="InnerXml" /> property by actually walking the child nodes if 
        /// <see cref="InnerXml" /> is <c>null</c>.
        /// </summary>
        /// <returns>The Inner XML for this node.</returns>
        public string GetInnerXml()
        {
            if (innerXml != null)
                return innerXml;
            else if (children.Count == 0)
                return this.Value;

            var sb     = new StringBuilder();
            var writer = new XmlTextWriter(new StringWriter(sb));

            writer.Formatting = Formatting.None;

            for (int i = 0; i < children.Count; i++)
                children[i].Save(writer);

            writer.Close();
            return sb.ToString();
        }

        /// <summary>
        /// This method writes the node to the text writer passed.  Pass 
        /// fFormat=<c>true</c> if the output is to be formatted into human readable 
        /// form.
        /// </summary>
        /// <param name="output">The output text writer.</param>
        /// <param name="fFormat"><c>true</c> to format the output.</param>
        public void Save(TextWriter output, bool fFormat)
        {
            var writer = new XmlTextWriter(output);

            if (fFormat)
            {
                writer.Formatting = Formatting.Indented;
                writer.Indentation = 1;
                writer.IndentChar = '\t';
            }
            else
                writer.Formatting = Formatting.None;

            Save(writer);
        }

        /// <summary>
        /// This method recursively writes the node to the XML writer passed.
        /// </summary>
        /// <param name="writer">The output writer.</param>
        private void Save(XmlTextWriter writer)
        {
            var text = string.Empty;

            writer.WriteStartElement(this.name);

            for (int i = 0; i < props.Count; i++)
                if (props[i].Name == string.Empty)
                    text = props[i].Value;
                else
                    writer.WriteAttributeString(props[i].Name, this.props[i].Value);

            if (innerXml == null)
            {
                if (children.Count == 0)
                    writer.WriteString(text);
                else
                    for (int i = 0; i < children.Count; i++)
                        children[i].Save(writer);
            }
            else
                writer.WriteRaw(innerXml);

            writer.WriteEndElement();
        }

        /// <summary>
        /// This method renders the node tree into a byte array using the 
        /// specified encoding.
        /// </summary>
        /// <param name="encoding">The encoding.</param>
        /// <returns>The encoded byte array.</returns>
        public byte[] Encode(Encoding encoding)
        {
            var sb     = new StringBuilder();
            var writer = new StringWriter(sb);

            Save(writer, false);
            writer.Close();
            return encoding.GetBytes(sb.ToString());
        }

        /// <summary>
        /// This property returns the collection of child nodes.
        /// </summary>
        public XmlNodeArray Children
        {
            get { return this.children; }
        }

        /// <summary>
        /// This property returns the collection of properties.
        /// </summary>
        public XmlPropArray Properties
        {
            get { return this.props; }
        }

        /// <summary>
        /// This method adds the property/value pair specified to the node's property collection.
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <param name="value">The property value.</param>
        public void AddProp(string name, string value)
        {
            props.Add(new XmlProp(name, value));
        }

        /// <summary>
        /// This method adds the property/value pair specified to the node's property collection.
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <param name="value">The property value.</param>
        public void AddProp(string name, int value)
        {
            props.Add(new XmlProp(name, value.ToString()));
        }

        /// <summary>
        /// This method adds the property/value pair specified to the node's property collection.
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <param name="value">The property value.</param>
        public void AddProp(string name, uint value)
        {
            props.Add(new XmlProp(name, value.ToString()));
        }

        /// <summary>
        /// This method adds the property/value pair specified to the node's property collection.
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <param name="value">The property value.</param>
        public void AddProp(string name, ulong value)
        {
            props.Add(new XmlProp(name, value.ToString()));
        }

        /// <summary>
        /// This method adds the property/value pair specified to the node's property collection.
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <param name="value">The property value.</param>
        public void AddProp(string name, long value)
        {
            props.Add(new XmlProp(name, value.ToString()));
        }

        /// <summary>
        /// This method appends the node passed to the set of child nodes.  Note that ownership of
        /// the node will pass to this node.
        /// </summary>
        /// <param name="node">The node to add.</param>
        public void AddChild(XmlNode node)
        {
            children.Add(node);
        }

        /// <summary>
        /// This method is a short-cuts that create a child node with the name passed and
        /// then adds the value passed as the content property.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        public void AddChild(string name, string value)
        {
            children.Add(new XmlNode(name, value));
        }

        /// <summary>
        /// This method is a short-cuts that create a child node with the name passed and
        /// then adds the value passed as the content property.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        public void AddChild(string name, int value)
        {
            children.Add(new XmlNode(name, value.ToString()));
        }

        /// <summary>
        /// This method is a short-cuts that create a child node with the name passed and
        /// then adds the value passed as the content property.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        public void AddChild(string name, uint value)
        {
            children.Add(new XmlNode(name, value.ToString()));
        }

        /// <summary>
        /// This method is a short-cuts that create a child node with the name passed and
        /// then adds the value passed as the content property.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        public void AddChild(string name, ulong value)
        {
            children.Add(new XmlNode(name, value.ToString()));
        }

        /// <summary>
        /// This method is a short-cuts that create a child node with the name passed and
        /// then adds the value passed as the content property.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        public void AddChild(string name, long value)
        {
            children.Add(new XmlNode(name, value.ToString()));
        }

        /// <summary>
        /// This method is a short-cuts that create a child node with the name passed and
        /// then adds the value passed as the content property.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        public void AddChild(string name, DateTime value)
        {
            children.Add(new XmlNode(name, value.Ticks.ToString()));
        }

        /// <summary>
        /// This method searches the child nodes for nodes with the 
        /// name passed.  The method returns an array containing
        /// any such nodes found.
        /// </summary>
        /// <param name="name">The desired node name.</param>
        public XmlNodeArray FindChildren(string name)
        {
            var list = new XmlNodeArray();

            for (int i = 0; i < children.Count; i++)
                if (children[i].Name == name)
                    list.Add(children[i]);

            return list;
        }

        /// <summary>
        /// This method searches for the child node with the name passed and
        /// then returns the value of the node's default property as a string.
        /// The method throws an exception if the child node does not exist.
        /// </summary>
        /// <param name="name">Name of the child node desired.</param>
        public string GetChildString(string name)
        {
            for (int i = 0; i < children.Count; i++)
                if (children[i].Name == name)
                    return children[i].Value;

            throw new Exception(string.Format("Child node <{0}> not found.", name));
        }

        /// <summary>
        /// This method searches for the child node with the name passed and
        /// then returns the value of the node's default property into the
        /// reference property passed.
        /// </summary>
        /// <param name="name">Name of the child node desired.</param>
        /// <param name="value">Output value.</param>
        /// <returns>
        /// The method returns <c>true</c> on success,
        /// <c>false</c> if the child node does not exist or if the value is
        /// not formatted properly for the type of property desired.
        /// </returns>
        public bool GetChildValue(string name, out string value)
        {
            for (int i = 0; i < children.Count; i++)
                if (children[i].Name == name)
                {
                    value = children[i].Value;
                    return true;
                }

            value = string.Empty;
            return false;
        }

        /// <summary>
        /// This method searches for the child node with the name passed and
        /// then returns the value of the node's default property into the
        /// reference property passed.
        /// </summary>
        /// <param name="name">Name of the child node desired.</param>
        /// <param name="value">Output value.</param>
        /// <returns>
        /// The method returns <c>true</c> on success, <c>false</c> if the 
        /// child node does not exist or if the value is not formatted properly 
        /// for the type of property desired.
        /// </returns>
        public bool GetChildValue(string name, out int value)
        {
            string str;

            if (!GetChildValue(name, out str))
            {
                value = 0;
                return false;
            }

            try
            {
                value = int.Parse(str);
                return true;
            }
            catch
            {
                value = 0;
                return false;
            }
        }

        /// <summary>
        /// This method searches for the child node with the name passed and
        /// then returns the value of the node's default property into the
        /// reference property passed.
        /// </summary>
        /// <param name="name">Name of the child node desired.</param>
        /// <param name="value">Output value.</param>
        /// <returns>
        /// The method returns <c>true</c> on success, <c>false</c> if the 
        /// child node does not exist or if the value is not formatted properly 
        /// for the type of property desired.
        /// </returns>
        public bool GetChildValue(string name, out bool value)
        {
            string str;

            if (!GetChildValue(name, out str))
            {
                value = false;
                return false;
            }

            try
            {
                switch (str.ToLowerInvariant())
                {

                    case "true":
                    case "1":
                    case "yes":

                        value = true;
                        break;

                    default:

                        value = false;
                        break;
                }

                return true;
            }
            catch
            {
                value = false;
                return false;
            }
        }

        /// <summary>
        /// This method searches for the child node with the name passed and
        /// then returns the value of the node's default property into the
        /// reference property passed.
        /// </summary>
        /// <param name="name">Name of the child node desired.</param>
        /// <param name="value">Output value.</param>
        /// <returns>
        /// The method returns <c>true</c> on success, <c>false</c> if the 
        /// child node does not exist or if the value is not formatted properly 
        /// for the type of property desired.
        /// </returns>
        public bool GetChildValue(string name, out ulong value)
        {
            string str;

            if (!GetChildValue(name, out str))
            {
                value = 0;
                return false;
            }

            try
            {
                value = ulong.Parse(str);
                return true;
            }
            catch
            {
                value = 0;
                return false;
            }
        }

        /// <summary>
        /// This method searches for the child node with the name passed and
        /// then returns the value of the node's default property into the
        /// reference property passed.
        /// </summary>
        /// <param name="name">Name of the child node desired.</param>
        /// <param name="value">Output value.</param>
        /// <returns>
        /// The method returns <c>true</c> on success, <c>false</c> if the 
        /// child node does not exist or if the value is not formatted properly 
        /// for the type of property desired.
        /// </returns>
        public bool GetChildValue(string name, out long value)
        {
            string str;

            if (!GetChildValue(name, out str))
            {
                value = 0;
                return false;
            }

            try
            {
                value = long.Parse(str);
                return true;
            }
            catch
            {
                value = 0;
                return false;
            }
        }

        /// <summary>
        /// This method searches for the child node with the name passed and
        /// then returns the value of the node's default property into the
        /// reference property passed.
        /// </summary>
        /// <param name="name">Name of the child node desired.</param>
        /// <param name="value">Output value.</param>
        /// <returns>
        /// The method returns <c>true</c> on success, <c>false</c> if the 
        /// child node does not exist or if the value is not formatted properly 
        /// for the type of property desired.
        /// </returns>
        public bool GetChildValue(string name, out DateTime value)
        {
            ulong ticks;

            if (!GetChildValue(name, out ticks))
            {
                value = new DateTime(0);
                return false;
            }

            value = new DateTime((long)ticks);
            return true;
        }

        /// <summary>
        /// This method splits the node/prop path passed into two components: the path to the node
        /// and the property name.
        /// </summary>
        /// <param name="path">The path to split.</param>
        /// <param name="nodePath">Will be set to the path extracted.</param>
        /// <param name="propName">Will be set to the property extracted.</param>
        private void SplitPath(string path, out string nodePath, out string propName)
        {
            int posSep;

            if (path.Length == 0)
            {
                nodePath = ".";
                propName = "";
                return;
            }

            if (path[0] == '/')
            {
                posSep = path.LastIndexOf('/', path.Length - 1, path.Length - 1);
                if (posSep == -1)
                {
                    nodePath = "/";
                    propName = path.Substring(1);
                }
                else
                {
                    nodePath = path.Substring(0, posSep);
                    propName = path.Substring(posSep + 1);
                }
            }
            else
            {
                posSep = path.LastIndexOf('/');
                if (posSep == -1)
                {
                    nodePath = ".";
                    propName = path;
                }
                else
                {
                    nodePath = path.Substring(0, posSep);
                    propName = path.Substring(posSep + 1);
                }
            }
        }

        /// <summary>
        /// This method searches the node tree (rooted at this node) for the specified subnode.  A heirarchy of 
        /// node names can be specified much like directories in a file system.
        /// </summary>
        /// <param name="path">Path to the node.</param>
        /// <remarks>
        /// For example: "/" specifies the  root of the tree containing the current node, 
        /// ".." specifies the parent of this node, and "." specifies this node.  You can 
        /// also specify names of nodes as in: "./Foo/Bar" or "../Foo" or "/Bar/FooBar".  Note
        /// that the use of "." to specifiy this node is optional: "./Foo" is equivalent to "Foo".  
        /// This is basically a very poor man's XPATH.  The method returns a pointer to the 
        /// specified node if found, <c>null</c> otherwise.
        /// </remarks>
        public XmlNode GetNode(string path)
        {
            XmlNode     node;
            int         pos;
            int         posSep;
            string      strRel;

            pos = 0;
            if (path[pos] == '/')
            {
                // Scan upward in the tree for the root

                node = this;
                while (node.Parent != null)
                    node = node.Parent;

                pos++;
            }
            else
                node = this;

            // Split the path up into relative paths delimited by '/' characters
            // and walk the tree from the current node.

            while (pos < path.Length && node != null)
            {
                posSep = path.IndexOf('/', pos);
                if (posSep == -1)
                    strRel = path.Substring(pos);
                else
                    strRel = path.Substring(pos, posSep - pos);

                if (strRel == "..")
                    node = node.Parent;                     // parent node
                else if (strRel != ".")
                    node = node.Children[strRel];           // child node

                if (posSep == -1 || node == null)
                    break;
                else
                    pos = posSep + 1;
            }

            return node;
        }

        //-----------------------------------------------------------------------------------------
        // These methods search the node tree for a named property and then attempt to returns it
        // as a string, integer, or boolean.  Each method accepts a node path as is described 
        // in the comment for GetNode().  The path up to but not including the last item specifies the
        // node and then the last item specifies the desired property of the node.  For example, "./Foo"
        // specifies the "Foo" property of this node and "Child1/Foo" specifies the "Foo" property of
        // the "Child1" node.  Each method accepts a default parameter.  If the method cannot find
        // the node or property or is unable to parse the property value, then the method will return
        // the default value instead.
        //
        // Finally, the contents of a tag are stored as a property named by the empty string ("").  So,
        // to get the contents of a node via these methods, simply append a "/" to the path.  For
        // example, you use GetPropStr("/child/") to get the value "abcd" from the XML below:
        //
        //      <root><child>abcd</child></root>

        /// <summary>
        /// This method returns the property at the path specified.
        /// </summary>
        /// <param name="path">The property path.</param>
        /// <returns>The property value or <c>null</c>.</returns>
        public string this[string path]
        {
            get { return GetPropStr(path, (string)null); }
        }

        /// <summary>
        /// This method returns the property at the path specified.
        /// </summary>
        /// <param name="path">The property path.</param>
        /// <param name="strDef">The default value.</param>
        /// <returns>The property value.</returns>
        public string GetPropStr(string path, string strDef)
        {
            XmlNode     node;
            XmlProp      prop;
            string      strNode;
            string      strProp;

            SplitPath(path, out strNode, out strProp);
            node = GetNode(strNode);
            if (node == null)
                return strDef;

            prop = node.Properties[strProp];
            if (prop == null)
                return strDef;

            return prop.Value;
        }

        /// <summary>
        /// This method returns the property at the path specified.
        /// </summary>
        /// <param name="path">The property path.</param>
        /// <returns>The property value or <c>null</c>.</returns>
        public string GetPropStr(string path)
        {
            return GetPropStr(path, string.Empty);
        }

        /// <summary>
        /// This method returns the property at the path specified.
        /// </summary>
        /// <param name="path">The property path.</param>
        /// <param name="iDef">The default value.</param>
        /// <returns>The property value.</returns>
        public int GetPropInt(string path, int iDef)
        {
            XmlNode     node;
            XmlProp     prop;
            string      strNode;
            string      strProp;

            SplitPath(path, out strNode, out strProp);
            node = GetNode(strNode);
            if (node == null)
                return iDef;

            prop = node.Properties[strProp];
            if (prop == null)
                return iDef;

            try
            {
                return int.Parse(prop.Value);
            }
            catch
            {
                return iDef;
            }
        }

        /// <summary>
        /// This method returns the property at the path specified.
        /// </summary>
        /// <param name="path">The property path.</param>
        /// <returns>The property value.</returns>
        public int GetPropInt(string path)
        {
            return GetPropInt(path, -1);
        }

        /// <summary>
        /// This method returns the property at the path specified.
        /// </summary>
        /// <param name="path">The property path.</param>
        /// <param name="dwDef">The default value.</param>
        /// <returns>The property value.</returns>
        public uint GetPropDWORD(string path, uint dwDef)
        {
            XmlNode     node;
            XmlProp     prop;
            string      strNode;
            string      strProp;

            SplitPath(path, out strNode, out strProp);
            node = GetNode(strNode);
            if (node == null)
                return dwDef;

            prop = node.Properties[strProp];
            if (prop == null)
                return dwDef;

            try
            {
                return uint.Parse(prop.Value);
            }
            catch
            {
                return dwDef;
            }
        }

        /// <summary>
        /// This method returns the property at the path specified.
        /// </summary>
        /// <param name="path">The property path.</param>
        /// <returns>The property value.</returns>
        public uint GetPropDWORD(string path)
        {
            return GetPropDWORD(path, 0xFFFFFFFF);
        }

        /// <summary>
        /// This method returns the property at the path specified.
        /// </summary>
        /// <param name="path">The property path.</param>
        /// <param name="dwDef">The default value.</param>
        /// <returns>The property value.</returns>
        public ulong GetPropDWORDLONG(string path, ulong dwDef)
        {
            XmlNode     node;
            XmlProp     prop;
            string      strNode;
            string      strProp;

            SplitPath(path, out strNode, out strProp);
            node = GetNode(strNode);
            if (node == null)
                return dwDef;

            prop = node.Properties[strProp];
            if (prop == null)
                return dwDef;

            try
            {
                return ulong.Parse(prop.Value);
            }
            catch
            {
                return dwDef;
            }

        }

        /// <summary>
        /// This method returns the property at the path specified.
        /// </summary>
        /// <param name="path">The property path.</param>
        /// <returns>The property value.</returns>
        public ulong GetPropDWORDLONG(string path)
        {
            return GetPropDWORDLONG(path, 0xFFFFFFFFFFFFFFFF);
        }

        /// <summary>
        /// This method returns the property at the path specified.
        /// </summary>
        /// <param name="path">The property path.</param>
        /// <param name="fDef">The default value.</param>
        /// <returns>The property value.</returns>
        public bool GetPropBool(string path, bool fDef)
        {

            XmlNode     node;
            XmlProp     prop;
            string      strNode;
            string      strProp;
            bool        v;

            SplitPath(path, out strNode, out strProp);
            node = GetNode(strNode);
            if (node == null)
                return fDef;

            prop = node.Properties[strProp];
            if (prop == null)
                return fDef;

            if (!XmlProp.Get(out v, prop.Value))
                return fDef;

            return v;
        }

        /// <summary>
        /// This method returns the property at the path specified.
        /// </summary>
        /// <param name="path">The property path.</param>
        /// <returns>The property value.</returns>
        public bool GetPropBool(string path)
        {
            return GetPropBool(path, false);
        }

        /// <summary>
        /// This method returns the property at the path specified.
        /// </summary>
        /// <param name="path">The property path.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The property value.</returns>
        public XmlProp GetPropObj(string path, XmlProp def)
        {
            XmlNode     node;
            XmlProp     prop;
            string      strNode;
            string      strProp;

            SplitPath(path, out strNode, out strProp);
            node = GetNode(strNode);
            if (node == null)
                return def;

            prop = node.Properties[strProp];
            if (prop == null)
                return def;

            return prop;
        }

        /// <summary>
        /// This method returns the property at the path specified.
        /// </summary>
        /// <param name="path">The property path.</param>
        /// <returns>The property value or <c>null</c>.</returns>
        public XmlProp GetPropObj(string path)
        {
            return GetPropObj(path, null);
        }

        /// <summary>
        /// This method parses the XML encoded in the buffer passed and then
        /// dumps it out to the debug console as formatted text.
        /// </summary>
        /// <param name="title">Title of the dump.</param>
        /// <param name="buf">The buffer.</param>
        /// <param name="encoding">The encoding.</param>
        public static void Dump(string title, byte[] buf, Encoding encoding)
        {
#if DEBUG
            var node   = new XmlNode(buf, encoding);
            var sb     = new StringBuilder();
            var writer = new StringWriter(sb);

            node.Save(writer, true);
            writer.Flush();

            if (title != string.Empty)
                Debug.WriteLine(title);

            Debug.WriteLine(sb.ToString());
#endif
        }

        /// <summary>
        /// This method renders the XML node and its children as a formatted string.
        /// </summary>
        /// <returns>The XML string.</returns>
        public override string ToString()
        {
            return ToString(true);
        }

        /// <summary>
        /// This method renders the XML node and its children as a string.
        /// </summary>
        /// <param name="formatted">Controls whether the string is formatted or not.</param>
        /// <returns>The XML string.</returns>
        public string ToString(bool formatted)
        {
            var sb     = new StringBuilder();
            var writer = new StringWriter(sb);

            Save(writer, formatted);
            writer.Flush();
            return sb.ToString();
        }
    }
}
