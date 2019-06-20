//-----------------------------------------------------------------------------
// FILE:        TopologyHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: ITopologyProvider related utilities.

using System;
using System.IO;
using System.Net;
using System.Reflection;

using LillTek.Common;

namespace LillTek.Messaging
{
    /// <summary>
    /// <see cref="ITopologyProvider" /> related utilities.
    /// </summary>
    public static class TopologyHelper
    {
        /// <summary>
        /// Message string for the exception thrown a closed router is passed to a topology provider.
        /// </summary>
        public const string RouterClosedMsg = "Router must be started first.";

        /// <summary>
        /// Message string for the exception thrown when an operation is attempted on a closed topology provider.
        /// </summary>
        public const string ClosedMsg = "Topology provider is closed.";

        /// <summary>
        /// Message string for the exception thrown when the cluster endpoint is not logical.
        /// </summary>
        public const string ClusterEPNotLogicalMsg = "[cluster-ep] is not a logical endpoint";

        /// <summary>
        /// Message string for exceptions thrown when a logical endpoint was expected.
        /// </summary>
        public const string LogicalEPMsg = "Logical endpoint expected";

        /// <summary>
        /// Message string for exceptions thrown when a topology provder was not opened in server mode.
        /// </summary>
        public const string NotServerMsg = "Topology provider is not in server mode.";

        /// <summary>
        /// Message string for exceptions thrown when a topology provder was not opened in client mode.
        /// </summary>
        public const string NotClientMsg = "Topology provider is not in client mode.";

        /// <summary>
        /// Instantiates and initializes an <see cref="ITopologyProvider" /> plug-in defined by a configuration setting
        /// and then opens it client mode.
        /// </summary>
        /// <param name="router">The message router to associate with the topology.</param>
        /// <param name="typeKey">The fully qualified configuration setting specifying the topology type specification (or <c>null</c>).</param>
        /// <param name="argsKey">The fully qualified configuration key specifying the topology arguments formatted for <see cref="ArgCollection" />.</param>
        /// <returns>The <see cref="ITopologyProvider" /> instance.</returns>
        /// <remarks>
        /// <para>
        /// This utility uses <see cref="Config.Get(string,System.Type)" /> to map the configuration setting
        /// to a .NET assembly and type and then instantiates an instance and then opens it as a client side
        /// cluster.  argsKey names the configuration string to be be instantiated into a <see cref="ArgCollection" />
        /// and then passed to the cluster's <see cref="ITopologyProvider.OpenClient" /> method.
        /// </para>
        /// <para>
        /// typeKey may be passed as null.  In this case, the configuration arguments must include a
        /// parameter named <b>"topology-type"</b> that defines the type assembly and class.
        /// </para>
        /// <note>
        /// The topology arguments must include an logical endpoint string argument named <b>cluster-ep</b>.
        /// This argument specifies the cluster endpoint to use when opening the topology.
        /// </note>
        /// </remarks>
        public static ITopologyProvider OpenClient(MsgRouter router, string typeKey, string argsKey)
        {
            System.Type         type;
            ITopologyProvider   topology;
            ArgCollection       args;
            string              clusterEP;

            if (!router.IsOpen)
                throw new InvalidOperationException(RouterClosedMsg);

            args = ArgCollection.Parse(Config.Global.Get(argsKey));

            if (typeKey == null)
            {
                string topologyType;

                typeKey      = "args[\"topology-type\"]";
                topologyType = args["topology-type"];

                if (topologyType == null)
                    throw new ArgumentException("Topology arguments must specify [topology-type] if [typeKey] is null.");

                type = Config.Parse(topologyType, (System.Type)null);
            }
            else
                type = Config.Global.Get(typeKey, (System.Type)null);

            if (type == null || !typeof(ITopologyProvider).IsAssignableFrom(type))
                throw new ArgumentException(string.Format("Unable to map setting [{0}] to [ITopologyProvider].", typeKey));

            clusterEP = args["cluster-ep"];
            if (clusterEP == null)
                throw new ArgumentException("Topology arguments must specify [cluster-ep]");

            topology = Helper.CreateInstance<ITopologyProvider>(type);
            topology.OpenClient(router, clusterEP, args);

            return topology;
        }

