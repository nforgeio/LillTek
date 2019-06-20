//-----------------------------------------------------------------------------
// FILE:        WizardStepList.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a list of wizard steps.

using System;
using System.Collections;

using LillTek.Common;
using LillTek.Install;
using LillTek.Data;

namespace LillTek.Data.Install
{
    /// <summary>
    /// Implements a list of wizard steps.
    /// </summary>
    internal sealed class WizardStepList
    {

        private InstallWizard   wizard;
        private ArrayList       list;
        private IWizardStep     current;
        private bool            skipToFinish;
        private bool            inStepTo;

        /// <summary>
        /// Constructs an empty list.
        /// </summary>
        /// <param name="wizard">The parent wizard form.</param>
        public WizardStepList(InstallWizard wizard)
        {
            this.wizard       = wizard;
            this.list         = new ArrayList();
            this.current      = null;
            this.skipToFinish = false;
            this.inStepTo     = false;
        }

        /// <summary>
        /// Adds a step to the list.
        /// </summary>
        /// <param name="step">The step.</param>
        public void Add(IWizardStep step)
        {
            list.Add(step);
        }

        /// <summary>
        /// Returns the first step in the list.
        /// </summary>
        public IWizardStep FirstStep
        {
            get { return (IWizardStep)list[0]; }
        }

        /// <summary>
        /// Returns the last step in the list.
        /// </summary>
        public IWizardStep LastStep
        {
            get { return (IWizardStep)list[list.Count - 1]; }
        }

        /// <summary>
        /// Returns the current step (or <c>null</c>).
        /// </summary>
        public IWizardStep Current
        {
            get { return current; }
        }

        /// <summary>
        /// Returns the previous step in the list (or <c>null</c>).
        /// </summary>
        /// <param name="step">The current step.</param>
        public IWizardStep GetPrev(IWizardStep step)
        {
            var pos = list.IndexOf(step);
            
            if (pos <= 0)
                return null;
            else
                return (IWizardStep)list[pos - 1];
        }

        /// <summary>
        /// Returns the next step in the list (or <c>null</c>).
        /// </summary>
        /// <param name="step">The current step.</param>
        public IWizardStep GetNext(IWizardStep step)
        {
            var pos = list.IndexOf(step);

            if (pos == -1 || pos >= list.Count - 1)
                return null;
            else
                return (IWizardStep)list[pos + 1];
        }

        /// <summary>
        /// Changes the current step.
        /// </summary>
        /// <param name="step">The new current step.</param>
        /// <param name="forward"><c>true</c> if we're stepping forward in the wizard.</param>
        public void StepTo(IWizardStep step, bool forward)
        {
            try
            {
                inStepTo = true;

                if (current != null && !current.OnStepOut(this, forward))
                    return;

                current = step;

                if (step != null)
                    step.OnStepIn(this);

                wizard.OnStep(this, step);
            }
            finally
            {
                inStepTo = false;

                if (skipToFinish)
                {

                    skipToFinish = false;
                    StepTo(LastStep, false);
                }
            }
        }

        /// <summary>
        /// Moves backwards a step.
        /// </summary>
        public void StepBack()
        {
            var step = GetPrev(current);

            if (step != null)
                StepTo(step, false);
        }

        /// <summary>
        /// Moves forward a step.
        /// </summary>
        public void StepNext()
        {
            var  step = GetNext(current);

            if (step != null)
                StepTo(step, true);
        }

        /// <summary>
        /// Skips all remaining steps and goes to the last one.
        /// </summary>
        public void SkipToFinish()
        {
            if (inStepTo)
                skipToFinish = true;
            else
                StepTo(LastStep, false);
        }
    }
}