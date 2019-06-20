//-----------------------------------------------------------------------------
// FILE:        SqlParam.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Encapsulates a SQL parameter passed to the SqlBatch.AppendCall()
//              method.

using System;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using System.Collections;

using LillTek.Common;

namespace LillTek.Data
{
    /// <summary>
    /// Encapsulates a SQL parameter passed to the <see cref="SqlBatch" />.<see cref="SqlBatch.AppendCall" /> method.
    /// </summary>
    public class SqlParam
    {
        private const string BadParamNameMsg = "A SQL parameter name cannot be null or empty.";

        /// <summary>
        /// Constructs an integer parameter.
        /// </summary>
        /// <param name="name">The parameter name.</param>
        /// <param name="value">The parameter value.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="name" /> is <c>null</c> or empty.</exception>
        public SqlParam(string name, int? value)
        {
            if (String.IsNullOrEmpty(name))
                throw new ArgumentException(BadParamNameMsg, "name");

            this.Name    = name;
            this.Value   = value;
            this.Literal = value.HasValue ? SqlHelper.Literal(value.Value) : "null";
        }

        /// <summary>
        /// Constructs a long parameter.
        /// </summary>
        /// <param name="name">The parameter name.</param>
        /// <param name="value">The parameter value.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="name" /> is <c>null</c> or empty.</exception>
        public SqlParam(string name, long? value)
        {
            if (String.IsNullOrEmpty(name))
                throw new ArgumentException(BadParamNameMsg, "name");

            this.Name    = name;
            this.Value   = value;
            this.Literal = value.HasValue ? SqlHelper.Literal(value.Value) : "null";
        }

        /// <summary>
        /// Constructs a floating point parameter.
        /// </summary>
        /// <param name="name">The parameter name.</param>
        /// <param name="value">The parameter value.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="name" /> is <c>null</c> or empty.</exception>
        public SqlParam(string name, double? value)
        {
            if (String.IsNullOrEmpty(name))
                throw new ArgumentException(BadParamNameMsg, "name");

            this.Name    = name;
            this.Value   = value;
            this.Literal = value.HasValue ? SqlHelper.Literal(value.Value) : "null";
        }

        /// <summary>
        /// Constructs a string parameter.
        /// </summary>
        /// <param name="name">The parameter name.</param>
        /// <param name="value">The parameter value.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="name" /> is <c>null</c> or empty.</exception>
        public SqlParam(string name, string value)
        {
            if (String.IsNullOrEmpty(name))
                throw new ArgumentException(BadParamNameMsg, "name");

            this.Name    = name;
            this.Value   = value;
            this.Literal = value != null ? SqlHelper.Literal(value) : "null";
        }

        /// <summary>
        /// Constructs a boolean parameter.
        /// </summary>
        /// <param name="name">The parameter name.</param>
        /// <param name="value">The parameter value.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="name" /> is <c>null</c> or empty.</exception>
        public SqlParam(string name, bool? value)
        {
            if (String.IsNullOrEmpty(name))
                throw new ArgumentException(BadParamNameMsg, "name");

            this.Name    = name;
            this.Value   = value;
            this.Literal = value.HasValue ? SqlHelper.Literal(value.Value) : "null";
        }

        /// <summary>
        /// Constructs a <see cref="DateTime"/> parameter.
        /// </summary>
        /// <param name="name">The parameter name.</param>
        /// <param name="value">The parameter value.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="name" /> is <c>null</c> or empty.</exception>
        public SqlParam(string name, DateTime? value)
        {
            if (String.IsNullOrEmpty(name))
                throw new ArgumentException(BadParamNameMsg, "name");

            this.Name    = name;
            this.Value   = value;
            this.Literal = value.HasValue ? SqlHelper.Literal(value.Value) : "null";
        }

        /// <summary>
        /// Constructs a <see cref="DateTimeOffset"/> parameter.
        /// </summary>
        /// <param name="name">The parameter name.</param>
        /// <param name="value">The parameter value.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="name" /> is <c>null</c> or empty.</exception>
        public SqlParam(string name, DateTimeOffset? value)
        {
            if (String.IsNullOrEmpty(name))
                throw new ArgumentException(BadParamNameMsg, "name");

            this.Name    = name;
            this.Value   = value;
            this.Literal = value.HasValue ? SqlHelper.Literal(value.Value) : "null";
        }

        /// <summary>
        /// Constructs a <see cref="Guid"/> parameter.
        /// </summary>
        /// <param name="name">The parameter name.</param>
        /// <param name="value">The parameter value.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="name" /> is <c>null</c> or empty.</exception>
        public SqlParam(string name, Guid? value)
        {
            if (String.IsNullOrEmpty(name))
                throw new ArgumentException(BadParamNameMsg, "name");

            this.Name    = name;
            this.Value   = value;
            this.Literal = value.HasValue ? SqlHelper.Literal(value.Value) : "null";
        }

        /// <summary>
        /// Constructs a byte array parameter.
        /// </summary>
        /// <param name="name">The parameter name.</param>
        /// <param name="value">The parameter value.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="name" /> is <c>null</c> or empty.</exception>
        public SqlParam(string name, byte[] value)
        {
            if (String.IsNullOrEmpty(name))
                throw new ArgumentException(BadParamNameMsg, "name");

            this.Name    = name;
            this.Value   = value;
            this.Literal = value != null ? SqlHelper.Literal(value) : "null";
        }

        /// <summary>
        /// The parameter name.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// The parameter value as an <see cref="object" />.
        /// </summary>
        public object Value { get; private set; }

        /// <summary>
        /// The parameter value as a T-SQL literal.
        /// </summary>
        public string Literal { get; private set; }
    }
}
