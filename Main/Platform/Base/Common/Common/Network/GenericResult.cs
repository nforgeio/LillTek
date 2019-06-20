//-----------------------------------------------------------------------------
// FILE:        GenericResult.cs
// OWNER:       Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: A generic class used to wrap results and exceptions returned by a 
//              web service in a form that can be consumed by Silverlight applications.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

namespace LillTek.Common
{
    /// <summary>
    /// Manages the exception type/fault mappings used by <see cref="GenericResult{TResult}" />.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class maintains a global table of exception type names to type instances
    /// used by <see cref="GenericResult{TResult}" /> for mapping a fault type
    /// string to a specific exception type to be thrown when the client receives
    /// a faulted result.  By default,  <see cref="GenericResult{TResult}" /> will
    /// throw a <see cref="RemoteException" /> for a fault.  
    /// </para>
    /// <para>
    /// Applications can use one of the <b>RegisterException()</b> overrides to
    /// register custom mappings.  These methods map a fault type string to 
    /// a local exception type, where the fault type string is the fully qualified
    /// name of the type as it was thrown on the service.
    /// </para>
    /// <para>
    /// <see cref="RegisterException(System.Type)" /> maps the specified exception type's
    /// fully qualified name to the fault type string.  You'll use this method most
    /// of the time, specially for standard .NET framework exceptions.  The 
    /// <see cref="RegisterException(System.Type,string)" /> method is also available
    /// for situations where you need to map local exception type with a different
    /// fully qualified name to a fault.
    /// </para>
    /// <para>
    /// The <see cref="RegisterCommonExceptions" /> method is called by the static constructor
    /// and registers some common exceptions, including:
    /// </para>
    /// <list type="bullet">
    ///     <item>System.Security.SecurityException</item>
    ///     <item>ExpiredTicketException</item>
    ///     <item>System.TimeoutException</item>
    ///     <item>System.NotImplementedException</item>
    ///     <item>System.ArgumentException</item>
    ///     <item>System.ArgumentNullException</item>
    ///     <item>CancelException</item>
    ///     <item>VersionException</item>
    /// </list>
    /// <note>
    /// All registered exception types must implement a constructor accepting a
    /// single string message parameter.
    /// </note>
    /// </remarks>
    /// <threadsafety static="true" />
    public static class GenericResult
    {
        private static Dictionary<string, System.Type> faultMap = new Dictionary<string, Type>();

        /// <summary>
        /// Generic constructor.
        /// </summary>
        static GenericResult()
        {
            RegisterCommonExceptions();
        }

        /// <summary>
        /// Registers an exception type associating the type's fully qualified name 
        /// with the fault type string.
        /// </summary>
        /// <param name="type">The exception type being registered.</param>
        /// <exception cref="ArgumentException">Thrown if the type does not derive from <see cref="Exception" />.</exception>
        public static void RegisterException(System.Type type)
        {
            RegisterException(type, type.FullName);
        }

        /// <summary>
        /// Registers and exception type associating the type with the fault string type specified.
        /// </summary>
        /// <param name="type">The exception type being registered.</param>
        /// <param name="faultType">The fault string type.</param>
        /// <exception cref="ArgumentException">Thrown if the type does not derive from <see cref="Exception" />.</exception>
        public static void RegisterException(System.Type type, string faultType)
        {
            if (!type.IsSubclassOf(typeof(Exception)))
                throw new ArgumentException(string.Format("GenericResult cannot register [{0}] because it does not derive from [Exception].", type.FullName));

            lock (faultMap)
                faultMap[faultType] = type;
        }

        /// <summary>
        /// Registers some common exception types.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <see cref="RegisterCommonExceptions" /> method registers some common
        /// exceptions, including:
        /// </para>
        /// <list type="bullet">
        ///     <item>System.Security.SecurityException</item>
        ///     <item>ExpiredTicketException</item>
        ///     <item>System.TimeoutException</item>
        ///     <item>System.NotImplementedException</item>
        ///     <item>System.ArgumentException</item>
        ///     <item>System.ArgumentNullException</item>
        ///     <item>CancelException</item>
        ///     <item>VersionException</item>
        /// </list>
        /// </remarks>
        public static void RegisterCommonExceptions()
        {
            RegisterException(typeof(System.Security.SecurityException));
            RegisterException(typeof(ExpiredTicketException));
            RegisterException(typeof(TimeoutException));
            RegisterException(typeof(NotImplementedException));
            RegisterException(typeof(ArgumentException));
            RegisterException(typeof(ArgumentNullException));
            RegisterException(typeof(CancelException));
            RegisterException(typeof(VersionException));
        }

