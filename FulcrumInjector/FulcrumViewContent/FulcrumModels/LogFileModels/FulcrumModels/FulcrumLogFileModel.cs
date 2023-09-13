﻿using System.IO;
using FulcrumInjector.FulcrumViewSupport.FulcrumDataConverters;

namespace FulcrumInjector.FulcrumViewContent.FulcrumModels.LogFileModels.FulcrumModels
{
    /// <summary>
    /// Class which holds information about a log file path from the local machine
    /// </summary>
    internal class FulcrumLogFileModel : LogFileModel
    {
        #region Custom Events
        #endregion //Custom Events

        #region Fields
        #endregion //Fields

        #region Properties

        // Public facing properties holding information about the file and the contents of it
        public bool LogFileExists => File.Exists(this.LogFilePath);
        public string LogFileSize => this.LogFileExists ? new FileInfo(this.LogFilePath).Length.ToFileSize() : "N/A";

        #endregion //Properties

        #region Structs and Classes
        #endregion //Structs and Classes

        // --------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Spawns a new log file object instance and configures fields/properties of it
        /// </summary>
        /// <param name="InputLogPath">The path to the input log file object</param>
        public FulcrumLogFileModel(string InputLogPath) : base(InputLogPath) { }
    }
}