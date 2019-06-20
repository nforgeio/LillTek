//-----------------------------------------------------------------------------
// FILE:        ScriptPlaceHolder.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: A container that web server applications can use to to generate
//              custom client side scripts.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Hosting;
using System.Web.UI.WebControls;
using System.Xml;
using System.Xml.Linq;

using LillTek.Common;
using LillTek.Service;

namespace LillTek.Web
{
    /// <summary>
    /// A container that web server applications can use to to generate
    /// custom client side scripts.
    /// </summary>
    public class ScriptPlaceHolder
    {
        private List<string>                scriptRefs;
        private Dictionary<string, object>  variables;
        private StringBuilder               sbMain;
        private bool                        inFunction;

        /// <summary>
        /// Constructor.
        /// </summary>
        public ScriptPlaceHolder()
        {
            this.scriptRefs = new List<string>();
            this.variables  = new Dictionary<string, object>();
            this.sbMain     = new StringBuilder(2048);
            this.inFunction = false;
        }

        /// <summary>
        /// Adds a script reference.
        /// </summary>
        /// <param name="uri">Source URI for the javascript.</param>
        public void AddScriptReference(string uri)
        {
            scriptRefs.Add(uri);
        }

        /// <summary>
        /// Sets a global script variable to a value.
        /// </summary>
        /// <param name="name">The variable name.</param>
        /// <param name="value">The value.</param>
        public void Var(string name, object value)
        {
            this.variables[name] = value;
        }

        /// <summary>
        /// Sets a global script variable to a formatted value.
        /// </summary>
        /// <param name="name">The variable name.</param>
        /// <param name="format">The format string.</param>
        /// <param name="args">The arguments.</param>
        public void Var(string name, string format, params object[] args)
        {
            Var(name, (object)string.Format(format, args));
        }

        /// <summary>
        /// Sets a global script variable to a literal value.
        /// </summary>
        /// <param name="name">The variable name.</param>
        /// <param name="value">The value.</param>
        public void VarLiteral(string name, string value)
        {
            this.variables[name] = new ScriptLiteral(value);
        }

        /// <summary>
        /// Sets a global script variable to a literal formatted value.
        /// </summary>
        /// <param name="name">The variable name.</param>
        /// <param name="format">The format string.</param>
        /// <param name="args">The arguments.</param>
        public void VarLiteral(string name, string format, params object[] args)
        {
            VarLiteral(name, string.Format(format, args));
        }

        /// <summary>
        /// Appends the text passed to the script.
        /// </summary>
        /// <param name="text">The text.</param>
        public void Append(string text)
        {
            sbMain.Append(text);
        }

        /// <summary>
        /// Appends the formatted text passed to the script.
        /// </summary>
        /// <param name="format">The format string.</param>
        /// <param name="args">The arguments</param>
        public void Append(string format, params object[] args)
        {
            sbMain.Append(string.Format(format, args));
        }

        /// <summary>
        /// Appends the text passed to the script.
        /// </summary>
        /// <param name="text">The text.</param>
        public void AppendLine(string text)
        {
            sbMain.AppendLine(text);
        }

        /// <summary>
        /// Appends the formatted text passed to the script.
        /// </summary>
        /// <param name="format">The format string.</param>
        /// <param name="args">The arguments</param>
        public void AppendLine(string format, params object[] args)
        {
            sbMain.AppendLine(string.Format(format, args));
        }

        /// <summary>
        /// Appends a method definition to the script.
        /// </summary>
        /// <param name="name">The method name.</param>
        /// <param name="parameters">The parameter names.</param>
        /// <exception cref="InvalidOperationException">Thrown if a method is already open.</exception>
        public void OpenFunction(string name, params string[] parameters)
        {
            if (inFunction)
                throw new InvalidOperationException("OpenFunction() has already been called.");

            if (parameters.Length == 0)
                sbMain.AppendLine(string.Format("function {0}() {{", name));
            else
            {
                sbMain.Append(string.Format("function {0}(", name));

                for (int i = 0; i < parameters.Length; i++)
                {
                    if (i > 0)
                        sbMain.Append(',');

                    sbMain.Append(parameters[i]);
                }

                sbMain.AppendLine(") {");
            }

            sbMain.AppendLine();
            inFunction = true;
        }

        /// <summary>
        /// Closes an open method definition.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if a method is not open.</exception>
        public void CloseFunction()
        {
            if (!inFunction)
                throw new InvalidOperationException("OpenFunction() has not been called.");

            sbMain.AppendLine("}");
            sbMain.AppendLine();

            inFunction = false;
        }

        /// <summary>
        /// Renders the script contents to a <see cref="Literal" /> control.
        /// </summary>
        /// <param name="literal">The target control.</param>
        public void RenderTo(Literal literal)
        {
            if (inFunction)
                throw new ArgumentException("Cannot render scripts while nested within an OpenFunction() block.");

            var sb = new StringBuilder(sbMain.Length + 1024);

            if (scriptRefs.Count > 0)
                sb.AppendLine();

            foreach (var uri in scriptRefs)
            {
                sb.AppendFormat(@"<script type=""text/javascript"" src=""{0}""></script>", uri);
                sb.AppendLine();
            }

            if (scriptRefs.Count > 0)
                sb.AppendLine();

            sb.AppendLine("<script type=\"text/javascript\">");

            if (variables.Count > 0)
            {
                foreach (var pair in variables)
                {
                    var stringValue = pair.Value as string;

                    if (stringValue != null)
                        sb.AppendFormat("var {0} = \"{1}\";", pair.Key, ScriptLiteral.Escape(stringValue));
                    else if (pair.Value is bool)
                        sb.AppendFormat("var {0} = {1};", pair.Key, ((bool)pair.Value) ? "true" : "false");
                    else
                        sb.AppendFormat("var {0} = {1};", pair.Key, pair.Value);

                    sb.AppendLine();
                }

                sb.AppendLine();
            }

            sb.AppendLine(sbMain.ToString());

            sb.AppendLine("</script>");

            literal.Text = sb.ToString();
        }
    }
}