        /// <summary>
        /// Clears all fault type registrations (useful for unit testing).
        /// </summary>
        public static void ClearExceptions()
        {
            lock (faultMap)
                faultMap.Clear();
        }

        /// <summary>
        /// Maps a fault type name and message into a local exception.
        /// </summary>
        /// <param name="faultType">The fault type name.</param>
        /// <param name="message">The fault message.</param>
        /// <returns>The <see cref="Exception" /> or <c>null</c> if the fault could not be mapped.</returns>
        internal static Exception MapFault(string faultType, string message)
        {
            System.Type type;

            lock (faultMap)
                if (faultMap.TryGetValue(faultType, out type))
                    return (Exception)Activator.CreateInstance(type, new object[] { message });

            return null;
        }
    }

    /// <summary>
    /// A generic class used to wrap results and exceptions returned by a 
    /// web service in a form that can be consumed by Silverlight applications.
    /// </summary>
    /// <typeparam name="TResult">The web service operation result type.</typeparam>
    /// <remarks>
    /// <para>
    /// Silverlight client applications are not able to consume standard SOAP
    /// faults thrown by web services.  This class does a resonable job of working
    /// around this limitation by wrapping a fault string together with a web service
    /// method result for transmission back to the Silverlight client where an
    /// exception will be thrown for faults or the wrapped result can be examined.
    /// </para>
    /// <para>
    /// This class is pretty easy to use.  On the service side, you'll need to
    /// modify your methods to return <c>GenericResult&lt;T&gt;</c> where <b>T</b>
    /// is the object type your method returns (use <b>int</b> for methods that would 
    /// normally return <c>void</c>).  Then instantiate and return a <c>GenericResult</c>
    /// instance where you would normally return a result or handle an exception.
    /// </para>
    /// <code language="cs">
    /// [ServiceContract]
    /// public interface IMyService {
    /// 
    ///     [OperationContract]
    ///     GenericResult&lt;double&gt; Div(double p1,double p2);
    /// }
    /// 
    /// public class MyService : IMyService {
    /// 
    ///     public GenericResult&lt;double&gt; Div(double p1,double p2) {
    ///     
    ///         try {
    ///         
    ///             return GenericResult&lt;double&gt;(p1/p2);
    ///         }
    ///         catch (DivideByZeroException e) {
    ///         
    ///             return GenericResult&lt;double&gt;(e);
    ///         }
    ///     }
    /// }
    /// </code>
    /// <para>
    /// On the Silverlight client side, you'll use the static <see cref="GetOrThrow" />
    /// method obtain the result of the operation.  This method returns the operation
    /// result if there were no errors, otherwise it throws a <see cref="RemoteException" />
    /// or other registered exception with the fault message.  Here's how you might code this:
    /// </para>
    /// <code language="cs">
    /// void Div() {
    /// 
    ///     MyServiceClient     client = new MyServiceClient();
    ///     
    ///     client.DivCompleted += new EventHandler&lt;DivCompletedEventArgs&gt;(OnDivCompleted);
    ///     client.DivAsync(10,20);
    /// }
    /// 
    /// void OnDivCompleted(object sender,DivCompletedEventArgs args) {
    /// 
    ///     double  result;
    /// 
    ///     try {
    ///     
    ///         result = GenericResult.GetOrThrow(args.Result);
    ///     }
    ///     catch (RemoteException e) {
    ///     
    ///         // Handle exceptions thrown by the service
    ///     }
    ///     catch (Exception e) {
    ///     
    ///         // Handle exceptions thrown by Silverlight or WCF
    ///     }
    /// }
    /// </code>
    /// <para>
    /// All of the action takes place in the <c>GenericResult.GetOrThrow()</c>
    /// method call.  If the Silverlight or WCF encountered an error then
    /// the <c>args.Result</c> will throw an exception.  If a result was
    /// received from the service, then <c>GetOrThrow()</c> will throw
    /// a <see cref="RemoteException" /> if the service returned a fault,
    /// otherwise it will return the unwrapped service result.
    /// </para>
    /// <para>
    /// By default, this type will throw a <see cref="RemoteException" />
    /// on the Silverlight client side but the result type does include the
    /// <see cref="FaultType" /> which will be set to the fully qualified
    /// name of the exception thrown by the service and it is possible
    /// to register specific exception type mappings using <see cref="GenericResult.RegisterException(System.Type)" />.
    /// </para>
    /// </remarks>
    [DataContract(Namespace = "http://schemas.lilltek.com/platform/LillTek.Common.GenericResult/2008-07-24")]
    public class GenericResult<TResult>
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the web service result returned be calling the instance's
        /// <see cref="Result" /> property.  If the instance has a <see cref="Fault" /> string
        /// property and its value is not <c>null</c> then a <see cref="RemoteException" />
        /// will be thrown instead.
        /// </summary>
        /// <param name="wrappedResult">The <see cref="GenericResult" /> instance returned by the service.</param>
        public static TResult GetOrThrow(object wrappedResult)
        {

            if (wrappedResult == null)
                throw new ArgumentNullException("wrappedResult");

            System.Type     resultType = wrappedResult.GetType();
            PropertyInfo    messageProp;
            PropertyInfo    typeProp;
            PropertyInfo    resultProp;
            string          fault;
            string          faultType;
            Exception       e;

            messageProp = resultType.GetProperty("Fault");
            typeProp    = resultType.GetProperty("FaultType");

            if (messageProp != null && messageProp.PropertyType == typeof(string) &&
                typeProp != null && typeProp.PropertyType == typeof(string))
            {

                fault     = (string)messageProp.GetValue(wrappedResult, null);
                faultType = (string)typeProp.GetValue(wrappedResult, null);

                if (fault != null && faultType != null)
                {

                    e = GenericResult.MapFault(faultType, fault);
                    if (e != null)
                        throw e;
                    else
                        throw new RemoteException(fault);
                }
            }

            resultProp = resultType.GetProperty("Result");
            if (resultProp == null)
                throw new InvalidOperationException("GenericResult: Result object passed does not expose a [Result] property.");

            if (!object.ReferenceEquals(typeof(TResult), resultProp.PropertyType))
                throw new InvalidOperationException(string.Format("GenericResult: Result object has type [{0}] rather than the expected [{1}] type.",
                                                                  resultProp.PropertyType.FullName, typeof(TResult).FullName));

            return (TResult)resultProp.GetValue(wrappedResult, null);
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructs a generic result with a default value.
        /// </summary>
        public GenericResult()
        {
            this.Result    = default(TResult);
            this.Fault     = null;
            this.FaultType = null;
        }

        /// <summary>
        /// Constructs a generic result using a specific result value.
        /// </summary>
        /// <param name="result">The result.</param>
        public GenericResult(TResult result)
        {
            this.Result    = result;
            this.Fault     = null;
            this.FaultType = null;
        }

        /// <summary>
        /// Constructs a generic result that signals an error.
        /// </summary>
        /// <param name="e">The exception.</param>
        public GenericResult(Exception e)
        {
            this.Result    = default(TResult);
            this.Fault     = e.Message;
            this.FaultType = e.GetType().FullName;
        }

        /// <summary>
        /// The result value.
        /// </summary>
#if !SILVERLIGHT
        [DataMember]
#endif
        public TResult Result { get; set; }

        /// <summary>
        /// The fault type.
        /// </summary>
#if !SILVERLIGHT
        [DataMember]
#endif
        public string FaultType { get; set; }

        /// <summary>
        /// The fault message (or <c>null</c>).
        /// </summary>
#if !SILVERLIGHT
        [DataMember]
#endif
        public string Fault { get; set; }
    }
}
