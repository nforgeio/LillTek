//-----------------------------------------------------------------------------
// FILE:        AppStoreClient.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Provides the client side access to the Application Store service.

using System;
using System.Collections.Generic;

using LillTek.Common;
using LillTek.Datacenter.Msgs.AppStore;
using LillTek.Messaging;

namespace LillTek.Datacenter
{
    /// <summary>
    /// Implements client side access to the <b>Application Store</b> service.  This
    /// class is designed to be used iternally by the LillTek Platform.
    /// </summary>
    /// <threadsafety instance="true" />
    public sealed class AppStoreClient : IDisposable
    {
        /// <summary>
        /// The default application store cluster base endpoint.
        /// </summary>
        public const string AbstractBaseEP = "abstract://LillTek/DataCenter/AppStore";

        /// <summary>
        /// The default application store cluster endpoint.
        /// </summary>
        public const string AbstractClusterEP = AbstractBaseEP + "/*";

        private object                  syncLock;           // Thread synchronization instance
        private MsgRouter               router;             // The associated message router
        private AppPackageFolder        packageFolder;      // The local package cache folder (or null)
        private AppStoreClientSettings  settings;           // The client settings
        private GatedTimer              bkTimer;            // Background task timer
        private DateTime                nextPurgeTime;      // Next scheduled package folder purge time (SYS)

        /// <summary>
        /// Constructor.
        /// </summary>
        public AppStoreClient()
        {
            this.syncLock      = null;
            this.router        = null;
            this.packageFolder = null;
            this.bkTimer       = null;
        }

        /// <summary>
        /// Opens the <see cref="AppStoreClient" /> instance so that it is ready to
        /// process requests.
        /// </summary>
        /// <param name="router">The <see cref="MsgRouter" /> to be associated with the client.</param>
        /// <param name="settings">The <see cref="AppStoreClientSettings" /> to be used.</param>
        /// <exception cref="InvalidOperationException">Thrown if the instance is already open.</exception>
        public void Open(MsgRouter router, AppStoreClientSettings settings)
        {
            if (this.syncLock != null)
                throw new InvalidOperationException("AppStoreClient is already open.");

            // Make sure that the LillTek.Datacenter message types have been
            // registered with the LillTek.Messaging subsystem.

            LillTek.Datacenter.Global.RegisterMsgTypes();

            // Initialize

            using (TimedLock.Lock(router.SyncRoot))
            {
                this.syncLock      = router.SyncRoot;
                this.router        = router;
                this.settings      = settings;
                this.bkTimer       = new GatedTimer(new System.Threading.TimerCallback(OnBkTimer), null, settings.BkTaskInterval);
                this.nextPurgeTime = SysTime.Now;

                if (settings.LocalCache)
                    this.packageFolder = new AppPackageFolder(syncLock, settings.PackageFolder);
                else
                    this.packageFolder = null;

                router.Dispatcher.AddTarget(this);
            }
        }

        /// <summary>
        /// Releases all resources associated with the instance.
        /// </summary>
        public void Close()
        {
            using (TimedLock.Lock(syncLock))
            {
                if (syncLock == null)
                    return;

                router.Dispatcher.RemoveTarget(this);

                if (packageFolder != null)
                {
                    packageFolder.Dispose();
                    packageFolder = null;
                }

                if (bkTimer != null)
                {
                    bkTimer.Dispose();
                    bkTimer = null;
                }

                router   = null;
                syncLock = null;
            }
        }

        /// <summary>
        /// Releases all resources associated with the instance.
        /// </summary>
        public void Dispose()
        {
            Close();
        }

        /// <summary>
        /// Throws an <see cref="ObjectDisposedException" /> if the instance is not open.
        /// </summary>
        private void VerifyOpen()
        {
            if (syncLock == null)
                throw new ObjectDisposedException(typeof(AppStoreClient).Name);
        }

        /// <summary>
        /// Throws an <see cref="InvalidOperationException" /> if local caching is disabled.
        /// </summary>
        private void VerifyLocal()
        {
            if (packageFolder == null)
                throw new InvalidOperationException("Local application package is disabled.");
        }

        /// <summary>
        /// Returns the <see cref="PackageFolder" /> instance used to manage the packages.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if the cache is not open.</exception>
        /// <exception cref="InvalidOperationException">Thrown if local package caching is disabled.</exception>
        public AppPackageFolder PackageFolder
        {
            get
            {
                VerifyOpen();
                VerifyLocal();
                return packageFolder;
            }
        }

