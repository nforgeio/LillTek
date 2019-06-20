//-----------------------------------------------------------------------------
// FILE:        EnhancedForm.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Extends the basic Windows Form implementation by hooking
//              low-level Windows messages to provide advanced functionality.

using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;

using LillTek.Common;
using LillTek.Windows;

namespace LillTek.Client
{
    /// <summary>
    /// Holds information about the menu item selected by the user.
    /// </summary>
    public sealed class SysMenuClickEventArgs : EventArgs
    {
        /// <summary>
        /// The application defined string that identifies the menu clicked.
        /// </summary>
        public readonly string Key;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="key">The application defined string that identifies the menu clicked.</param>
        internal SysMenuClickEventArgs(string key)
        {
            this.Key = key;
        }
    }

    /// <summary>
    /// Delegate definition used by the <see cref="EnhancedForm.SysMenuClick" /> event.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="args">The event arguments.</param>
    public delegate void SysMenuClickEventHandler(object sender, SysMenuClickEventArgs args);

    /// <summary>
    /// Extends the basic Windows Form implementation by hooking low-level Windows messages 
    /// to provide advanced functionality.
    /// </summary>
    /// <remarks>
    /// The current implementation supports adding menus to the form's system
    /// menu and then receiving click events using the <see cref="AddSysMenu" />
    /// method and the <see cref="SysMenuClick" /> event.
    /// </remarks>
    public class EnhancedForm : Form
    {
        private Dictionary<int, string> sysMenuMap = new Dictionary<int, string>();
        private int                     nextSysMenuID = WinMsg.WM_USER;

        /// <summary>
        /// Raised when a menu added to the system menu via <see cref="AddSysMenu" /> is selected
        /// by the user.
        /// </summary>
        public event SysMenuClickEventHandler SysMenuClick;

        /// <summary>
        /// Constructor.
        /// </summary>
        public EnhancedForm()
        {
        }

        /// <summary>
        /// Adds a menu item to the form's system menu.
        /// </summary>
        /// <param name="title">The title of the menu to be displayed (or <c>null</c>).</param>
        /// <param name="key">An application defined key to be used to identify the item when selected.</param>
        /// <remarks>
        /// <para>
        /// Pass <paramref name="title" /> as <c>null</c> to append a menu separator instead of a menu item.
        /// </para>
        /// <note>
        /// This works only for top-level forms.  The application can
        /// handle menu clicks by listening on the <see cref="SysMenuClick" /> event.
        /// </note>
        /// </remarks>
        public void AddSysMenu(string title, string key)
        {
            var hSysMenu = WinApi.GetSystemMenu(this.Handle, false);

            if (title == null)
            {
                WinApi.AppendMenu(hSysMenu, MenuFlags.MF_SEPARATOR, 0, string.Empty);
                return;
            }

            WinApi.AppendMenu(hSysMenu, MenuFlags.MF_STRING, nextSysMenuID, title);
            sysMenuMap.Add(nextSysMenuID, key);
            nextSysMenuID++;
        }

        /// <summary>
        /// Overrides the default window proc to handle low-level Windows messages.
        /// </summary>
        /// <param name="msg">The windows message.</param>
        /// <remarks>
        /// Applications forms that need to override this must call this base
        /// method for all messages that are not explicitly handled by the application.
        /// </remarks>
        protected override void WndProc(ref Message msg)
        {
            if (msg.Msg == WinMsg.WM_SYSCOMMAND)
            {

                if (sysMenuMap.Count == 0 || SysMenuClick == null)
                    return;

                int     menuID = msg.WParam.ToInt32();
                string  key;

                if (sysMenuMap.TryGetValue(menuID, out key))
                    SysMenuClick(this, new SysMenuClickEventArgs(key));
            }

            base.WndProc(ref msg);
        }
    }
}
