﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FulcrumInjector.ViewControl.Models;
using Newtonsoft.Json;
using SharpLogger;
using SharpLogger.LoggerObjects;
using SharpLogger.LoggerSupport;

namespace FulcrumInjector.AppLogic
{
    /// <summary>
    /// Builds a new template selection object which helps us find templates
    /// </summary>
    public class UserSettingsDataTemplateSelector : DataTemplateSelector
    {
        // Logger object.
        private static SubServiceLogger TemplateLogger => (SubServiceLogger)LogBroker.LoggerQueue.GetLoggers(LoggerActions.SubServiceLogger)
            .FirstOrDefault(LoggerObj => LoggerObj.LoggerName.StartsWith("SettingsDataTemplateLogger")) ?? new SubServiceLogger("SettingsDataTemplateLogger");

        // --------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Data Template location routine
        /// Pulls in the type of the item in the template and then finds a style based on the output
        /// </summary>
        /// <param name="InputItem"></param>
        /// <param name="ObjectContainer"></param>
        /// <returns></returns>
        public override DataTemplate SelectTemplate(object InputItem, DependencyObject ObjectContainer)
        {
            // Check if we can use this selector object or not.
            if (ObjectContainer is FrameworkElement InputElement && InputItem is SettingsEntryModel SettingModelObject)
            {
                // Now find the type of control to use
                switch (SettingModelObject.TypeOfControl)
                {
                    // Found control type
                    case ControlTypes.CHECKBOX_CONTROL: return InputElement.FindResource("CheckboxSettingEntryDataTemplate") as DataTemplate;
                    case ControlTypes.TEXTBOX_CONTROL: return InputElement.FindResource("TextBoxSettingEntryDataTemplate") as DataTemplate;
                    
                    // If failed
                    case ControlTypes.NOT_DEFINED:
                        TemplateLogger.WriteLog($"FAILED TO FIND NEW CONTROL TYPE FOR VALUE {SettingModelObject.TypeOfControl}!", LogType.ErrorLog);
                        return null;
                }
            }

            // Failed to find control template output
            TemplateLogger.WriteLog("ERROR! INVALID CONTROL TYPE WAS PROCESSED! NOT RETURNING A DATATEMPLATE FOR IT", LogType.ErrorLog);
            TemplateLogger.WriteLog($"CONTROL PASSED CONTENT: {JsonConvert.SerializeObject(InputItem, Formatting.None)}", LogType.TraceLog);
            return null;
        }
    }
}
