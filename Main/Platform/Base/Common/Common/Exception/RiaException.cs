//-----------------------------------------------------------------------------
// FILE:        RiaException.cs
// OWNER:       Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Used to.

using System;
using System.Collections.Generic;

#if SILVERLIGHT
using System.ComponentModel.DataAnnotations;
#endif

using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

#if SILVERLIGHT
using System.ServiceModel.DomainServices.Client;
#endif

namespace LillTek.Common
{
#if !SILVERLIGHT

    /// <summary>
    /// Stub class for use in non-Silverlight builds.
    /// </summary>
    public class SubmitOperation
    {
    }

    /// <summary>
    /// Stub class for use in non-Silverlight builds.
    /// </summary>
    public sealed class InvokeOperation
    {
    }

    /// <summary>
    /// Stub class for use in non-Silverlight builds.
    /// </summary>
    public sealed class LoadOperation
    {
    }

#endif

    /// <summary>
    /// Used by a client application to reconsitute exceptions thrown by an .NET RIA service.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Microsoft .NET RIA Services platform does an admirable job of of making it easy
    /// to write n-tier Silverlight applications by managing the serialization of entities,
    /// queries, and generalizes service calls made by a client application to the service.
    /// The platform even provides for the transmission of fault information from the service.
    /// </para>
    /// <para>
    /// This fault information returned by an RIA service consists soley of an error message string
    /// and it's the responsibility of the client application to check for presence of an error
    /// and handle it, perhaps by throwing an exception to be caught and handled in some other
    /// part of the application.  This class provides a standard mechanism for RIA services to
    /// marshal an exception instance thrown on the service back the client.
    /// </para>
    /// <para>
    /// The basic idea is for the RIA service methods to catch any service side exceptions
    /// thrown during the processing of a service request and then rethrow a <see cref="RiaException" />
    /// created using the <see cref="RiaException(Exception)" /> constructor.
    /// </para>
    /// <para>
    /// The client side of the application will call one of the <c>Check</c> methods when the
    /// operation completes, passing the operation's <b>InvokeOperation</b>, <b>SubmitOperation</b>,
    /// or <b>LoadOperation</b> instance.  <b>Check</b> will examine the operation to determine if
    /// it failed or was cancelled.  A <see cref="CancelException" /> will be thrown if the operation
    /// failed and the class will attempt to unmarshal and rethrow the original exception thrown on 
    /// during the service processing.
    /// </para>
    /// <para>
    /// The client implementation of this class maintains a global table of fully qualified exception 
    /// type names to type instances.  Silverlight applications can use one of the <b>RegisterException()</b> 
    /// overrides to register custom mappings.  These methods map a fault type string to 
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
    ///     <item>TimeoutException</item>
    ///     <item>NotImplementedException</item>
    ///     <item>InvalidOperationException</item>
    ///     <item>ArgumentException</item>
    ///     <item>ArgumentNullException</item>
    ///     <item>CancelException</item>
    ///     <item>VersionException</item>
    /// </list>
    /// <note>
    /// All registered exception types must implement a constructor accepting a
    /// single string message parameter.
    /// </note>
    /// <para>
    /// <see cref="RiaException" /> marshals exceptions by encoding the fully qualified name
    /// of the original exception thrown during service processing into the error message string
    /// transmitted by RIA Services from the service to the client.  The encoding for this is:
    /// </para>
    /// <code language="none">
    /// ":RIAEXCEPTION:" + &lt;fully qualified type name&gt; + ":" + &lt;message&gt;
    /// </code>
    /// <para>
    /// where <i>message</i> is the error message thrown in the original exception.
    /// </para>
    /// <para>
    /// The <c>Check()</c> methods called by Silverlight clients perform the following
    /// actions when processing an RIA operation:
    /// </para>
    /// <list type="number">
    ///     <item>
    ///     Throw a <see cref="CancelException" /> if the operation was cancelled.
    ///     </item>
    ///     <item>
    ///     Return without throwing anything if the operation completed without error.
    ///     </item>
    ///     <item>
    ///     Examine the operation's error string.  If it is not prefixed with "RIAEXCEPTION:"
    ///     or does not otherwise look like a valid marshaled exception, then throw a
    ///     <see cref="RemoteException" /> with the error string.
    ///     </item>
    ///     <item>
    ///     Parse the error string to obtain the fully qualified exception type name and
    ///     the original exception message.
    ///     </item>
    ///     <item>
    ///     If the type has been registered with the class, then instantiate an instance
    ///     of the type, passing the original exception message and throw it.
    ///     </item>
    ///     <item>
    ///     If the type has not been registered, then throw a <see cref="RemoteException" />
    ///     with the original exception message.
    ///     </item>
    /// </list>
    /// </remarks>
    /// <threadsafety static="true" />
    public sealed class RiaException : Exception
    {
        //---------------------------------------------------------------------
        // Static members