        /// <summary>
        /// Returns the requested <see cref="AppPackage" /> from the local cache opened for read access.
        /// </summary>
        /// <param name="storeEP">
        /// The application store endpoint or <c>null</c> to query any 
        /// application store instance in the cluster.
        /// </param>
        /// <param name="appRef">The <see cref="AppRef" /> identifing the desired application package.</param>
        /// <returns>The open <see cref="AppPackage" />.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the cache is not open.</exception>
        /// <remarks>
        /// <para>
        /// This method first looks in the local <see cref="AppPackageFolder" /> cache for the
        /// requested package.  If the package is not present locally, then the package will
        /// be requested from an application store service instance.  If the application
        /// store returns a package, it will be added to the local package folder and
        /// an open <see cref="AppPackage" /> will be returned.
        /// </para>
        /// <note>
        /// Some care should be taken to ensure that the <see cref="AppPackage" />
        /// instances returned are promptly disposed when they are no longer needed.
        /// </note>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">Thrown if the cache is not open.</exception>
        /// <exception cref="InvalidOperationException">Thrown if local package caching is disabled.</exception>
        /// <exception cref="AppPackageException">Thrown if the package cannot be found.</exception>
        public AppPackage GetPackage(MsgEP storeEP, AppRef appRef)
        {
            return GetPackage(storeEP, appRef, true);
        }

        /// <summary>
        /// Returns the requested <see cref="AppPackage" /> from the local cache opened for read access, 
        /// optionally querying an application store service if the package is not available locally.
        /// </summary>
        /// <param name="storeEP">
        /// The application store endpoint or <c>null</c> to query any 
        /// application store instance in the cluster.
        /// </param>
        /// <param name="appRef">The <see cref="AppRef" /> identifing the desired application package.</param>
        /// <param name="queryStore">
        /// Pass <c>true</c> if an application store is to be queried if the package
        /// is not cached locally.
        /// </param>
        /// <returns></returns>
        /// <exception cref="ObjectDisposedException">Thrown if the cache is not open.</exception>
        /// <exception cref="InvalidOperationException">Thrown if local package caching is disabled.</exception>
        /// <exception cref="AppPackageException">Thrown if the package cannot be found.</exception>
        public AppPackage GetPackage(MsgEP storeEP, AppRef appRef, bool queryStore)
        {
            AppPackageInfo  info;
            string          transitPath;

            if (storeEP == null)
                storeEP = settings.ClusterEP;

            using (TimedLock.Lock(syncLock))
            {
                VerifyOpen();
                VerifyLocal();

                info = packageFolder.GetPackageInfo(appRef);
                if (info != null)
                    return AppPackage.Open(info.FullPath);
            }

            // $hack(jeff.lill): 
            //
            // The code below isn't strictly threadsafe
            // but should exhibit problems only when
            // closing the client under load so I'm
            // not going to worry about this right now.

            // Download the package from an application store.

            transitPath = packageFolder.BeginTransit(appRef);
            try
            {
                DownloadPackage(null, appRef, transitPath);
                packageFolder.EndTransit(transitPath, true);
            }
            catch
            {
                packageFolder.EndTransit(transitPath, false);
                throw;
            }

            info = packageFolder.GetPackageInfo(appRef);
            if (info == null)
                throw new AppPackageException("Package [{0}] not found.", appRef);

            return AppPackage.Open(info.FullPath);
        }

        /// <summary>
        /// Determines if a specific <see cref="AppPackage" /> is present in the local cache.
        /// </summary>
        /// <param name="appRef">The <see cref="AppRef" /> identifing the desired application package.</param>
        /// <returns><c>true</c> if the application package is present.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the cache is not open.</exception>
        /// <exception cref="InvalidOperationException">Thrown if local package caching is disabled.</exception>
        public bool IsCached(AppRef appRef)
        {
            using (TimedLock.Lock(syncLock))
            {
                VerifyOpen();
                VerifyLocal();

                if (packageFolder == null)
                    return false;

                return packageFolder.GetPackageInfo(appRef) != null;
            }
        }

        /// <summary>
        /// Handles background activities.
        /// </summary>
        /// <param name="state">Not used.</param>
        private void OnBkTimer(object state)
        {
            if (packageFolder == null || SysTime.Now < nextPurgeTime)
                return;

            using (TimedLock.Lock(syncLock))
            {
                // Scan for packages that have exceeded their lifespan and
                // remove them from the cache.

                var ttd = DateTime.UtcNow - settings.PackageTTL;

                packageFolder.Scan();

                foreach (AppPackageInfo info in packageFolder.GetPackages())
                    if (info.WriteTimeUtc <= ttd)
                        packageFolder.Remove(info.AppRef);

                nextPurgeTime = SysTime.Now + settings.PurgeInterval;
            }
        }

        //---------------------------------------------------------------------
        // Remote Store/Cache Management methods

        /// <summary>
        /// Queries for the <see cref="MsgEP" /> for the primary application store.
        /// </summary>
        /// <param name="storeEP">
        /// The application store endpoint or <c>null</c> to query any 
        /// application store instance in the cluster.
        /// </param>
        /// <returns>
        /// The primary store's <see cref="MsgEP" /> or <c>null</c> if the
        /// primary application store cannot be found.
        /// </returns>
        public MsgEP GetPrimaryStoreEP(MsgEP storeEP)
        {
            if (storeEP == null)
                storeEP = settings.ClusterEP;

            return ((AppStoreAck)router.Query(storeEP, new AppStoreQuery(AppStoreQuery.GetPrimaryCmd))).StoreEP;
        }

