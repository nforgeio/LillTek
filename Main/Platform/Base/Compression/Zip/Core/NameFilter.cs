// NameFilter.cs
//
// Copyright 2005 John Reilly
//
// This program is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either version 2
// of the License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
//
// Linking this library statically or dynamically with other modules is
// making a combined work based on this library.  Thus, the terms and
// conditions of the GNU General Public License cover the whole
// combination.
// 
// As a special exception, the copyright holders of this library give you
// permission to link this library with independent modules to produce an
// executable, regardless of the license terms of these independent
// modules, and to copy and distribute the resulting executable under
// terms of your choice, provided that you also meet, for each linked
// independent module, the terms and conditions of the license of that
// module.  An independent module is a module which is not derived from
// or based on this library.  If you modify this library, you may extend
// this exception to your version of the library, but you are not
// obligated to do so.  If you do not wish to do so, delete this
// exception statement from your version.

// LillTek.com: Minor changes to the source files to change the namespace
//              to LillTek.Compression and otherwise make them suitable
//              for Windows/CE builds.
//
//              Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace LillTek.Compression.Core
{
	/// <summary>
	/// NameFilter is a string matching class which allows for both positive and negative
	/// matching.
	/// A filter is a sequence of independant <see cref="Regex">regular expressions</see> separated by semi-colons ';'
	/// Each expression can be prefixed by a plus '+' sign or a minus '-' sign to denote the expression
	/// is intended to include or exclude names.  If neither a plus or minus sign is found include is the default
	/// A given name is tested for inclusion before checking exclusions.  Only names matching an include spec 
	/// and not matching an exclude spec are deemed to match the filter.
	/// An empty filter matches any name.
	/// </summary>
	/// <example>The following expression includes all name ending in '.dat' with the exception of 'dummy.dat'
	/// "+\.dat$;-^dummy\.dat$"
	/// </example>
	public class NameFilter : IScanFilter
	{
		#region Constructors
		/// <summary>
		/// Construct an instance based on the filter expression passed
		/// </summary>
		/// <param name="filter">The filter expression.</param>
		public NameFilter(string filter)
		{
			filter_ = filter;
			inclusions_ = new List<Regex>();
			exclusions_ = new List<Regex>();
			Compile();
		}
		#endregion

		/// <summary>
		/// Test a string to see if it is a valid regular expression.
		/// </summary>
		/// <param name="expression">The expression to test.</param>
		/// <returns><c>true</c> if expression is a valid <see cref="System.Text.RegularExpressions.Regex"/> <c>false</c> otherwise.</returns>
		public static bool IsValidExpression(string expression)
		{
			bool result = true;
			try
			{
				Regex exp = new Regex(expression, RegexOptions.IgnoreCase | RegexOptions.Singleline);
			}
			catch
			{
				result = false;
			}
			return result;
		}

		/// <summary>
		/// Test an expression to see if it is valid as a filter.
		/// </summary>
		/// <param name="toTest">The filter expression to test.</param>
		/// <returns><c>true</c> if the expression is valid, <c>false</c> otherwise.</returns>
		public static bool IsValidFilterExpression(string toTest)
		{
			if ( toTest == null )
			{
				throw new ArgumentNullException("toTest");
			}

			bool result = true;

			try
			{
				string[] items = toTest.Split(';');
				for ( int i = 0; i < items.Length; ++i )
				{
					if ( items[i] != null && items[i].Length > 0 )
					{
						string toCompile;

						if ( items[i][0] == '+' )
						{
							toCompile = items[i].Substring(1, items[i].Length - 1);
						}
						else if ( items[i][0] == '-' )
						{
							toCompile = items[i].Substring(1, items[i].Length - 1);
						}
						else
						{
							toCompile = items[i];
						}

						Regex testRegex = new Regex(toCompile, RegexOptions.IgnoreCase | RegexOptions.Singleline);
					}
				}
			}
			catch ( Exception )
			{
				result = false;
			}

			return result;
		}

		/// <summary>
		/// Convert this filter to its string equivalent.
		/// </summary>
		/// <returns>The string equivalent for this filter.</returns>
		public override string ToString()
		{
			return filter_;
		}

		/// <summary>
		/// Test a value to see if it is included by the filter.
		/// </summary>
		/// <param name="name">The value to test.</param>
		/// <returns><c>true</c> if the value is included, <c>false</c> otherwise.</returns>
		public bool IsIncluded(string name)
		{
			bool result = false;
			if ( inclusions_.Count == 0 )
			{
				result = true;
			}
			else
			{
				foreach ( Regex r in inclusions_ )
				{
					if ( r.IsMatch(name) )
					{
						result = true;
						break;
					}
				}
			}
			return result;
		}

		/// <summary>
		/// Test a value to see if it is excluded by the filter.
		/// </summary>
		/// <param name="name">The value to test.</param>
		/// <returns><c>true</c> if the value is excluded, <c>false</c> otherwise.</returns>
		public bool IsExcluded(string name)
		{
			bool result = false;
			foreach ( Regex r in exclusions_ )
			{
				if ( r.IsMatch(name) )
				{
					result = true;
					break;
				}
			}
			return result;
		}

		#region IScanFilter Members
		/// <summary>
		/// Test a value to see if it matches the filter.
		/// </summary>
		/// <param name="name">The value to test.</param>
		/// <returns><c>true</c> if the value matches, <c>false</c> otherwise.</returns>
		public bool IsMatch(string name)
		{
			return (IsIncluded(name) == true) && (IsExcluded(name) == false);
		}
		#endregion

		/// <summary>
		/// Compile this filter.
		/// </summary>
		void Compile()
		{
			// TODO: Check too see if combining RE's makes it faster/smaller.
			// simple scheme would be to have one RE for inclusion and one for exclusion.
			if ( filter_ == null )
			{
				return;
			}

			// TODO: Allow for paths to include ';'
			string[] items = filter_.Split(';');
			for ( int i = 0; i < items.Length; ++i )
			{
				if ( (items[i] != null) && (items[i].Length > 0) )
				{
					bool include = (items[i][0] != '-');
					string toCompile;

					if ( items[i][0] == '+' )
					{
						toCompile = items[i].Substring(1, items[i].Length - 1);
					}
					else if ( items[i][0] == '-' )
					{
						toCompile = items[i].Substring(1, items[i].Length - 1);
					}
					else
					{
						toCompile = items[i];
					}

					// NOTE: Regular expressions can fail to compile here for a number of reasons that cause an exception
					// these are left unhandled here as the caller is responsible for ensuring all is valid.
					// several functions IsValidFilterExpression and IsValidExpression are provided for such checking
					if ( include )
					{
#if MOBILE_DEVICE
						inclusions_.Add(new Regex(toCompile, RegexOptions.IgnoreCase | RegexOptions.Singleline));
#else
						inclusions_.Add(new Regex(toCompile, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline));
#endif
					}
					else
					{
#if MOBILE_DEVICE
						exclusions_.Add(new Regex(toCompile, RegexOptions.IgnoreCase | RegexOptions.Singleline));
#else
						exclusions_.Add(new Regex(toCompile, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline));
#endif
					}
				}
			}
		}

		#region Instance Fields
		string filter_;
		List<Regex> inclusions_;
		List<Regex> exclusions_;
		#endregion
	}
}
