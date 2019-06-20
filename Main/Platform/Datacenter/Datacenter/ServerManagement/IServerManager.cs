//-----------------------------------------------------------------------------
// FILE:        IServerManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the client side interface to a ServerManagerHandler.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;

using LillTek.Common;
using LillTek.Service;

namespace LillTek.Datacenter.ServerManagement
{
    /// <summary>
    /// Defines the web service interface exposed by Server Manager services
    /// for remote server monitoring and control.
    /// </summary>
    [ServiceContract(Namespace = ServerManager.ContractNamespace)]
    public interface IServerManager
    {
        /// <summary>
        /// Verifies that the server manager instance is present, running, 
        /// and any required security credentials are valid.
        /// </summary>
        [OperationContract]
        void Ping();

        /// <summary>
        /// Lists the disk drives available on the remote machine.
        /// </summary>
        /// <returns>Array of drive information.</returns>
        [OperationContract]
        RemoteDrive[] ListDrives();

        /// <summary>
        /// Returns the fully qualified path to a special folder on the remote machine.
        /// </summary>
        /// <param name="folder">Identifies the requested folder.</param>
        [OperationContract]
        string GetFolderPath(RemoteSpecialFolder folder);

        /// <summary>
        /// Lists the files within a directory on the remote machine.
        /// </summary>
        /// <param name="path">Fully qualified path to the directory on the remote machine.</param>
        /// <param name="pattern">The search pattern (or <c>null</c>).</param>
        /// <param name="searchOption">Indicates whether files in child folders should also be listed.</param>
        /// <returns>Array of file information records.</returns>
        [OperationContract]
        RemoteFile[] ListFiles(string path, string pattern, SearchOption searchOption);

        /// <summary>
        /// Lists the folders within a directory on the remote machine.
        /// </summary>
        /// <param name="path">Fully qualified path to the directory on the remote machine.</param>
        /// <returns>Array of directory information records.</returns>
        [OperationContract]
        RemoteFile[] ListFolders(string path);

        /// <summary>
        /// Determines whether a file exists on the remote machine.
        /// </summary>
        /// <param name="path">The fully qualified path to the file.</param>
        /// <returns><c>true</c> if the file exists.</returns>
        [OperationContract]
        bool FileExists(string path);

        /// <summary>
        /// Determines whether a folder exists on the remote machine.
        /// </summary>
        /// <param name="path">The fully qualified path to the folder.</param>
        /// <returns><c>true</c> if the folder exists.</returns>
        [OperationContract]
        bool FolderExists(string path);

        /// <summary>
        /// Deletes a file or directory on the remote machine.
        /// </summary>
        /// <param name="path">The fully qualified name of the file (including optional wildcards).</param>
        /// <remarks>
        /// This method supports wildcards and also implements the
        /// recursive deletion of directories.
        /// </remarks>
        [OperationContract]
        void DeleteFile(string path);

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
        [OperationContract]
        void CreateDirectory(string path);

        /// <summary>
        /// Deletes a directory on the remote machine, recursively deleting any files
        /// and subdirectories contained within.
        /// </summary>
        /// <param name="path">The fully qualfied name of the directory.</param>
        [OperationContract]
        void DeleteDirectory(string path);

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
        [OperationContract]
        void CopyFile(string source, string destination, bool recursive);

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
        [OperationContract]
        Guid OpenRemoteFile(string path);

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
        [OperationContract]
        Guid CreateRemoteFile(string path);

        /// <summary>
        /// Reads a block of data from a remote file.
        /// </summary>
        /// <param name="fileID">The <see cref="Guid" /> returned by <see cref="OpenRemoteFile" />.</param>
        /// <param name="position">The file position of the first byte to be read.</param>
        /// <param name="count">The number of bytes to read.</param>
        /// <returns>The data actually read or <c>null</c> if the end of the file has been reached.</returns>
        [OperationContract]
        byte[] RemoteFileRead(Guid fileID, long position, int count);

