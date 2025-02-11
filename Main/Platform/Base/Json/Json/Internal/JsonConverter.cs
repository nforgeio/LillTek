#region License
// Copyright 2006 James Newton-King
// http://www.newtonsoft.com
//
// This work is licensed under the Creative Commons Attribution 2.5 License
// http://creativecommons.org/licenses/by/2.5/
//
// You are free:
//    * to copy, distribute, display, and perform the work
//    * to make derivative works
//    * to make commercial use of the work
//
// Under the following conditions:
//    * You must attribute the work in the manner specified by the author or licensor:
//          - If you find this component useful a link to http://www.newtonsoft.com would be appreciated.
//    * For any reuse or distribution, you must make clear to others the license terms of this work.
//    * Any of these conditions can be waived if you get permission from the copyright holder.
//
// ----------------------------------------------------------------------------
// Minor modifications to fit into the LillTek Platform
//
// Copyright (c) 2005-2015 by Jeffrey Lill.
#endregion

using System;
using System.Collections.Generic;
using System.Text;

namespace LillTek.Json.Internal
{
    /// <summary>
    /// 
    /// </summary>
	internal abstract class JsonConverter
	{
        /// <summary>
        /// 
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="value"></param>
		public virtual void WriteJson(JsonWriter writer, object value)
		{
			JsonSerializer serializer = new JsonSerializer();

			serializer.Serialize(writer, value);
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="objectType"></param>
        /// <returns></returns>
		public virtual object ReadJson(JsonReader reader, Type objectType)
		{
			throw new NotImplementedException(string.Format("{0} has not overriden FromJson method.", GetType().Name));
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="objectType"></param>
        /// <returns></returns>
		public abstract bool CanConvert(Type objectType);
	}
}
