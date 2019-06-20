//-----------------------------------------------------------------------------
// FILE:        SqlConnectionInfo.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Abstracts a SQL connection string.

using System;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using System.Collections;
using System.Collections.Generic;

using LillTek.Common;

namespace LillTek.Data
{
    /// <summary>
    /// Abstracts the contents of a SQL connection string.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The main purpose for this class is to normalize the various forms
    /// of a SQL connection string parameters.  The class constructor and the
    /// <see cref="Parse" /> method processes the name/value pairs encoded 
    /// into the string into a collection of name value pairs, mapping certain
    /// property names as described below.
    /// </para>
    /// <code language="none">
    ///    Input Name     maps to ->      Normalized
    ///     ----------                     ----------
    ///    Data Source                    Server
    ///    Initial Catalog                Database
    ///    User Id                        UID
    ///    Password                       PWD
    ///    Trusted_Connection=true        Integrated Security=SSPI
    ///    Trusted_Connection=false       (nothing)
    /// </code>
    /// <para>
    /// Other connection string parameters will be added to the property
    /// set unchanged.
    /// </para>
    /// <para>
    /// The instance indexer can be used to gain access to all connection
    /// string properties using the case insenstive property name as the
    /// key.  The class also implements <see cref="IEnumerable" /> so the properties can
    /// be enumerated using standard techniques.  The enumerator returns the
    /// set of property keys held by the collection.
    /// </para>
    /// <note>
    /// If no security parameters are specified then the
    /// <see cref="ToString" /> method will render "Integrated Security=SSPI".
    /// </note>
    /// </remarks>
    public sealed class SqlConnectionInfo : IEnumerable
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Casts a connection string instance back into an actual string.
        /// </summary>
        /// <param name="conInfo">The string to be convered.</param>
        /// <returns>The corresponding actual string.</returns>
        public static implicit operator string(SqlConnectionInfo conInfo)
        {
            return conInfo.ToString();
        }

        /// <summary>
        /// Parses the connection string passed into a SqlConnectionInfo instance.
        /// </summary>
        /// <param name="conString">The source string.</param>
        /// <returns>The connection info instance.</returns>
        public static SqlConnectionInfo Parse(string conString)
        {
            return new SqlConnectionInfo(conString);
        }

        /// <summary>
        /// Normalizes the SQL connection string passed.
        /// </summary>
        /// <param name="conString">The input string.</param>
        /// <returns>The normalized version of the input..</returns>
        public static string Normalize(string conString)
        {
            return SqlConnectionInfo.Parse(conString).ToString();
        }

        //---------------------------------------------------------------------
        // Instance members

        private ArgCollection properties = new ArgCollection('=', ';');

        /// <summary>
        /// Constructs an empty connection info instance.
        /// </summary>
        public SqlConnectionInfo()
        {
        }

        /// <summary>
        /// Constructs a connection info instance by parsing the parameters
        /// encoded in the string passed.
        /// </summary>
        /// <param name="conString">The input connection string.</param>
        public SqlConnectionInfo(string conString)
        {
            var args = ArgCollection.Parse(conString, '=', ';');

            foreach (string param in args)
            {
                switch (param.ToLowerInvariant())
                {
                    case "data source":

                        properties.Set("server", args[param]);
                        break;

                    case "initial catalog":

                        properties.Set("database", args[param]);
                        break;

                    case "user id":

                        properties.Set("uid", args[param]);
                        break;

                    case "password":

                        properties.Set("pwd", args[param]);
                        break;

                    case "trusted_connection":

                        switch (args[param].ToLowerInvariant())
                        {
                            case "true":
                            case "yes":

                                properties.Set("integrated security", "SSPI");
                                break;

                            case "false":
                            case "no":

                                break;
                        }
                        break;

                    default:

                        properties.Set(param.ToLowerInvariant(), args[param]);
                        break;
                }
            }
        }

        /// <summary>
        /// Indexes into the property set to referenced value.
        /// </summary>
        /// <param name="key">The case insenstive key.</param>
        /// <remarks>
        /// <para>
        /// This indexer can be used to add, return, or modify property entries
        /// to the connection string.  Note that the getter will return null
        /// if the requested property is not present.
        /// </para>
        /// <note>
        /// The indexer doesn't perform any additional property
        /// name normalization.  Use the <see cref="Server" />, <see cref="Database" />,
        /// <see cref="Security" />, <see cref="UserID" />, and <see cref="Password" />
        /// properties to modify these properties.
        /// </note>
        /// </remarks>
        public string this[string key]
        {
            get { return properties[key]; }
            set { properties[key] = value; }
        }

        /// <summary>
        /// The database server name,
        /// </summary>
        public string Server
        {
            get { return this["server"]; }
            set { this["server"] = value; }
        }

        /// <summary>
        /// The database name.
        /// </summary>
        public string Database
        {
            get { return this["database"]; }
            set { this["database"] = value; }
        }

        /// <summary>
        /// The security scheme (either empty or "SSPI").
        /// </summary>
        public string Security
        {
            get
            {
                var v = this["integrated security"];

                if (v == null || v == string.Empty)
                    return v;
                else
                    return "SSPI";
            }

            set { this["integrated security"] = value; }
        }

        /// <summary>
        /// The authentication user ID to use SQL security or <c>null</c> for
        /// integrated Windows security.
        /// </summary>
        public string UserID
        {
            get { return this["uid"]; }
            set { this["uid"] = value; }
        }

        /// <summary>
        /// The authentication password to use SQL security or <c>null</c> for
        /// integrated Windows security.
        /// </summary>
        public string Password
        {
            get { return this["pwd"]; }
            set { this["pwd"] = value; }
        }

        /// <summary>
        /// Renders the connection info instance back into a string format suitable for
        /// passing directly to ADO.NET.
        /// </summary>
        public override string ToString()
        {
            var output = properties.ToString();

            if (this["integrated security"] == null && this["uid"] == null)
            {
                if (!output.EndsWith(";"))
                    output += ";";

                output += "Integrated Security=SSPI";
            }

            return output;
        }

        /// <summary>
        /// Returns the set of property keys maintained by the instance.
        /// </summary>
        public IEnumerator GetEnumerator()
        {
            return properties.GetEnumerator();
        }
    }
}
