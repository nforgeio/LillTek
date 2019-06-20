//-----------------------------------------------------------------------------
// FILE:        _ServerManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests.

#if TODO

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Reflection;
using System.Threading;

using LillTek.Common;
using LillTek.Datacenter;
using LillTek.Datacenter.Server;
using LillTek.Messaging;
using LillTek.Service;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Datacenter.Server.Test
{
    [TestClass]
    public class _ServerManager
    {
        private LeafRouter router = null;
        private ServerManagerHandler handler = null;

        [TestInitialize]
        public void Initialize()
        {
            NetTrace.Start();
            NetTrace.Enable(MsgRouter.TraceSubsystem, 0);

            const string settings =
@"
&section MsgRouter

    AppName                = Test
    AppDescription         = Test Description
    RouterEP			   = physical://detached/test/leaf
    CloudEP    			   = $(LillTek.DC.CloudEP)
    CloudAdapter    	   = ANY
    UdpEP				   = ANY:0
    TcpEP				   = ANY:0
    TcpBacklog			   = 100
    TcpDelay			   = off
    BkInterval			   = 1s
    MaxIdle				   = 5m
    EnableP2P              = yes
    AdvertiseTime		   = 1m
    DefMsgTTL			   = 5
    SharedKey		 	   = PLAINTEXT
    SessionCacheTime       = 2m
    SessionRetries         = 3
    SessionTimeout         = 10s
    MaxLogicalAdvertiseEPs = 256
    DeadRouterTTL          = 2s

&endsection
";
            Config.SetConfig(settings.Replace('&', '#'));
            MsgEP.ReloadAbstractMap();

            router = new LeafRouter();
            handler = new ServerManagerHandler();

            handler.Start(router, null, null, null);
            router.Start();
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (handler != null)
                handler.Stop();

            if (router != null)
                router.Stop();

            NetTrace.Stop();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ServerManager_Connect()
        {
            ServerManager sm = new ServerManager(router);

            Assert.IsFalse(sm.IsConnected);
            sm.Connect(ServiceManager.AbstractEP);
            Assert.IsTrue(sm.IsConnected);
            sm.Disconnect();
            Assert.IsFalse(sm.IsConnected);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ServerManager_ListDrives()
        {
            ServiceManager sm = new ServiceManager(router);
            RemoteDriveInfo[] drives;
            string[] local;
            bool ok;

            sm.Connect(ServiceManager.AbstractEP);
            try
            {
                drives = sm.ListDrives();
                local = Environment.GetLogicalDrives();

                Assert.AreEqual(local.Length, drives.Length);

                for (int i = 0; i < local.Length; i++)
                {
                    ok = false;
                    for (int j = 0; j < drives.Length; j++)
                        if (local[i] == drives[j].Name + "\\")
                        {

                            ok = true;
                            break;
                        }

                    Assert.IsTrue(ok);
                }
            }
            finally
            {
                sm.Disconnect();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ServerManager_GetFolderPath()
        {
            ServiceManager sm = new ServiceManager(router);

            sm.Connect(ServiceManager.AbstractEP);
            try
            {
                Assert.AreEqual(Path.GetTempPath(), sm.GetFolderPath(RemoteSpecialFolder.Temporary));
                Assert.AreEqual(Environment.GetFolderPath(Environment.SpecialFolder.System), sm.GetFolderPath(RemoteSpecialFolder.System));
                Assert.AreEqual(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), sm.GetFolderPath(RemoteSpecialFolder.ProgramFiles));

                string path;
                int pos;

                path = Assembly.GetExecutingAssembly().CodeBase;
                Assert.IsTrue(path.StartsWith("file://"));
                path = path.Substring(8);
                pos = path.LastIndexOf('/');
                path = path.Substring(0, pos);

                Assert.AreEqual(path, sm.GetFolderPath(RemoteSpecialFolder.ServiceManager));
            }
            finally
            {
                sm.Disconnect();
            }
        }

        private bool CheckFile(RemoteFileInfo[] fi, string path, bool isFolder)
        {
            for (int i = 0; i < fi.Length; i++)
                if (String.Compare(fi[i].FullName, path, true) == 0)
                    return (fi[i].Attributes & FileAttributes.Directory) != 0 == isFolder;

            return false;
        }

        private void CreateFile(string folder, string fname, bool readOnly)
        {
            StreamWriter writer;
            string path;

            if (folder.EndsWith("\\"))
                path = folder + fname;
            else
                path = folder + "\\" + fname;

            writer = new StreamWriter(path);
            writer.WriteLine("Hello World!");
            writer.Close();

            if (readOnly)
                File.SetAttributes(path, FileAttributes.ReadOnly);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ServerManager_ListFiles()
        {
            string folder = Path.GetTempPath() + Helper.NewGuid().ToString();
            ServiceManager sm;

            Directory.CreateDirectory(folder);

            Directory.CreateDirectory(folder + "\\src");
            CreateFile(folder + "\\src", "test1.dat", false);
            CreateFile(folder + "\\src", "test2.txt", false);
            CreateFile(folder + "\\src", "test3.txt", false);

            Directory.CreateDirectory(folder + "\\src\\0");
            CreateFile(folder + "\\src\\0", "test1.txt", false);
            CreateFile(folder + "\\src\\0", "test2.txt", false);
            CreateFile(folder + "\\src\\0", "test3.txt", false);

            Directory.CreateDirectory(folder + "\\src\\1");
            CreateFile(folder + "\\src\\1", "test1.txt", false);
            CreateFile(folder + "\\src\\1", "test2.txt", false);
            CreateFile(folder + "\\src\\1", "test3.txt", false);

            Directory.CreateDirectory(folder + "\\src\\2");
            CreateFile(folder + "\\src\\2", "test1.txt", false);
            CreateFile(folder + "\\src\\2", "test2.txt", false);
            CreateFile(folder + "\\src\\2", "test3.txt", false);

            Directory.CreateDirectory(folder + "\\src\\3");
            CreateFile(folder + "\\src\\3", "test1.txt", false);
            CreateFile(folder + "\\src\\3", "test2.txt", false);
            CreateFile(folder + "\\src\\3", "test3.txt", false);

            Directory.CreateDirectory(folder + "\\dst");

            sm = new ServiceManager(router);
            sm.Connect(ServiceManager.AbstractEP);
            try
            {
                RemoteFileInfo[] fi;

                fi = sm.ListFiles(folder + "\\src", null);
                Assert.IsTrue(CheckFile(fi, folder + "\\src\\test1.dat", false));
                Assert.IsTrue(CheckFile(fi, folder + "\\src\\test2.txt", false));
                Assert.IsTrue(CheckFile(fi, folder + "\\src\\test3.txt", false));
                Assert.IsTrue(CheckFile(fi, folder + "\\src\\0", true));
                Assert.IsTrue(CheckFile(fi, folder + "\\src\\1", true));
                Assert.IsTrue(CheckFile(fi, folder + "\\src\\2", true));
                Assert.IsTrue(CheckFile(fi, folder + "\\src\\3", true));

                fi = sm.ListFiles(folder + "\\src", "*.dat");
                Assert.AreEqual(1, fi.Length);
                Assert.IsTrue(CheckFile(fi, folder + "\\src\\test1.dat", false));

                fi = sm.ListFiles(folder + "\\src", "*.txt");
                Assert.IsTrue(CheckFile(fi, folder + "\\src\\test2.txt", false));
                Assert.IsTrue(CheckFile(fi, folder + "\\src\\test3.txt", false));
            }
            finally
            {
                sm.Disconnect();
                Helper.DeleteFile(folder, true);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ServerManager_DeleteFile()
        {
            string folder = Path.GetTempPath() + Helper.NewGuid().ToString();
            ServiceManager sm;

            Directory.CreateDirectory(folder);

            Directory.CreateDirectory(folder + "\\src");
            CreateFile(folder + "\\src", "test1.dat", false);
            CreateFile(folder + "\\src", "test2.txt", false);
            CreateFile(folder + "\\src", "test3.txt", false);

            Directory.CreateDirectory(folder + "\\src\\0");
            CreateFile(folder + "\\src\\0", "test1.txt", false);
            CreateFile(folder + "\\src\\0", "test2.txt", false);
            CreateFile(folder + "\\src\\0", "test3.txt", false);

            Directory.CreateDirectory(folder + "\\src\\1");
            CreateFile(folder + "\\src\\1", "test1.txt", false);
            CreateFile(folder + "\\src\\1", "test2.txt", false);
            CreateFile(folder + "\\src\\1", "test3.txt", false);

            Directory.CreateDirectory(folder + "\\src\\2");
            CreateFile(folder + "\\src\\2", "test1.txt", false);
            CreateFile(folder + "\\src\\2", "test2.txt", false);
            CreateFile(folder + "\\src\\2", "test3.txt", false);

            Directory.CreateDirectory(folder + "\\src\\3");
            CreateFile(folder + "\\src\\3", "test1.txt", false);
            CreateFile(folder + "\\src\\3", "test2.txt", false);
            CreateFile(folder + "\\src\\3", "test3.txt", false);

            sm = new ServiceManager(router);
            sm.Connect(ServiceManager.AbstractEP);
            try
            {
                sm.DeleteFile(folder + "\\src\\*.dat");
                Assert.IsFalse(File.Exists(folder + "\\src\\test1.dat"));
                Assert.IsTrue(File.Exists(folder + "\\src\\test2.txt"));
                Assert.IsTrue(File.Exists(folder + "\\src\\test3.txt"));

                sm.DeleteFile(folder + "\\src");
                Assert.IsFalse(Directory.Exists(folder + "\\src"));
            }
            finally
            {
                sm.Disconnect();
                Helper.DeleteFile(folder, true);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ServerManager_CreateDirectory()
        {
            string folder = Path.GetTempPath() + Helper.NewGuid().ToString();
            ServiceManager sm;

            Directory.CreateDirectory(folder);

            sm = new ServiceManager(router);
            sm.Connect(ServiceManager.AbstractEP);
            try
            {
                sm.CreateDirectory(folder + "\\test");
                Assert.IsTrue(Directory.Exists(folder + "\\test"));
            }
            finally
            {
                sm.Disconnect();
                Helper.DeleteFile(folder, true);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ServerManager_DeleteDirectory()
        {
            string folder = Path.GetTempPath() + Helper.NewGuid().ToString();
            ServiceManager sm;

            Directory.CreateDirectory(folder);
            Directory.CreateDirectory(folder + "\\test");

            sm = new ServiceManager(router);
            sm.Connect(ServiceManager.AbstractEP);
            try
            {
                sm.DeleteDirectory(folder + "\\test");
                Assert.IsFalse(Directory.Exists(folder + "\\test"));
            }
            finally
            {
                sm.Disconnect();
                Helper.DeleteFile(folder, true);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ServerManager_CopyFile()
        {
            string folder = Path.GetTempPath() + Helper.NewGuid().ToString();
            ServiceManager sm;

            Directory.CreateDirectory(folder);
            Directory.CreateDirectory(folder + "\\src");
            CreateFile(folder + "\\src", "test1.txt", false);

            sm = new ServiceManager(router);
            sm.Connect(ServiceManager.AbstractEP);
            try
            {
                sm.CopyFile(folder + "\\src\\test1.txt", folder + "\\src\\test2.txt", false);
                Assert.IsTrue(File.Exists(folder + "\\src\\test2.txt"));
            }
            finally
            {
                sm.Disconnect();
                Helper.DeleteFile(folder, true);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ServerManager_UploadFile()
        {
            string folder = Path.GetTempPath() + Helper.NewGuid().ToString();
            ServiceManager sm;
            StreamWriter writer;
            StreamReader reader;
            string s;

            Directory.CreateDirectory(folder);

            sm = new ServiceManager(router);
            sm.Connect(ServiceManager.AbstractEP);
            try
            {
                writer = new StreamWriter(folder + "\\source.txt");
                writer.Write("Hello world!");
                writer.Close();

                sm.UploadFile(folder + "\\source.txt", folder + "\\upload.txt");

                Assert.IsTrue(File.Exists(folder + "\\upload.txt"));

                reader = new StreamReader(folder + "\\upload.txt");
                s = reader.ReadToEnd();
                reader.Close();

                Assert.AreEqual("Hello world!", s);
            }
            finally
            {
                sm.Disconnect();
                Helper.DeleteFile(folder, true);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ServerManager_DownloadFile()
        {
            string folder = Path.GetTempPath() + Helper.NewGuid().ToString();
            ServiceManager sm;
            StreamWriter writer;
            StreamReader reader;
            string s;

            Directory.CreateDirectory(folder);

            sm = new ServiceManager(router);
            sm.Connect(ServiceManager.AbstractEP);
            try
            {
                writer = new StreamWriter(folder + "\\source.txt");
                writer.Write("Hello world!");
                writer.Close();

                sm.DownloadFile(folder + "\\source.txt", folder + "\\download.txt");

                Assert.IsTrue(File.Exists(folder + "\\download.txt"));

                reader = new StreamReader(folder + "\\download.txt");
                s = reader.ReadToEnd();
                reader.Close();

                Assert.AreEqual("Hello world!", s);
            }
            finally
            {
                sm.Disconnect();
                Helper.DeleteFile(folder, true);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ServerManager_Reboot()
        {
            ServiceManager sm;

            sm = new ServiceManager(router);
            sm.Connect(ServiceManager.AbstractEP);
            try
            {
                Assert.Ignore("Test must be run manually.");

                sm.Reboot();
            }
            finally
            {
                sm.Disconnect();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ServerManager_Shutdown()
        {
            ServiceManager sm;

            sm = new ServiceManager(router);
            sm.Connect(ServiceManager.AbstractEP);
            try
            {
                Assert.Ignore("Test must be run manually.");

                sm.Shutdown();
            }
            finally
            {
                sm.Disconnect();
            }
        }

        private RemoteServiceInfo FindService(string serviceName, RemoteServiceInfo[] rsi)
        {
            for (int i = 0; i < rsi.Length; i++)
                if (String.Compare(serviceName, rsi[i].ServiceName, true) == 0)
                    return rsi[i];

            return null;
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ServerManager_ListServices()
        {
            ServiceManager sm;
            RemoteServiceInfo[] rsi;
            RemoteServiceInfo service;

            sm = new ServiceManager(router);
            sm.Connect(ServiceManager.AbstractEP);
            try
            {
                rsi = sm.ListServices();
                service = FindService("Spooler", rsi);
                Assert.IsNotNull(service);
            }
            finally
            {
                sm.Disconnect();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ServerManager_StartStopService()
        {
            ServiceManager sm;
            RemoteServiceInfo[] rsi;
            RemoteServiceInfo service;

            sm = new ServiceManager(router);
            sm.Connect(ServiceManager.AbstractEP);
            try
            {
                sm.StopService("Spooler");
                Thread.Sleep(2000);

                rsi = sm.ListServices();
                service = FindService("Spooler", rsi);
                Assert.IsNotNull(service);
                Assert.AreEqual(ServiceState.Stopped, service.State);

                sm.StartService("Spooler");
                Thread.Sleep(2000);

                rsi = sm.ListServices();
                service = FindService("Spooler", rsi);
                Assert.IsNotNull(service);
                Assert.AreEqual(ServiceState.Running, service.State);
            }
            finally
            {
                sm.Disconnect();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ServerManager_SetServiceStartMode()
        {
            ServiceManager sm;
            RemoteServiceInfo[] rsi;
            RemoteServiceInfo service;

            sm = new ServiceManager(router);
            sm.Connect(ServiceManager.AbstractEP);
            try
            {
                rsi = sm.ListServices();
                service = FindService("Spooler", rsi);
                Assert.IsNotNull(service);

                sm.SetServiceStartMode("Spooler", RemoteServiceStartMode.Disabled);
                rsi = sm.ListServices();
                service = FindService("Spooler", rsi);
                Assert.AreEqual(RemoteServiceStartMode.Disabled, service.StartMode);

                sm.SetServiceStartMode("Spooler", RemoteServiceStartMode.Automatic);
                rsi = sm.ListServices();
                service = FindService("Spooler", rsi);
                Assert.AreEqual(RemoteServiceStartMode.Automatic, service.StartMode);
            }
            finally
            {
                sm.Disconnect();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ServerManager_ListProcesses()
        {
            ServiceManager sm;
            RemoteProcess[] processes;
            Process shell = null;
            bool found;
            int id;

            sm = new ServiceManager(router);
            sm.Connect(ServiceManager.AbstractEP);
            try
            {
                shell = Process.Start("cmd.exe");
                processes = sm.ListProcesses();

                found = false;
                for (int i = 0; i < processes.Length; i++)
                    if (processes[i].ProcessID == shell.Id)
                    {
                        found = true;
                        break;
                    }

                Assert.IsTrue(found);

                id = shell.Id;
                shell.Kill();
                shell = null;
                Thread.Sleep(1000);

                processes = sm.ListProcesses();

                found = false;
                for (int i = 0; i < processes.Length; i++)
                    if (processes[i].ProcessID == id)
                    {
                        found = true;
                        break;
                    }

                Assert.IsFalse(found);
            }
            finally
            {
                if (shell != null)
                    shell.Kill();

                sm.Disconnect();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ServerManager_KillProcess()
        {
            ServiceManager sm;
            RemoteProcess[] processes;
            Process shell = null;
            bool found;
            int id;

            sm = new ServiceManager(router);
            sm.Connect(ServiceManager.AbstractEP);
            try
            {
                shell = Process.Start("cmd.exe");
                processes = sm.ListProcesses();

                found = false;
                for (int i = 0; i < processes.Length; i++)
                    if (processes[i].ProcessID == shell.Id)
                    {
                        found = true;
                        break;
                    }

                Assert.IsTrue(found);

                id = shell.Id;
                sm.KillProcess(id);
                shell = null;
                Thread.Sleep(1000);

                processes = sm.ListProcesses();

                found = false;
                for (int i = 0; i < processes.Length; i++)
                    if (processes[i].ProcessID == id)
                    {
                        found = true;
                        break;
                    }

                Assert.IsFalse(found);
            }
            finally
            {
                if (shell != null)
                    shell.Kill();

                sm.Disconnect();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ServerManager_SetTime()
        {
            ServiceManager sm;
            DateTime now;
            DateTime time;

            // Assert.Ignore();

            sm = new ServiceManager(router);
            sm.Connect(ServiceManager.AbstractEP);
            try
            {
                now = DateTime.UtcNow;
                time = new DateTime(2004, 1, 1);

                sm.SetTime(time);

                Assert.AreEqual(time.Year, DateTime.UtcNow.Year);
                Assert.AreEqual(time.Month, DateTime.UtcNow.Month);
                Assert.AreEqual(time.Day, DateTime.UtcNow.Day);

                sm.SetTime(now);

                Assert.AreEqual(now.Year, DateTime.UtcNow.Year);
                Assert.AreEqual(now.Month, DateTime.UtcNow.Month);
                Assert.AreEqual(now.Day, DateTime.UtcNow.Day);
            }
            finally
            {
                sm.Disconnect();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ServerManager_SyncTime()
        {
            ServiceManager sm;
            DateTime time;

            // Assert.Ignore();

            sm = new ServiceManager(router);
            sm.Connect(ServiceManager.AbstractEP);
            try
            {
                time = DateTime.UtcNow;
                sm.SynchronizeTime();
                Assert.AreEqual(time.Year, DateTime.UtcNow.Year);
            }
            finally
            {
                sm.Disconnect();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ServerManager_Execute()
        {
            ServiceManager sm;
            string stdout, stderr;
            int rc;

            sm = new ServiceManager(router);
            sm.Connect(ServiceManager.AbstractEP);

            try
            {
                rc = sm.Execute("sc.exe", "query", out stdout, out stderr);
                Assert.AreEqual(0, rc);
                Assert.IsTrue(stdout != string.Empty);
                Assert.IsTrue(stderr == "\r\n");
            }
            finally
            {
                sm.Disconnect();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ServerManager_WmiQuery()
        {
            ServiceManager sm;
            WmiResultSet wmiResults;
            WmiQuery[] wmiQueries;

            sm = new ServiceManager(router);
            sm.Connect(ServiceManager.AbstractEP);

            try
            {
                wmiResults = new WmiResultSet();
                wmiResults.Add(new WmiResult("query1", new ManagementObjectSearcher("select * from CIM_LogicalDisk")));
                wmiResults.Add(new WmiResult("query2", new ManagementObjectSearcher("select * from CIM_Memory")));

                wmiQueries = new WmiQuery[] {

                    new WmiQuery("query1","select * from CIM_LogicalDisk"),
                    new WmiQuery("query2","select * from CIM_Memory")
                };

                Assert.AreEqual(wmiResults, sm.WmiQuery(wmiQueries));
            }
            finally
            {
                sm.Disconnect();
            }
        }
    }
}


#endif // TODO