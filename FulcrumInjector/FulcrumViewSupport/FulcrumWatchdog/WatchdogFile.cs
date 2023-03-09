﻿using FulcrumInjector.FulcrumViewSupport.FulcrumDataConverters;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using FulcrumInjector.FulcrumViewContent.FulcrumModels.WatchdogModels;
using SharpLogging;

namespace FulcrumInjector.FulcrumViewSupport.FulcrumWatchdog
{
    /// <summary>
    /// Class structure for a watched file instance inside a watched directory
    /// </summary>
    internal class WatchdogFile : IDisposable
    {
        #region Custom Events

        // Events for states of the file object instance
        public event EventHandler<FileEventArgs> FileChanged;
        public event EventHandler<FileAccessedEventArgs> FileAccessed;
        public event EventHandler<FileModifiedEventArgs> FileModified;

        /// <summary>
        /// Method to invoke when a new file changed event occurs
        /// </summary>
        /// <param name="EventArgs">Args fire along with this event</param>
        protected virtual void OnFileChanged(FileEventArgs EventArgs)
        {
            // Invoke the event handler if it's not null and fire event to update our directory
            this.FileChanged?.Invoke(this, EventArgs);

            // Update our time values here
            if (!File.Exists(this.FullFilePath)) return;
            FileInfo NewFileInfos = new FileInfo(this.FullFilePath);
            this.TimeCreated = NewFileInfos.CreationTime;
            this.TimeAccessed = NewFileInfos.LastAccessTime;
            this.TimeModified = NewFileInfos.LastWriteTime;

            // Log the file event being processed
            this._fileLogger.WriteLog($"PROCESSING A FILECHANGED (GENERIC) EVENT FOR FILE {this.FileName}", LogType.TraceLog);
        }
        /// <summary>
        /// Method to invoke when a new file changed event occurs
        /// </summary>
        /// <param name="EventArgs">Args fire along with this event</param>
        protected virtual void OnFileAccessed(FileAccessedEventArgs EventArgs)
        {
            // Invoke the event handler if it's not null
            this.OnFileChanged(EventArgs);
            this.FileAccessed?.Invoke(this, EventArgs);

            // Log file event being processed
            this._fileLogger.WriteLog($"PROCESSING A FILEACCESSED EVENT FOR FILE {this.FileName}", LogType.TraceLog);
        }
        /// <summary>
        /// Method to invoke when a new file changed event occurs
        /// </summary>
        /// <param name="EventArgs">Args fire along with this event</param>
        protected virtual void OnFileModified(FileModifiedEventArgs EventArgs)
        {
            // Invoke the event handler if it's not null
            this.OnFileChanged(EventArgs);
            this.FileModified?.Invoke(this, EventArgs);

            // Log file event being processed
            this._fileLogger.WriteLog($"PROCESSING A FILEMODIFIED EVENT FOR FILE {this.FileName}", LogType.TraceLog);
        }

        #endregion //Custom Events

        #region Fields

        // Logger object for this file instance
        private readonly SharpLogger _fileLogger;

        // Sets if we're watching this file or not 
        private int _refreshTime = 250;
        private CancellationToken _watchToken;
        private CancellationTokenSource _watchTokenSource;

        // Basic information about the file location
        public readonly string FileName;
        public readonly string FileFolder;
        public readonly string FullFilePath;
        public readonly string FileExtension;

        #endregion //Fields

        #region Properties

        // Public properties holding information about our currently watched file instance
        public bool IsMonitoring
        {
            get => this._watchTokenSource is { IsCancellationRequested: false };
            set
            {
                // If setting monitoring off, cancel the task to monitor and reset our cancellation source
                if (!value)
                {
                    // Reset the source object and cancel the task
                    this._watchTokenSource?.Cancel();
                    this._watchTokenSource = null;
                    return;
                }

                // If we're starting it up, then build a new task and run the operation to monitor
                // Setup new token objects for this task instance.
                this._watchTokenSource = new CancellationTokenSource();
                this._watchToken = this._watchTokenSource.Token;

                // Invoke our new task routine here for watching this file instance
                Task.Run(() =>
                {
                    // Start a new task which watches this file object and tracks the properties of it.
                    while (!this._watchTokenSource.IsCancellationRequested)
                    {
                        // Store a new file information object and wait for a given time period
                        FileInfo OldFileInfo = new FileInfo(this.FullFilePath);
                        Thread.Sleep(this._refreshTime);

                        // Attempt comparisons inside a try catch to avoid failures
                        try
                        {
                            // If the file was built or destroyed, let the directory object deal with it.
                            if (!this.FileExists || !OldFileInfo.Exists) return;

                            // If we're not dealing with a deleted or created object, then update values here
                            if (this.FileSize != OldFileInfo.Length)
                                this.OnFileChanged(new FileModifiedEventArgs(this));
                            else if (this.TimeAccessed != OldFileInfo.LastAccessTime)
                                this.OnFileAccessed(new FileAccessedEventArgs(this));
                            else if (this.TimeModified != OldFileInfo.LastWriteTime)
                                this.OnFileModified(new FileModifiedEventArgs(this));
                        }
                        catch (Exception CompareFilesEx)
                        {
                            // Catch the exception and log it out
                            this._fileLogger?.WriteException(CompareFilesEx);
                        }
                    }
                }, this._watchToken);
            }
        }
        public bool FileExists
        {
            get
            {
                // Check if the file exists. If it doesn't dispose this object
                bool FileExists = File.Exists(this.FullFilePath);
                if (!FileExists) this.Dispose();

                // Return if the file is real or not
                return FileExists;
            }
        }

