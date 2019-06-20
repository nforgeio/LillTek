//-----------------------------------------------------------------------------
// FILE:        ServerManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the client side interface to remotely manage a 
//              Service Manager instance and the server machine where it's
//              deployed.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.Serialization;
using System.Security;
using System.ServiceModel;
using System.Text;

using LillTek.Common;
using LillTek.Net.Wcf;
using LillTek.Service;

namespace LillTek.Datacenter.ServerManagement
{
    /// <summary>
    /// Implements the client side interface to remotely manage a 
    /// Server Manager instance and the server machine where it's
    /// deployed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The LillTek Server Manager service is a Windows service to be 
    /// deployed on server machines to be remotely monitored and managed
    /// by applications like LillTek Sentinel.  This service exposes
    /// WCF endpoints that can be used to manage files, services and processes
    /// as well as to monitor system status via WMI queries and access
    /// to the Windows event log.
    /// </para>
    /// <para>
    /// Use the <see cref="ServerManager(WcfEndpoint)" /> constructor to create
    /// an instance, passing the <see cref="WcfEndpoint" /> specifying the URI
    /// and binding to be used to communicate with the remote instance.  Then
    /// call <see cref="Open" /> to establish a connection to the instance.
    /// </para>
    /// <para>
    /// Once a connection has been established, you may call methods such as
    /// <see cref="ListDrives" />, <see cref="ListFiles" />, <see cref="UploadFile" />, 
    /// <see cref="Execute" />, and <see cref="WmiQuery" /> to perform operations
    /// on the remote server (see the methods help topic for a complete list of
    /// operations implemented).  
    /// </para>
    /// <para>
    /// This class is threadsafe.  Each operation will block the current thread
    /// until it completes and the class allows only one thread to submit
    /// operations at any time.
    /// </para>
    /// <para>
    /// Call <see cref="Close" /> or <see cref="Dispose" /> when you are finished
    /// with the proxy so that any associated resources will be promptly released.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true" />
    public sealed class ServerManager : IServerManager, IDisposable
    {
        /// <summary>
        /// The <b>namespace</b> to be used for Server Manager service and data WCF contracts.
        /// </summary>
        public const string ContractNamespace = "http://lilltek.com/platform/ServerManager/2008/06/01";

        private const int FileTransferBlockSize = 64 * 1024;

        private object                              syncLock = new object();
        private WcfEndpoint                         serverEP;       // The WCF endpoint for the server manager instance
        private WcfChannelFactory<IServerManager>   proxyFactory;   // Factory for IServerManager proxies
        private WcfClientContext<IServerManager>    context;        // The server manager proxy context
        private IServerManager                      remote;         // The remote server manager proxy

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="serverEP">The <see cref="WcfEndpoint" /> for the server manager instance.</param>
        public ServerManager(WcfEndpoint serverEP)
        {
            if (serverEP == null)
                throw new ArgumentNullException("serverEP");

            this.serverEP = serverEP;
        }

        /// <summary>
        /// Establishes a connection with the remote server manager, if a connection
        /// has not already been established.
        /// </summary>
        /// <exception cref="TimeoutException">Thrown if the remote instance did not respond.</exception>
        /// <exception cref="CommunicationException">Thrown if communication with the remote instance failed.</exception>
        /// <exception cref="SecurityException">Thrown if the credentials are invalid or if security policy forbids the operation.</exception>
        public void Open()
        {
            lock (syncLock)
            {
                if (proxyFactory != null)
                    return;

                proxyFactory = new WcfChannelFactory<IServerManager>(serverEP);
                context      = null;
                remote       = null;

                try
                {
                    context = new WcfClientContext<IServerManager>(proxyFactory.CreateChannel());
                    context.Open();

                    remote = context.Proxy;

                    Ping();
                }
                catch
                {
                    if (context != null)
                        context.Close();

                    remote = null;

                    proxyFactory.Close();
                    proxyFactory = null;

                    throw;
                }
            }
        }

        /// <summary>
        /// Closes the connection if one has been established.
        /// </summary>
        public void Close()
        {
            lock (syncLock)
            {
                if (context != null)
                {
                    context.Close();
                    context = null;
                }

                remote = null;

                if (proxyFactory != null)
                {
                    proxyFactory.Dispose();
                    proxyFactory = null;
                }
            }
        }