        /// <summary>
        /// Instantiates and initializes a <see cref="ITopologyProvider" /> plug-in,
        /// reconstituting a client cluster serialized by a call to <see cref="ITopologyProvider.SerializeClient" />.
        /// </summary>
        /// <param name="router">The message router to associate with the cluster.</param>
        /// <param name="serialized">The string returned by <see cref="ITopologyProvider.SerializeClient" />.</param>
        /// <returns>The <see cref="ITopologyProvider" /> instance.</returns>
        /// <remarks>
        /// <para>
        /// This is useful for reconsititiung a client side topology on another process
        /// or server, perhaps for implementing a generic reliable messaging service.
        /// </para>
        /// </remarks>
        public static ITopologyProvider OpenClient(MsgRouter router, string serialized)
        {
            System.Type         type;
            ITopologyProvider   topology;
            ArgCollection       args;
            string              topologyType;
            string              clusterEP;

            if (!router.IsOpen)
                throw new InvalidOperationException(RouterClosedMsg);

            args = ArgCollection.Parse(serialized);

            topologyType = args["topology-type"];
            if (topologyType == null)
                throw new ArgumentException("Topology arguments must specify [topology-type].");

            type = Config.Parse(topologyType, (System.Type)null);
            if (type == null || !typeof(ITopologyProvider).IsAssignableFrom(type))
                throw new ArgumentException(string.Format("Unable to map [{0}] into an [ITopologyProvider].", topologyType));

            clusterEP = args["cluster-ep"];
            if (clusterEP == null)
                throw new ArgumentException("Topology arguments must specify [cluster-ep]");

            topology = Helper.CreateInstance<ITopologyProvider>(type);
            topology.OpenClient(router, clusterEP, args);

            return topology;
        }

        /// <summary>
        /// Instantiates and initializes an <see cref="ITopologyProvider" /> plug-in defined by a configuration setting
        /// and then opens it in server mode.
        /// </summary>
        /// <param name="router">The message router to associate with the topology.</param>
        /// <param name="dynamicScope">Specifies the dynamic scope to be matched when processing dynamic endpoints.</param>
        /// <param name="target">The target object whose message handlers are to be scanned for dynamic endpoints.</param>
        /// <param name="typeKey">The fully qualified configuration setting specifying the topology type specification (or <c>null</c>).</param>
        /// <param name="argsKey">The fully qualified configuration key specifying the topology arguments formatted for <see cref="ArgCollection" />.</param>
        /// <returns>The <see cref="ITopologyProvider" /> instance.</returns>
        /// <remarks>
        /// <para>
        /// This utility uses <see cref="Config.Get(string,System.Type)" /> to map the configuration setting
        /// to a .NET assembly and type and then instantiates an instance and then opens it as a server side
        /// topology.  argsKey names the configuration string to be be instantiated into a <see cref="ArgCollection" />
        /// and then passed to the topology's <see cref="ITopologyProvider.OpenServer" /> method.
        /// </para>
        /// <para>
        /// typeKey may be passed as null.  In this case, the configuration arguments must include a
        /// parameter named <b>topology-type</b> that defines the type assembly and class.
        /// </para>
        /// <note>
        /// The topology arguments must include an logical endpoint string argument named <b>cluster-ep</b>.
        /// This argument specifies the cluster endpoint to use when opening the topology.
        /// </note>
        /// </remarks>
        public static ITopologyProvider OpenServer(MsgRouter router, string dynamicScope, object target, string typeKey, string argsKey)
        {
            System.Type         type;
            ITopologyProvider   topology;
            ArgCollection       args;
            string              clusterEP;

            if (!router.IsOpen)
                throw new InvalidOperationException(RouterClosedMsg);

            args = ArgCollection.Parse(Config.Global.Get(argsKey));

            if (typeKey == null)
            {
                string topologyType;

                typeKey      = "args[\"topology-type\"]";
                topologyType = args["topology-type"];

                if (topologyType == null)
                    throw new ArgumentException("Topology arguments must specify [topology-type] if [typeKey] is null.");

                type = Config.Parse(topologyType, (System.Type)null);
            }
            else
                type = Config.Global.Get(typeKey, (System.Type)null);

            if (type == null || !typeof(ITopologyProvider).IsAssignableFrom(type))
                throw new ArgumentException(string.Format("Unable to map setting [{0}] into an [ITopologyProvider].", typeKey));

            clusterEP = args["cluster-ep"];
            if (clusterEP == null)
                throw new ArgumentException("Topology arguments must specify [cluster-ep]");

            topology = Helper.CreateInstance<ITopologyProvider>(type);
            topology.OpenServer(router, dynamicScope, clusterEP, target, args);

            return topology;
        }

        /// <summary>
        /// A utility for rendering a topology type into a string form suitable for
        /// serializing into the topology-type parameter of a <see cref="ITopologyProvider.SerializeClient" />
        /// result.
        /// </summary>
        /// <param name="type">The fully qualified name of the type implementing <see cref="ITopologyProvider" />.</param>
        /// <returns>The serialized type.</returns>
        /// <remarks>
        /// <note>
        /// This method doesn't generate the full path name of the type's
        /// assembly into the result.  Only the assembly file name will be included
        /// since assembly files may be located in a different place on the server
        /// or service where the topology is reconstituted.
        /// </note>
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown if the type passed does not implement [ITopologyProvider].</exception>
        public static string SerializeType(System.Type type)
        {
            if (!typeof(ITopologyProvider).IsAssignableFrom(type))
                throw new ArgumentException(string.Format("[{0}] does not implement [ITopologyProvider].", type.FullName));

            return string.Format("{0}:{1}", type.FullName, Path.GetFileName(type.Assembly.Location));
        }
    }
}