        /// <summary>
        /// Uploads an application package to a remote application store instance.
        /// </summary>
        /// <param name="storeEP">
        /// The application store endpoint or <c>null</c> to query any 
        /// application store instance in the cluster.
        /// </param>
        /// <param name="appRef">The <see cref="AppRef" /> for the file.</param>
        /// <param name="path">The path to the application package file.</param>
        public void UploadPackage(MsgEP storeEP, AppRef appRef, string path)
        {
            StreamTransferSession session;

            if (storeEP == null)
                storeEP = settings.ClusterEP;

            session = StreamTransferSession.ClientUpload(router, storeEP, path);
            session.Args = "appref=" + appRef.ToString();

            session.Transfer();
        }

        /// <summary>
        /// Downloads an application package from a remote application store instance.
        /// </summary>
        /// <param name="storeEP">
        /// The application store endpoint or <c>null</c> to query any 
        /// application store instance in the cluster.
        /// </param>
        /// <param name="appRef">The <see cref="AppRef" /> for the file.</param>
        /// <param name="path">The path to the output application package file.</param>
        public void DownloadPackage(MsgEP storeEP, AppRef appRef, string path)
        {
            StreamTransferSession   session;
            AppStoreAck             ack;

            if (storeEP == null)
                storeEP = settings.ClusterEP;

            ack = (AppStoreAck)router.Query(storeEP, new AppStoreQuery(AppStoreQuery.DownloadCmd, appRef));
            storeEP = ack.StoreEP;

            session = StreamTransferSession.ClientDownload(router, storeEP, path);
            session.Args = "appref=" + appRef.ToString();

            session.Transfer();
        }

        /// <summary>
        /// Returns information about the application packages currently
        /// hosted by an application store.
        /// </summary>
        /// <param name="storeEP">
        /// The application store endpoint or <c>null</c> to query any 
        /// application store instance in the cluster.
        /// </param>
        /// <returns>
        /// An array of <see cref="AppPackageInfo" /> instances describing the
        /// available packages.
        /// </returns>
        public AppPackageInfo[] ListRemotePackages(MsgEP storeEP)
        {
            if (storeEP == null)
                storeEP = settings.ClusterEP;

            return ((AppStoreAck)router.Query(storeEP, new AppStoreQuery(AppStoreQuery.ListCmd))).Packages;
        }

        /// <summary>
        /// Removes a specific application package on an application store.
        /// </summary>
        /// <param name="storeEP">
        /// The application store endpoint or <c>null</c> to query any 
        /// application store instance in the cluster.
        /// </param>
        /// <param name="appRef">The <see cref="AppRef" /> specifying the package to be removed.</param>
        public void RemoveRemotePackage(MsgEP storeEP, AppRef appRef)
        {
            if (storeEP == null)
                storeEP = settings.ClusterEP;

            router.Query(storeEP, new AppStoreQuery(AppStoreQuery.RemoveCmd, appRef));
        }

        /// <summary>
        /// Commands a remote application store to synchronize the contents of its
        /// local cache on a remote application store with the  primary application store.
        /// </summary>
        /// <param name="storeEP">
        /// The application store endpoint or <c>null</c> to query any 
        /// application store instance in the cluster.
        /// </param>
        /// <param name="download">Indicates whether packages not present locally should be downloaded.</param>
        /// <remarks>
        /// <para>
        /// Pass <paramref download="download" /> as <c>true</c> to perform full synchronization including both 
        /// downloading application packages that exist on the primary application store but not locally as 
        /// well as deleting packages that exist locally but not on the primary.
        /// </para>
        /// <para>
        /// Pass <paramref download="download" /> as <c>false</c> if the store should limit itself
        /// to removing local packages that don't exist on the primary.
        /// </para>
        /// <note>
        /// The method does not wait for the message store to complete the synchronization
        /// process before returning.
        /// </note>
        /// </remarks>
        public void SynchRemote(MsgEP storeEP, bool download)
        {
            if (storeEP == null)
                storeEP = settings.ClusterEP;

            router.Query(storeEP, new AppStoreQuery(AppStoreQuery.SyncCmd));
        }

        /// <summary>
        /// Broadcasts a message to all application store instances commanding them to
        /// synchronize their local caches with the primary application store.
        /// </summary>
        /// <param name="download">Indicates whether packages not present locally should be downloaded.</param>
        /// <remarks>
        /// <para>
        /// Pass <paramref download="download" /> as <c>true</c> to perform full synchronization including both 
        /// downloading application packages that exist on the primary application store but not locally as 
        /// well as deleting packages that exist locally but not on the primary.
        /// </para>
        /// <para>
        /// Pass <paramref download="download" /> as <c>false</c> if the store should limit itself
        /// to removing local packages that don't exist on the primary.
        /// </para>
        /// </remarks>
        public void BroadcastSync(bool download)
        {
            router.BroadcastTo(settings.ClusterEP, new AppStoreMsg(AppStoreMsg.SyncCmd));
        }
    }
}