        /// <summary>
        /// Closes the connection if one has been established.
        /// </summary>
        public void Dispose()
        {
            Close();
        }

        /// <summary>
        /// Verifies that the instance is open.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if the instance is not open.</exception>
        private void Verify()
        {
            if (remote == null)
                throw new ObjectDisposedException(this.GetType().FullName);
        }

        /// <summary>
        /// Uploads a file from the local machine to the remote machine.
        /// </summary>
        /// <param name="localSource">The fully qualified local file name.</param>
        /// <param name="remoteDestination">The fully qualified remote file name.</param>
        /// <remarks>
        /// <para>
        /// Any existing file on the remote machine will be overwritten by
        /// this method, even if the file is marked as read-only.
        /// </para>
        /// <note>
        /// Any missing parent directories will be created on the remote
        /// machine as necessary.
        /// </note>
        /// </remarks>
        public void UploadFile(string localSource, string remoteDestination)
        {
            lock (syncLock)
            {
                Verify();
                using (var fs = new FileStream(localSource, FileMode.Open, FileAccess.Read))
                {
                    Guid        fileID;
                    byte[]      buffer;
                    byte[]      data;
                    int         cb;
                    long        pos;

                    fileID = remote.CreateRemoteFile(remoteDestination);
                    buffer = new byte[FileTransferBlockSize];

                    try
                    {
                        while (true)
                        {
                            pos = fs.Position;
                            cb = fs.Read(buffer, 0, buffer.Length);
                            if (cb == 0)
                                break;

                            if (cb == buffer.Length)
                                data = buffer;
                            else
                                data = Helper.Extract(buffer, 0, cb);

                            remote.RemoteFileWrite(fileID, pos, data);
                        }

                        remote.CloseRemoteFile(fileID);
                    }
                    catch
                    {
                        remote.PurgeRemoteFile(fileID);
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Downloads a file from the remote machine to the local machine.
        /// </summary>
        /// <param name="remoteSource">The fully qualified remote file name.</param>
        /// <param name="localDestination">The fully qualified local file name.</param>
        /// <remarks>
        /// <para>
        /// Any existing file on the local machine will be overwritten by
        /// this method, even if the file is marked as read-only.
        /// </para>
        /// <note>
        /// Any missing parent directories will be created on the local
        /// machine as necessary.
        /// </note>
        /// </remarks>
        public void DownloadFile(string remoteSource, string localDestination)
        {
            lock (syncLock)
            {
                Verify();

                try
                {
                    using (var fs = new FileStream(localDestination, FileMode.Create, FileAccess.ReadWrite))
                    {
                        Guid    fileID;
                        byte[]  data;

                        fileID = remote.OpenRemoteFile(remoteSource);

                        while (true)
                        {
                            data = remote.RemoteFileRead(fileID, fs.Position, FileTransferBlockSize);
                            if (data == null)
                                break;

                            fs.Write(data, 0, data.Length);
                        }

                        remote.CloseRemoteFile(fileID);
                    }
                }
                catch
                {
                    Helper.DeleteFile(localDestination);
                    throw;
                }
            }
        }

        /// <summary>
        /// Sets the time on the remote machine to the current time on this machine.
        /// </summary>
        public void SynchronizeTime()
        {
            SetTime(DateTime.UtcNow);
        }

        //---------------------------------------------------------------------
        // IServerManager implementation

        /// <summary>
        /// Verifies that the server manager instance is present, running, 
        /// and any required security credentials are valid.
        /// </summary>
        public void Ping()
        {
            lock (syncLock)
            {
                Verify();
                remote.Ping();
            }
        }

        /// <summary>
        /// Lists the disk drives available on the remote machine.
        /// </summary>
        /// <returns>Array of drive information.</returns>
        public RemoteDrive[] ListDrives()
        {
            lock (syncLock)
            {
                Verify();
                return remote.ListDrives();
            }
        }

        /// <summary>
        /// Returns the fully qualified path to a special folder on the remote machine.
        /// </summary>
        /// <param name="folder">Identifies the requested folder.</param>
        public string GetFolderPath(RemoteSpecialFolder folder)
        {
            lock (syncLock)
            {
                Verify();
                return remote.GetFolderPath(folder);
            }
        }

        /// <summary>
        /// Lists the files within a directory on the remote machine.
        /// </summary>
        /// <param name="path">Fully qualified path to the directory on the remote machine.</param>
        /// <param name="pattern">The search pattern (or <c>null</c>).</param>
        /// <param name="searchOption">Indicates whether files in child folders should also be listed.</param>
        /// <returns>Array of file information records.</returns>
        public RemoteFile[] ListFiles(string path, string pattern, SearchOption searchOption)
        {
            lock (syncLock)
            {
                Verify();
                return remote.ListFiles(path, pattern, searchOption);
            }
        }

        /// <summary>
        /// Lists the folders within a directory on the remote machine.
        /// </summary>
        /// <param name="path">Fully qualified path to the directory on the remote machine.</param>
        /// <returns>Array of directory information records.</returns>
        public RemoteFile[] ListFolders(string path)
        {
            lock (syncLock)
            {
                Verify();
                return remote.ListFolders(path);
            }
        }

        /// <summary>
        /// Determines whether a file exists on the remote machine.
        /// </summary>
        /// <param name="path">The fully qualified path to the file.</param>
        /// <returns><c>true</c> if the file exists.</returns>
        public bool FileExists(string path)
        {
            lock (syncLock)
            {
                Verify();
                return remote.FileExists(path);
            }
        }

        /// <summary>
        /// Determines whether a folder exists on the remote machine.
        /// </summary>
        /// <param name="path">The fully qualified path to the folder.</param>
        /// <returns><c>true</c> if the folder exists.</returns>
        public bool FolderExists(string path)
        {
            lock (syncLock)
            {
                Verify();
                return remote.FolderExists(path);
            }
        }

        /// <summary>
        /// Deletes a file or directory on the remote machine.
        /// </summary>
        /// <param name="path">The fully qualified name of the file (including optional wildcards).</param>
        /// <remarks>
        /// This method supports wildcards and also implements the
        /// recursive deletion of directories.
        /// </remarks>
        public void DeleteFile(string path)
        {
            lock (syncLock)
            {
                Verify();
                remote.DeleteFile(path);
            }
        }

        /// <summary>
        /// Creates a directory on the remote machine.
        /// </summary>
        /// <param name="path">The fully qualfied path of the directory.</param>
        /// <remarks>
        /// <note>
        /// It is not an error to call this method for a directory
        /// that already exists.
        /// </note>
        /// </remarks>
        public void CreateDirectory(string path)
        {
            lock (syncLock)
            {
                Verify();
                remote.CreateDirectory(path);
            }
        }

        /// <summary>
        /// Deletes a directory on the remote machine, recursively deleting any files
        /// and subdirectories contained within.
        /// </summary>
        /// <param name="path">The fully qualfied name of the directory.</param>
        public void DeleteDirectory(string path)
        {
            lock (syncLock)
            {
                Verify();
                remote.DeleteDirectory(path);
            }
        }

        /// <summary>
        /// Copies files on the remote machine.
        /// </summary>
        /// <param name="source">The fully qualified name of the source file with optional wildcards.</param>
        /// <param name="destination">The fully qualified name of the destination file.</param>
        /// <param name="recursive"><c>true</c> to recursively copy directories.</param>
        /// <remarks>
        /// This method is able to copy a single file, or a set of files that
        /// match a wildcard pattern.  The method can also tree copy directories
        /// from one location to another if <paramref name="recursive" /> is <c>true</c>.
        /// This method implements the same behavior as <see cref="Helper.CopyFile(string,string,bool)" />.
        /// </remarks>
        public void CopyFile(string source, string destination, bool recursive)
        {
            lock (syncLock)
            {
                Verify();
                remote.CopyFile(source, destination, recursive);
            }
        }

        /// <summary>
        /// Opens an existing file on the remote machine for reading.
        /// </summary>
        /// <param name="path">The fully qualified remote file path.</param>
        /// <returns>The file ID to be used for subsequent read operations.</returns>
        /// <remarks>
        /// This method actually copies the remote file to a temporary location
        /// and any subsequent <see cref="RemoteFileRead" /> operations will be
        /// performed from the copy.  The temporary file will be deleted when
        /// <see cref="CloseRemoteFile" /> or if the file has not been accessed
        /// for a long period of time.
        /// </remarks>
        public Guid OpenRemoteFile(string path)
        {
            lock (syncLock)
            {
                Verify();
                return remote.OpenRemoteFile(path);
            }
        }

        /// <summary>
        /// Creates a file on the remote machine for writing.
        /// </summary>
        /// <param name="path">The fully qualified remote file path.</param>
        /// <returns>The file ID to be used for subsequent write operations.</returns>
        /// <remarks>
        /// This method actually creates a temporary file on the remote machine,
        /// where subsequent calls to <see cref="RemoteFileWrite" /> will write their
        /// data.  The temporary file will be copied to the specified path
        /// when <see cref="CloseRemoteFile" /> is called.
        /// </remarks>
        public Guid CreateRemoteFile(string path)
        {
            lock (syncLock)
            {
                Verify();
                return remote.CreateRemoteFile(path);
            }
        }

        /// <summary>
        /// Reads a block of data from a remote file.
        /// </summary>
        /// <param name="fileID">The <see cref="Guid" /> returned by <see cref="OpenRemoteFile" />.</param>
        /// <param name="position">The file position of the first byte to be read.</param>
        /// <param name="count">The number of bytes to read.</param>
        /// <returns>The data actually read or <c>null</c> if the end of the file has been reached.</returns>
        public byte[] RemoteFileRead(Guid fileID, long position, int count)
        {
            lock (syncLock)
            {
                Verify();
                return remote.RemoteFileRead(fileID, position, count);
            }
        }

        /// <summary>
        /// Writes a block of data to a remote file.
        /// </summary>
        /// <param name="fileID">The <see cref="Guid" /> returned by <see cref="CreateRemoteFile" />.</param>
        /// <param name="position">The file position of the first byte to be written.</param>
        /// <param name="data">The data to be written.</param>
        public void RemoteFileWrite(Guid fileID, long position, byte[] data)
        {
            lock (syncLock)
            {
                Verify();
                remote.RemoteFileWrite(fileID, position, data);
            }
        }

        /// <summary>
        /// Closes a remote file.
        /// </summary>
        /// <param name="fileID">The <see cref="Guid" /> returned by <see cref="OpenRemoteFile" /> or <see cref="CreateRemoteFile" />.</param>
        /// <remarks>
        /// Files created on the remote machine via <see cref="CreateRemoteFile" /> will be copied
        /// to the target path by this method.  Note that any missing parent directories on the
        /// remote machine will be created as necessary before copying the file.
        /// </remarks>
        public void CloseRemoteFile(Guid fileID)
        {
            lock (syncLock)
            {
                Verify();
                remote.CloseRemoteFile(fileID);
            }
        }

        /// <summary>
        /// Closes and deletes a remote file being written.
        /// </summary>
        /// <param name="fileID">The <see cref="Guid" /> returned by <see cref="CreateRemoteFile" />.</param>
        /// <remarks>
        /// Use this method to abort file uploading.  This method closes the temporary remote file and deletes
        /// it without copying it to its file destination.
        /// </remarks>
        public void PurgeRemoteFile(Guid fileID)
        {
            lock (syncLock)
            {
                Verify();
                remote.PurgeRemoteFile(fileID);
            }
        }

        /// <summary>
        /// Executes a program on the remote machine.
        /// </summary>
        /// <param name="path">The fully qualified path of the program on the remote machine.</param>
        /// <param name="args">The arguments to be passed.</param>
        /// <param name="stdOutput">Will be set to the redirected standard output.</param>
        /// <param name="stdError">Will be set to the redirected standard error.</param>
        /// <param name="timeout">The maximum time to wait for the process to complete.</param>
        /// <returns>The process exit code.</returns>
        /// <remarks>
        /// Pass timeout=TimeSpan.Zero to wait forever.
        /// </remarks>
        public int Execute(string path, string args, out string stdOutput, out string stdError, TimeSpan timeout)
        {
            lock (syncLock)
            {
                Verify();
                return remote.Execute(path, args, out stdOutput, out stdError, timeout);
            }
        }

        /// <summary>
        /// Restarts the remote machine.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This method will notify the server to restart, then close
        /// the session and then return immediately, without waiting for any
        /// kind of acknowledgment from the server.
        /// </note>
        /// </remarks>
        public void Reboot()
        {
            lock (syncLock)
            {
                Verify();
                remote.Reboot();
            }
        }

        /// <summary>
        /// Shuts down the remote machine.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This method will notify the server to shutdown, then close
        /// the session and then return immediately, without waiting for any
        /// kind of acknowledgment from the server.
        /// </note>
        /// </remarks>
        public void PowerDown()
        {
            lock (syncLock)
            {
                Verify();
                remote.PowerDown();
            }
        }

        /// <summary>
        /// Returns information about the services running on the remote machine.
        /// </summary>
        /// <returns>A <see cref="RemoteServiceInfo" /> array describing the installed services.</returns>
        public RemoteServiceInfo[] ListServices()
        {
            lock (syncLock)
            {
                Verify();
                return remote.ListServices();
            }
        }

        /// <summary>
        /// Returns information about a particular service on the remote machine. 
        /// </summary>
        /// <param name="serviceName">Name of the service.</param>
        /// <returns>
        /// A <see cref="RemoteServiceInfo" /> instance describing the service if 
        /// it exists, <c>null</c> if the requested service is not installed.
        /// </returns>
        public RemoteServiceInfo GetServiceStatus(string serviceName)
        {
            lock (syncLock)
            {
                Verify();
                return remote.GetServiceStatus(serviceName);
            }
        }

        /// <summary>
        /// Starts a named service if it's not already running on a remote machine.
        /// </summary>
        /// <param name="serviceName">Name of the service to start.</param>
        public void StartService(string serviceName)
        {
            lock (syncLock)
            {
                Verify();
                remote.StartService(serviceName);
            }
        }

        /// <summary>
        /// Stops a service if it is running on a remote machine.
        /// </summary>
        /// <param name="serviceName">Name of the service to be stopped.</param>
        public void StopService(string serviceName)
        {
            lock (syncLock)
            {
                Verify();
                remote.StopService(serviceName);
            }
        }

        /// <summary>
        /// Sets a service's start mode on a remote machine.
        /// </summary>
        /// <param name="serviceName">The service name.</param>
        /// <param name="startMode">The start mode to set.</param>
        public void SetServiceStartMode(string serviceName, RemoteServiceStartMode startMode)
        {
            lock (syncLock)
            {
                Verify();
                remote.SetServiceStartMode(serviceName, startMode);
            }
        }

        /// <summary>
        /// Returns information about the processes running on a remote machine.
        /// </summary>
        /// <param name="computeCpuUtilization">Pass <c>true</c> to calculate CPU utilization for the processes.</param>
        /// <returns>The array of <see cref="RemoteProcess" /> values.</returns>
        public RemoteProcess[] ListProcesses(bool computeCpuUtilization)
        {
            lock (syncLock)
            {
                Verify();
                return remote.ListProcesses(computeCpuUtilization);
            }
        }

        /// <summary>
        /// Kills a process running on a remote machine.
        /// </summary>
        /// <param name="processID">The ID of the process to be killed.</param>
        public void KillProcess(int processID)
        {
            lock (syncLock)
            {
                Verify();
                remote.KillProcess(processID);
            }
        }

        /// <summary>
        /// Sets the time on the remote machine.
        /// </summary>
        /// <param name="time">The time to set (UTC).</param>
        public void SetTime(DateTime time)
        {
            lock (syncLock)
            {
                Verify();
                remote.SetTime(time);
            }
        }

        /// <summary>
        /// Performs one or more WMI queries on the remote machine.
        /// </summary>
        /// <param name="queries">The set of queries to be performed.</param>
        /// <returns>The query results.</returns>
        public WmiResultSet WmiQuery(params WmiQuery[] queries)
        {
            lock (syncLock)
            {
                Verify();
                return remote.WmiQuery(queries);
            }
        }
    }
}