        /// <summary>
        /// Writes a block of data to a remote file.
        /// </summary>
        /// <param name="fileID">The <see cref="Guid" /> returned by <see cref="CreateRemoteFile" />.</param>
        /// <param name="position">The file position of the first byte to be written.</param>
        /// <param name="data">The data to be written.</param>
        [OperationContract]
        void RemoteFileWrite(Guid fileID, long position, byte[] data);

        /// <summary>
        /// Closes a remote file.
        /// </summary>
        /// <param name="fileID">The <see cref="Guid" /> returned by <see cref="OpenRemoteFile" /> or <see cref="CreateRemoteFile" />.</param>
        /// <remarks>
        /// Files created on the remote machine via <see cref="CreateRemoteFile" /> will be copied
        /// to the target path by this method.  Note that any missing parent directories on the
        /// remote machine will be created as necessary before copying the file.
        /// </remarks>
        [OperationContract]
        void CloseRemoteFile(Guid fileID);

        /// <summary>
        /// Closes and deletes a remote file being written.
        /// </summary>
        /// <param name="fileID">The <see cref="Guid" /> returned by <see cref="CreateRemoteFile" />.</param>
        /// <remarks>
        /// Use this method to abort file uploading.  This method closes the temporary remote file and deletes
        /// it without copying it to its file destination.
        /// </remarks>
        [OperationContract]
        void PurgeRemoteFile(Guid fileID);

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
        [OperationContract]
        int Execute(string path, string args, out string stdOutput, out string stdError, TimeSpan timeout);

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
        [OperationContract]
        void Reboot();

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
        [OperationContract]
        void PowerDown();

        /// <summary>
        /// Returns information about the services running on the remote machine.
        /// </summary>
        /// <returns>A <see cref="RemoteServiceInfo" /> array describing the installed services.</returns>
        [OperationContract]
        RemoteServiceInfo[] ListServices();

        /// <summary>
        /// Returns information about a particular service on the remote machine. 
        /// </summary>
        /// <param name="serviceName">Name of the service.</param>
        /// <returns>
        /// A <see cref="RemoteServiceInfo" /> instance describing the service if 
        /// it exists, <c>null</c> if the requested service is not installed.
        /// </returns>
        [OperationContract]
        RemoteServiceInfo GetServiceStatus(string serviceName);

        /// <summary>
        /// Starts a named service if it's not already running on a remote machine.
        /// </summary>
        /// <param name="serviceName">Name of the service to start.</param>
        [OperationContract]
        void StartService(string serviceName);

        /// <summary>
        /// Stops a service if it is running on a remote machine.
        /// </summary>
        /// <param name="serviceName">Name of the service to be stopped.</param>
        [OperationContract]
        void StopService(string serviceName);

        /// <summary>
        /// Sets a service's start mode on a remote machine.
        /// </summary>
        /// <param name="serviceName">The service name.</param>
        /// <param name="startMode">The start mode to set.</param>
        [OperationContract]
        void SetServiceStartMode(string serviceName, RemoteServiceStartMode startMode);

        /// <summary>
        /// Returns information about the processes running on a remote machine.
        /// </summary>
        /// <param name="computeCpuUtilization">Pass <c>true</c> to calculate CPU utilization for the processes.</param>
        /// <returns>The array of <see cref="RemoteProcess" /> values.</returns>
        [OperationContract]
        RemoteProcess[] ListProcesses(bool computeCpuUtilization);

        /// <summary>
        /// Kills a process running on a remote machine.
        /// </summary>
        /// <param name="processID">The ID of the process to be killed.</param>
        [OperationContract]
        void KillProcess(int processID);

        /// <summary>
        /// Sets the time on the remote machine.
        /// </summary>
        /// <param name="time">The time to set (UTC).</param>
        [OperationContract]
        void SetTime(DateTime time);

        /// <summary>
        /// Performs one or more WMI queries on the remote machine.
        /// </summary>
        /// <param name="queries">The set of queries to be performed.</param>
        /// <returns>The query results.</returns>
        [OperationContract]
        WmiResultSet WmiQuery(params WmiQuery[] queries);
    }
}
