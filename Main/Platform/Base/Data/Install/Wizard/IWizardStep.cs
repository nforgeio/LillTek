//-----------------------------------------------------------------------------
// FILE:        IWizardStep.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes the behavior of a wizard step.

using System;
using System.IO;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;

using LillTek.Common;
using LillTek.Install;
using LillTek.Data;

namespace LillTek.Data.Install
{
    /// <summary>
    /// Describes the behavior of a wizard step.
    /// </summary>
    internal interface IWizardStep
    {
        /// <summary>
        /// Returns the title of the step.
        /// </summary>
        string Title { get; }

        /// <summary>
        /// Called when the step is activated.
        /// </summary>
        /// <param name="steps">The step list.</param>
        void OnStepIn(WizardStepList steps);

        /// <summary>
        /// Called when the step is deactivated.
        /// </summary>
        /// <param name="steps">The step list.</param>
        /// <param name="forward"><c>true</c> if we're stepping forward in the wizard.</param>
        /// <returns><c>true</c> if the transition can proceed.</returns>
        bool OnStepOut(WizardStepList steps, bool forward);
    }
}