        private const string MarshalPrefix = ":RIAEXCEPTION:";

        private static Dictionary<string, System.Type> faultMap = new Dictionary<string, Type>();

        /// <summary>
        /// Generic constructor.
        /// </summary>
        static RiaException()
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
                throw new ArgumentException(string.Format("RiaException cannot register [{0}] because it does not derive from [Exception].", type.FullName));

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
        ///     <item>TimeoutException</item>
        ///     <item>NotImplementedException</item>
        ///     <item>InvalidOperationException</item>
        ///     <item>ArgumentException</item>
        ///     <item>ArgumentNullException</item>
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
            RegisterException(typeof(InvalidOperationException));
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

#if SILVERLIGHT

        /// <summary>
        /// Maps a fault type name and message into a local exception.
        /// </summary>
        /// <param name="faultType">The fault type name.</param>
        /// <param name="message">The fault message.</param>
        /// <returns>The <see cref="Exception" /> or <c>null</c> if the fault could not be mapped.</returns>
        private static Exception MapFault(string faultType, string message) 
        {
            System.Type     type;

            lock (faultMap)
                if (faultMap.TryGetValue(faultType,out type))
                    return (Exception) Activator.CreateInstance(type,new object[] { message });

            return null;
        }

        /// <summary>
        /// Converts any errors identified for a set of entities into extended
        /// human readable fault details that can be added to exceptions thrown
        /// by this class.
        /// </summary>
        /// <param name="entitiesInError">The problemantic entities.</param>
        /// <returns>The extended details or <c>null</c>.</returns>
        private static string GetFaultDetails(IEnumerable<Entity> entitiesInError) 
        {
            if (entitiesInError == null)
                return null;

            StringBuilder   sb = new StringBuilder();

            foreach (var entity in entitiesInError) 
            {
                string      title;

                if (sb.Length > 0)
                    sb.Append("\r\n");

                title = string.Format("Entity: {0}",entity.GetType().Name);
                sb.AppendLine(title);
                sb.Append('-',title.Length);
                sb.AppendLine();

                foreach (var result in entity.ValidationErrors) {

                    if (!string.IsNullOrWhiteSpace(result.ErrorMessage)) {

                        bool    isFirst = true;

                        sb.Append('[');

                        foreach (var memberName in result.MemberNames) {

                            if (isFirst)
                                isFirst = false;
                            else
                                sb.Append(", ");

                            sb.Append(memberName);
                        }

                        sb.Append("]: ");

                        sb.Append(result.ErrorMessage);
                    }
                }
            }

            if (sb.Length == 0)
                return null;
            else
                return sb.ToString();
        }

        /// <summary>
        /// Converts any errors identified by the validation results into extended
        /// human readable fault details that can be added to exceptions thrown
        /// by this class.
        /// </summary>
        /// <param name="validationResults">The validation results.</param>
        /// <returns>The extended details or <c>null</c>.</returns>
        private static string GetFaultDetails(IEnumerable<ValidationResult> validationResults)
        {
            if (validationResults == null)
                return null;

            StringBuilder   sb = new StringBuilder();

            foreach (var result in validationResults)
            {
                if (!string.IsNullOrWhiteSpace(result.ErrorMessage)) 
                {
                    bool    isFirst = true;

                    sb.Append('[');

                    foreach (var memberName in result.MemberNames) 
                    {
                        if (isFirst)
                            isFirst = false;
                        else
                            sb.Append(", ");

                        sb.Append(memberName);
                    }

                    sb.Append("]: ");
                    sb.AppendLine(result.ErrorMessage);
                }
            }

            if (sb.Length == 0)
                return null;
            else
                return sb.ToString();
        }