        // Public properties containing file size and access information
        public DateTime TimeCreated { get; private set; }
        public DateTime TimeModified { get; private set; }
        public DateTime TimeAccessed { get; private set; }

        // Public properties holding file Size information as a long value and a string formatted byte value
        public long FileSize => this.FileExists ? new FileInfo(this.FullFilePath).Length : 0;
        public string FileSizeString
        {
            get
            {
                // Check if the file is not real or no bytes are found
                if (!this.FileExists) return "File Not Found!";
                return this.FileSize == 0 ? "0 B" : this.FileSize.ToFileSize();
            }
        }

        #endregion //Properties

        #region Structs and Classes
        #endregion //Structs and Classes

        // ------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Disposal method for cleaning up the resources of this object when done being used
        /// </summary>
        public void Dispose()
        {
            // Cancel the refresh task and dispose the token source
            this._watchTokenSource?.Cancel();
            this._watchTokenSource?.Dispose();

            // Log disposing and exit out
            this._fileLogger.WriteLog($"DISPOSING LOGGER FOR FILE INSTANCE {this.FileName}", LogType.TraceLog);
        }
        /// <summary>
        /// Converts this file object into a formatted string output which contains all the information about this file
        /// </summary>
        /// <returns>A String which has the file name and all properties of the file instance</returns>
        public override string ToString()
        {
            try
            {
                // Convert this file object into a text table object and return the output of it.
                string OutputFileString =
                    $"File: {this.FileName}\n" +
                    $"--> File Path:      {this.FullFilePath}\n" +
                    $"--> File Exists:    {(this.FileExists ? "YES" : "NO")}\n" +
                    $"--> File Size:      {this.FileSizeString} ({this.FileSize} bytes)\n" +
                    $"--> File Extension: {this.FileExtension}\n" +
                    $"--> Time Created:   {this.TimeCreated:G}\n" +
                    $"--> Time Modified:  {this.TimeModified:G}\n" +
                    $"--> Time Accessed:  {this.TimeAccessed:G}\n";

                // Return the built string output
                return OutputFileString;
            }
            catch
            {
                // If this fails out, then just return the name of the path of the file
                return this.FullFilePath;
            }
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Builds a new watched file object and sets up the basic properties of the file instance
        /// </summary>
        /// <param name="FileToWatch">The path for our file to track on this class instance</param>
        /// <param name="ThrowOnMissing">Throws an exception on construction if the file does not exist when true</param>
        public WatchdogFile(string FileToWatch, bool ThrowOnMissing = false)
        {
            // Throw a new exception if the file to watch does not exist
            if (ThrowOnMissing && !File.Exists(FileToWatch))
                throw new FileNotFoundException($"ERROR! FILE: {FileToWatch} COULD NOT BE WATCHED SINCE IT DOES NOT EXIST!");

            try
            {
                // Store file path location information and setup refreshing routines for properties
                this.FullFilePath = FileToWatch;
                this.FileName = Path.GetFileName(this.FullFilePath);
                this.FileExtension = Path.GetExtension(this.FullFilePath);
                this.FileFolder = Path.GetDirectoryName(this.FullFilePath);

                // Build our logger here and store it on our instance
                string LoggerName = Path.GetFileNameWithoutExtension(this.FileName);
                this._fileLogger = new SharpLogger(LoggerActions.UniversalLogger, LoggerName);
            }
            catch (Exception SetFileInfoEx)
            {
                // Catch the exception and log it out
                this._fileLogger?.WriteException(SetFileInfoEx);
                return;
            }

            try
            {
                // If the file exists, then we set up the time values and size information
                if (!this.FileExists) return;
                FileInfo WatchedFileInfo = new FileInfo(this.FullFilePath);
                this.TimeCreated = WatchedFileInfo.CreationTime;
                this.TimeModified = WatchedFileInfo.LastWriteTime;
                this.TimeAccessed = WatchedFileInfo.LastAccessTime;
            }
            catch (Exception SetFileTimeEx)
            {
                // Catch the exception and log it out
                this._fileLogger?.WriteException(SetFileTimeEx);
                return;
            }

            try
            {
                // Now try and start monitoring our file instance here
                this.IsMonitoring = true;
            }
            catch (Exception SetMonitoringStateEx)
            {
                // Catch the exception and log it out
                this._fileLogger?.WriteException(SetMonitoringStateEx);
                return;
            }
        }
    }
}