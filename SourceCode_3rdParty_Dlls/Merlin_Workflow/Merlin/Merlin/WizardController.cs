﻿/*
 * The contributors of Merlin license this file to You under the Apache 
 * License, Version 2.0 (the "License"); you may not use this file except 
 * in compliance with the License.  You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

 
 
﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;



namespace Merlin
{
    /// <summary>
    /// This is the executor of the wizard. It performs step sequence
    /// traversal and navigation.
    /// </summary>
    public class WizardController
    {
        public List<IStep> steps;                   // DC: made all these public
        public WizardForm wizardForm;
        public int currentStepIndex;
        public Image logoImage;
        public WizardResult result = WizardResult.Cancel;
        public Action<Exception> onError;              // DC: so that we can catch exceptions from O2

        #region Constructors



        /// <summary>
        /// Creates a new wizard controller.
        /// </summary>
        /// <param name="steps">
        /// The list of steps of the wizard.
        /// </param>
        public WizardController(List<IStep> steps)
        {
            this.steps = steps;
        }

        #endregion

        #region Properties

        public Image LogoImage
        {
            get { return logoImage; }
            set { 
                logoImage = value;
                if (wizardForm != null)
                {
                    wizardForm.LogoImage = value;
                }
            }
        }
        #endregion

        #region Control methods


        /// <summary>
        /// Starts the wizard.
        /// </summary>
        /// <param name="windowName">Text that appears in the title bar of the wizard window.</param>
        /// <returns>Whether the wizard was aborted, failed, or ran to completion.</returns>
        public WizardResult StartWizard(string windowName)
        {
            return StartWizard(windowName, false, null);
        }


        /// <summary>
        /// Starts the wizard.
        /// </summary>
        /// <param name="windowName">Text that appears in the title bar of the wizard window.</param>
        /// <param name="icon">Sets the Icon of the wizard window</param>
        /// <param name="showInTaskBar">Sets the ShowInTaskbar property of the wizard window</param>
        /// <returns>Whether the wizard was aborted, failed, or ran to completion.</returns>
        public WizardResult StartWizard(string windowName, bool showInTaskBar, Icon icon)
        {
            return StartWizard(windowName, showInTaskBar, icon, -1, -1);
        }

        public WizardResult StartWizard(string windowName, bool showInTaskBar, Icon icon, int width, int height)  //DC added width and height    
        {
            if (steps == null || steps.Count < 1)            
                throw new EmptyStepSequenceException();
            
            if (steps[steps.Count - 1] is ConditionalStep)            
                throw new ConditionalStepAtEndException();
            
            wizardForm = new WizardForm(windowName);
                
            if (width > -1)                                         //DC
                wizardForm.Width = width;    //DC
            if (height > -1)                                        //DC    
                wizardForm.Height = height;  //DC
            wizardForm.LogoImage = this.LogoImage;
            wizardForm.btnNext.Click += new EventHandler(btnNext_Click);
            wizardForm.btnBack.Click += new EventHandler(btnPrevious_Click);
            wizardForm.ShowInTaskbar = showInTaskBar;
            if (icon != null) wizardForm.Icon = icon;
            wizardForm.Enabled = true;
            wizardForm.BringToFront();
            currentStepIndex = 0;
            runComponent(steps[currentStepIndex]);
            wizardForm.ShowDialog();
            return result;
        }

        /// <summary>
        /// Calls the OnNext() method of the current step and
        /// advances the wizard to the next step. If the current step
        /// is the last in the sequence, ends the wizard.
        /// </summary>
        /// <returns></returns>
        private bool Advance()
        {
            IStep departingComponent = steps[currentStepIndex] is ConditionalStep
                ? (steps[currentStepIndex] as ConditionalStep).UnderlyingStep
                : steps[currentStepIndex];

            bool successful = departingComponent.OnNext();           
            if (successful)
            {
                if (steps.Count > currentStepIndex + 1)
                {
                    ++currentStepIndex;
                    IStep displayedComponent = steps[currentStepIndex];
                    while (displayedComponent is ConditionalStep)
                    {
                        ConditionalStep cc = steps[currentStepIndex] as ConditionalStep;
                        if (cc.Condition())
                        {
                            displayedComponent = cc.UnderlyingStep;
                            cc.Displayed = true;
                        }
                        else
                        {
                            cc.Displayed = false;
                            if (currentStepIndex == steps.Count - 1)
                            {
                                throw new Exception("Conditional step occurred last in step sequence"
                                    + " and was displayed.");
                            }
                            ++currentStepIndex;
                            displayedComponent = steps[currentStepIndex];
                        }
                    }
                    runComponent(displayedComponent);
                }
                else
                {
                    result = WizardResult.Finish;
                    endWizard();
                }
            }
            else
            {
                updateButtonState();
            }
            return successful;
        }
       

        private void endWizard()
        {
            wizardForm.Hide();
        }



        private void runComponent(IStep component)
        {
            try
            {
                if (component.UI == null && component.UIType != null)   // DC case when we need to create an instance of UI
                {
                    component.UI = (Control)Activator.CreateInstance(component.UIType);                    
                }
                if (component.UI == null)
                    throw new Exception("in runComponent, was not able to create instance of type : " + component.UIType.FullName);

                component.Controller = this;
                if (component.OnComponentLoad != null)
                    component.OnComponentLoad(component);
                if (currentStepIndex > steps.Count)
                {
                    throw new Exception("Attempting to run past the end of the wizard");
                }
                component.StepReached();
                Control ui = component.UI;

                if (ui != null)
                {
                    wizardForm.ShowUserControl(ui);
                    updateButtonState();
                    component.StepStateChanged
                        += new EventHandler(onComponentStateChanged);
                    wizardForm.HeaderTitle = component.Title;
                    if (!string.IsNullOrEmpty(component.Subtitle))
                    {
                        wizardForm.ShowSubtitle(component.Subtitle);
                    }
                    else
                    {
                        wizardForm.HideSubtitle();
                    }

                    // DC: next code was added for O2
                    try
                    {
                        if (component.UI.Controls.Count > 0)
                        {
                            if (component.UIType == null)                           // the FirstControl value depends on when the control was created
                                component.FirstControl = component.UI.Controls[0];
                            else
                                component.FirstControl = component.UI;

                            component.Controls = new List<Control>();
                            foreach (Control control in component.UI.Controls)
                                component.Controls.Add(control);
                        }
                        if (component.OnComponentAction != null)
                            component.OnComponentAction(component);

                    }
                    catch (Exception ex)
                    {
                        if (onError != null)
                            onError(ex);
                    }

                }
            }
            catch (Exception ex)
            {
                if (onError != null)
                    onError(ex);
            }
        }


        void onComponentStateChanged(object sender, EventArgs e)
        {
            updateButtonState();
        }

        /// <summary>
        /// Enables/disables wizard buttons in accordance with 
        /// the component's state.
        /// </summary>
        public void updateButtonState()         //   DC (was private)
        {
            wizardForm.btnBack.Enabled = currentStepIndex > 0
                && steps[currentStepIndex].AllowPrevious();
            wizardForm.btnNext.Enabled = 
                steps[currentStepIndex].AllowNext();
            wizardForm.btnCancel.Enabled = steps[currentStepIndex].AllowCancel();
            if (currentStepIndex == steps.Count - 1)
            {
                wizardForm.btnNext.Text = "&Finish";
            }
            else
            {
                wizardForm.btnNext.Text = "&Next >";
            }
        }

        void btnNext_Click(object sender, EventArgs e)
        {
            Advance();
        }

        void btnPrevious_Click(object sender, EventArgs e)
        {
            GoToPrevious();     // DC
        }

        public void GoToPrevious()          // Dc
        {
            IStep departingComponent = steps[currentStepIndex] is ConditionalStep
                ? (steps[currentStepIndex] as ConditionalStep).UnderlyingStep
                : steps[currentStepIndex];

            bool successful = departingComponent.OnPrevious();
            if (successful)
            {
                if (currentStepIndex > 0)
                {
                    --currentStepIndex;
                    IStep displayedComponent = steps[currentStepIndex];
                    while (displayedComponent is ConditionalStep
                        && !(displayedComponent as ConditionalStep).Displayed)
                    {
                        if (currentStepIndex == 0)
                        {
                            throw new Exception("Attempting to reverse past an undisplayed "
                                + "conditional component that is first in the sequence.");
                        }
                        --currentStepIndex;
                        displayedComponent = steps[currentStepIndex];
                    }
                    if (displayedComponent is ConditionalStep)
                    {
                        displayedComponent = (displayedComponent as ConditionalStep).UnderlyingStep;
                    }
                    runComponent(displayedComponent);
                }
                else
                {
                    wizardForm.Hide();
                }
            }
            else
            {
                updateButtonState();
            }
        }


        /// <summary>
        /// Adds a sequence of steps after the current step and before
        /// any steps that already follow the current step.
        /// </summary>
        /// <param name="steps"></param>
        public void AddAfterCurrent(IEnumerable<IStep> steps)
        {
            this.steps.InsertRange(currentStepIndex + 1, steps);
        }

        /// <summary>
        /// Adds a single step after the current step and before
        /// any steps that already follow the current step.
        /// </summary>
        /// <param name="step"></param>
        public void AddAfterCurrent(IStep step)
        {
            this.steps.Insert(currentStepIndex + 1, step);
        }

        /// <summary>
        /// Deletes all steps after the current step.
        /// </summary>
        public void DeleteAllAfterCurrent()
        {
            steps.RemoveRange(currentStepIndex + 1,
                steps.Count - (currentStepIndex + 1));
        }

        /// <summary>
        /// Deletes the step immediately following the current step.
        /// Throws an exception if the current step is the last in the
        /// step sequence.
        /// </summary>
        public void DeleteNext()
        {
            if (currentStepIndex == steps.Count - 1)
            {
                throw new Exception("Attempting to delete the step after the last");
            }
            steps.Remove(steps[currentStepIndex + 1]);
        }



        #endregion


        public enum WizardResult {Finish, Error, Cancel};

        // DC
        public void allowNext(bool state)
        {
            wizardForm.btnNext.Enabled = state;
        }

        public void allowBack(bool state)
        {
            wizardForm.btnBack.Enabled = state;
        }

        public void allowCancel(bool state)
        {
            wizardForm.btnCancel.Enabled = state;
        }
    }
}