        /// <summary>
        /// Attempts to unmarshal and throw the correct exception for the RIA fault.
        /// </summary>
        /// <param name="fault">The RIA fault exception.</param>
        /// <param name="extendedDetails">Extended fault details (or <c>null</c>).</param>
        private static void Check(Exception fault,string extendedDetails) 
        {
            string      message = fault.Message;
            string      typeName;
            Exception   e;
            int         posTag;
            int         posColon;

            // Append any extended details to the exception message.

            message = fault.Message;
            if (extendedDetails != null)
                message += "\r\n" + extendedDetails;

            // Note that as of the 11/2009 PDC release of WCF RIA Services, the invoke
            // operations prefix the error message in faults returned to the client with
            // additional text.  The code below is going to take care of the stripping
            // this prefix off.

            posTag = message.IndexOf(MarshalPrefix);
            if (posTag == -1)
                throw new RemoteException(message);

            posColon = message.IndexOf(':',posTag + MarshalPrefix.Length);
            if (posColon == -1)
                throw new RemoteException(message);

            typeName = message.Substring(posTag + MarshalPrefix.Length,posColon - posTag);
            message  = message.Substring(posColon + 1);

            e = MapFault(typeName,message);
            if (e != null)
                throw e;

            // Rethrow as a RemoteException.

            throw new RemoteException(message);
        }

        /// <summary>
        /// Used by Silverlight client applications to verify that a service operation without
        /// a return value completed successfully.
        /// </summary>
        /// <param name="operation">The service operation context.</param>
        /// <remarks>
        /// <para>
        /// This method returns if the operation was completed successfully without error,
        /// or throws a <see cref="CancelException" /> if the operation was cancelled.
        /// Otherwise, the method unmarshals and throws an encoded exception if possible,
        /// and if that's not possible, a <see cref="RemoteException" /> will be thrown.
        /// </para>
        /// <note>
        /// This method is available only on Silverlight clients.  An <see cref="InvalidOperationException" />
        /// will be thrown if called within an RIA service.
        /// </note>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if called by an RIA service.</exception>
        public static void Check(InvokeOperation operation)
        {
            if (operation.IsCanceled)
                throw new CancelException();

            if (!operation.HasError)
                return;

            operation.MarkErrorAsHandled();
            Check(operation.Error,GetFaultDetails(operation.ValidationErrors));
        }

        /// <summary>
        /// Used by Silverlight client applications to verify that a service operation with
        /// a return value completed successfully.
        /// </summary>
        /// <typeparam name="TResult">The operation result type.</typeparam>
        /// <param name="operation">The service operation context.</param>
        /// <remarks>
        /// <para>
        /// This method returns if the operation was completed successfully without error,
        /// or throws a <see cref="CancelException" /> if the operation was cancelled.
        /// Otherwise, the method unmarshals and throws an encoded exception if possible,
        /// and if that's not possible, a <see cref="RemoteException" /> will be thrown.
        /// </para>
        /// <note>
        /// This method is available only on Silverlight clients.  An <see cref="InvalidOperationException" />
        /// will be thrown if called within an RIA service.
        /// </note>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if called by an RIA service.</exception>
        public static void Check<TResult>(InvokeOperation<TResult> operation) 
        {
            if (operation.IsCanceled)
                throw new CancelException();

            if (!operation.HasError)
                return;

            operation.MarkErrorAsHandled();
            Check(operation.Error,GetFaultDetails(operation.ValidationErrors));
        }

        /// <summary>
        /// Used by Silverlight client applications to verify that an update operation
        /// completed successfully.
        /// </summary>
        /// <param name="operation">The query operation context.</param>
        public static void Check(SubmitOperation operation) 
        {
            if (operation.IsCanceled)
                throw new CancelException();

            if (!operation.HasError)
                return;

            operation.MarkErrorAsHandled();
            Check(operation.Error,GetFaultDetails(operation.EntitiesInError));
        }

        /// <summary>
        /// Used by Silverlight client applications to verify that a load operation
        /// completed successfully.
        /// </summary>
        /// <param name="operation">The query operation context.</param>
        public static void Check(LoadOperation operation) 
        {
            if (operation.IsCanceled)
                throw new CancelException();

            if (!operation.HasError)
                return;

            operation.MarkErrorAsHandled();
            Check(operation.Error,GetFaultDetails(operation.ValidationErrors));
        }

#else
        private const string CheckNotAvailable = "RiaException.Check() is not available for RIA services.";

        // Repeat these definitions so that their APIs will be included in the documentation
        // generated for the WINFULL build.

        /// <summary>
        /// Used by Silverlight client applications to verify that service operation without
        /// a return value completed successfully.
        /// </summary>
        /// <param name="operation">The service operation context.</param>
        /// <remarks>
        /// <para>
        /// This method returns if the operation was completed successfully without error,
        /// or throws a <see cref="CancelException" /> if the operation was cancelled.
        /// Otherwise, the method unmarshals and throws an encoded exception if possible,
        /// and if that's not possible, a <see cref="RemoteException" /> will be thrown.
        /// </para>
        /// <note>
        /// This method is available only on Silverlight clients.  An <see cref="InvalidOperationException" />
        /// will be thrown if called within an RIA service.
        /// </note>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if called by an RIA service.</exception>
        public static void Check(InvokeOperation operation)
        {
            throw new InvalidOperationException(CheckNotAvailable);
        }

        /// <summary>
        /// Used by Silverlight client applications to verify that service operation with
        /// a return value completed successfully.
        /// </summary>
        /// <typeparam name="TResult">The operation result type.</typeparam>
        /// <param name="operation">The service operation context.</param>
        /// <remarks>
        /// <para>
        /// This method returns if the operation was completed successfully without error,
        /// or throws a <see cref="CancelException" /> if the operation was cancelled.
        /// Otherwise, the method unmarshals and throws an encoded exception if possible,
        /// and if that's not possible, a <see cref="RemoteException" /> will be thrown.
        /// </para>
        /// <note>
        /// This method is available only on Silverlight clients.  An <see cref="InvalidOperationException" />
        /// will be thrown if called within an RIA service.
        /// </note>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if called by an RIA service.</exception>
        public static void Check<TResult>(InvokeOperation operation)
        {
            throw new InvalidOperationException(CheckNotAvailable);
        }

        /// <summary>
        /// Used by Silverlight client applications to verify that an update submission operation
        /// completed successfully.
        /// </summary>
        /// <param name="operation">The operation context.</param>
        /// <exception cref="InvalidOperationException">Thrown if called by an RIA service.</exception>
        public static void Check(SubmitOperation operation)
        {
            throw new InvalidOperationException(CheckNotAvailable);
        }

        /// <summary>
        /// Used by Silverlight client applications to verify that a load submission operation
        /// completed successfully.
        /// </summary>
        /// <param name="operation">The operation context.</param>
        /// <exception cref="InvalidOperationException">Thrown if called by an RIA service.</exception>
        public static void Check(LoadOperation operation)
        {
            throw new InvalidOperationException(CheckNotAvailable);
        }
#endif

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructs an instance with a specially formatted message string used
        /// for marshaling the exception passed back to an Silverlight RIA client.
        /// </summary>
        /// <param name="e">The exception to be marhaled.</param>
        /// <remarks>
        /// <note>
        /// <see cref="RiaException" /> instances may only be created within RIA 
        /// services.  An <see cref="InvalidOperationException" /> will be thrown
        /// if this called on a Silverlight client.
        /// </note>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown for Silverlight applications.</exception>
        public RiaException(Exception e)
            : base(e.Message.StartsWith(MarshalPrefix) ? e.Message : MarshalPrefix + e.GetType().FullName + ":" + e.Message)
        {
#if SILVERLIGHT
            throw new InvalidOperationException("RiaException instances cannot be created for Silverlight applications.");
#endif
        }

        /// <summary>
        /// Returns the exception message after removing the internal marshalling information.
        /// </summary>
        public string CleanMessage
        {
            get
            {
                if (this.Message.StartsWith(MarshalPrefix))
                    return this.Message.Substring(MarshalPrefix.Length);
                else
                    return this.Message;
            }
        }
    }
}
