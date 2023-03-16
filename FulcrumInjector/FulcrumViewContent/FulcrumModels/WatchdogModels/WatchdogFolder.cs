﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FulcrumInjector.FulcrumViewSupport;
using FulcrumInjector.FulcrumViewSupport.FulcrumDataConverters;
using FulcrumInjector.FulcrumViewSupport.FulcrumJsonSupport;
using FulcrumInjector.FulcrumViewSupport.FulcrumJsonSupport.JsonConverters;
using Newtonsoft.Json;
using SharpLogging;

namespace FulcrumInjector.FulcrumViewContent.FulcrumModels.WatchdogModels
{
    /// <summary>
    /// Host type for a file watchdog helper
    /// Looks at a defined directory and performs actions when files are located inside of it
    /// </summary>
    [JsonConverter(typeof(WatchdogFolderJsonConverter))]
    internal class WatchdogFolder : IDisposable
    {
        #region Custom Events

        /// <summary>
        /// Event used to process the addition or removal of file objects from our list of files being checked on the directory instance
        /// </summary>
        /// <param name="SendingWatcher">Sending watcher object</param>
        /// <param name="EventArgs">Events fired with this call</param>
        /// <exception cref="IndexOutOfRangeException">Thrown when the file to remove can not be found</exception>
        private void OnWatchdogFolderChanged(object SendingWatcher, FileSystemEventArgs EventArgs)
        {
            // Get the name of our new file object and clear out files that no longer exist
            var FilesToRemove = this._watchedFiles.Where(FileObj => FileObj.FullFilePath == EventArgs.FullPath);
            foreach (var FileToRemove in FilesToRemove) this._watchedFiles.Remove(FileToRemove);

            // Switch the type of event being processed on our instance and fire actions accordingly
            if (EventArgs.ChangeType is WatcherChangeTypes.Changed or WatcherChangeTypes.Renamed)
                this._watchedFiles.Add(new WatchdogFile(EventArgs.FullPath));

            // Invoke the watchdog action here to refresh our contents
            Task.Run(() => this._watchdogAction.Invoke());
        }

        #endregion //Custom Events

        #region Fields

        // Logger object used for this watchdog instance
        private static readonly SharpLogger _watchdogLogger = new SharpLogger(LoggerActions.UniversalLogger, "WatchdogFolderLogger");

        // Backing fields holding information about our watched directory and the watcher itself
        private readonly List<FileSystemWatcher> _directoryWatchers;                      // The file system watcher to track file changes
        private readonly ObservableCollection<WatchdogFile> _watchedFiles;                // Collection of our watched file objects.
        [JsonProperty("FolderPath")] private readonly string _watchedDirectory;           // The path we're watching on the system
        [JsonProperty("FileFilters")] private readonly string[] _watchedFileFilters;      // The filters for our paths on the system

        // Backing fields holding information about the event/action to invoke for this folder
        private bool _isExecuting;                   // Tells us if we're running the action or not
        private Action _watchdogAction;              // The action to run when events are fired
        private readonly int _executionGap;          // Time to wait between each execution 
        private DateTime _lastExecutionTime;         // Time the action was last invoked

        #endregion //Fields

        #region Properties

        // Public facing properties about our watched directory
        [JsonIgnore] public Action WatchdogAction => this._watchdogAction;
        [JsonIgnore] public string WatchedDirectoryPath => this._watchedDirectory;
        [JsonIgnore] public string WatchedDirectoryName => Path.GetDirectoryName(this._watchedDirectory);

        // Public facing filter set and directory information for the watched path
        [JsonIgnore] public bool IsMonitoring
        {
            get => this._directoryWatchers.All(WatcherObj => WatcherObj.EnableRaisingEvents);
            private set
            {
                // Set all the watcher states here
                foreach (FileSystemWatcher WatcherObject in this._directoryWatchers)
                    WatcherObject.EnableRaisingEvents = value;
            }
        }
        [JsonIgnore] public WatchdogFile[] WatchedFiles => this._watchedFiles.ToArray();
        [JsonIgnore] public DirectoryInfo WatchedDirectoryInfo => new(this._watchedDirectory);
        [JsonIgnore] public IEnumerable<string> WatchedFileFilters => this._watchedFileFilters;

        #endregion //Properties

        #region Structs and Classes
        #endregion //Structs and Classes

        // ------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Disposal method for cleaning up the resources of this object when done being used
        /// </summary>
        public void Dispose()
        {
            // Dispose the process and watcher objects first
            foreach (FileSystemWatcher WatcherObject in this._directoryWatchers) WatcherObject.Dispose();
            _watchdogLogger.WriteLog($"DISPOSED ALL FILESYSTEM WATCHERS FOR WATCHDOG INSTANCE {this.WatchedDirectoryName}", LogType.TraceLog);

            // Now dispose all the objects on our collection list
            for (int FileIndex = 0; FileIndex < this._watchedFiles.Count; FileIndex++)
            {
                // Dispose the file and null it out
                this._watchedFiles[FileIndex].Dispose();
                this._watchedFiles[FileIndex] = null;
            }

            // Clear our list of files
            this._watchedFiles.Clear();
            _watchdogLogger.WriteLog($"DISPOSED ALL FILE INSTANCES FOR WATCHDOG INSTANCE {this.WatchedDirectoryName}", LogType.TraceLog);
        }
        /// <summary>
        /// Converts this file directory into a formatted text table which contains all the information about the files inside of it
        /// </summary>
        /// <returns>A text table holding all the information about each file inside of it</returns>
        public override string ToString()
        {
            // Build a string for our folder configuration
            string FolderConfiguration =
                $"Watchdog Folder Configuration\n" +
                $"\t\\__ Directory Name:          {this.WatchedDirectoryName}\n" +
                $"\t\\__ Directory Path:          {this.WatchedDirectoryPath}\n" +
                $"\t\\__ Directory Types:         {string.Join(", ", this.WatchedFileFilters)}\n" +
                $"\t\\__ Directory Size:          {this._watchedFiles.Sum(FileObj => FileObj.FileSize).ToFileSize()}\n" +
                $"\t\\__ Directory File Count:    {this._watchedFiles.Count} File{((this._watchedFiles.Count == 1) ? string.Empty : "s")}\n" +
                $"\t\\__ Directory Monitoring:    {(this.IsMonitoring ? "On" : "Off")}";

            /* TODO: Enable this routine here again if I REALLY want to. Seems like this may cause big logging hang ups
             *
             * // If this folder has no files, then exit out of this routine
             * if (this._watchedFiles.Count == 0) return FolderConfiguration;
             *
             * // Setup a list of values to show in our table and then build our output table object
             * string[] TableHeaders = new[] { "File", "Exists", "File Size", "Extension", "Time Created", "Time Accessed", "Time Modified" };
             * Tuple<string, string, string, string, string, string, string>[] FileValues = this.WatchedFiles.Select(FileObj =>
             * {
             *     // Get the values of out file object here
             *     string FileName = FileObj.FileName;
             *     string ExistsString = FileObj.FileExists ? "Yes" : "No";
             *     string FileSize = FileObj.FileSizeString;
             *     string FileExtension = FileObj.FileExtension;
             *     string TimeCreated = FileObj.TimeCreated.ToString("G");
             *     string TimeAccessed = FileObj.TimeAccessed.ToString("G");
             *     string TimeModified = FileObj.TimeModified.ToString("G");
             * 
             *     // Build our new output tuple here
             *     return new Tuple<string, string, string, string, string, string, string>(
             *         FileName, ExistsString, FileSize, FileExtension, TimeCreated, TimeAccessed, TimeModified
             *     );
             * }).ToArray();
             * 
             * // Combine the base folder information with our file information now
             * FolderConfiguration += $"\n\n{string.Join(string.Empty, Enumerable.Repeat("=", 100))}\n";
             * FolderConfiguration += FileValues.ToStringTable(
             *     TableHeaders,
             *     FileObj => FileObj.Item1,
             *     FileObj => FileObj.Item2,
             *     FileObj => FileObj.Item3,
             *     FileObj => FileObj.Item4,
             *     FileObj => FileObj.Item5,
             *     FileObj => FileObj.Item6,
             *     FileObj => FileObj.Item7
             * );
             */

            // Return the built output string for our table instance
            return FolderConfiguration;
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// CTOR for a new file watchdog host helper
        /// </summary>
        /// <param name="WatchedDirectory">The path being monitored by this watchdog helper</param>
        /// <param name="WatchdogFilters">Optional filters used to filter what files we're watching</param>
        public WatchdogFolder(string WatchedDirectory, params string[] WatchdogFilters)
        {
            // Store our configuration values first
            this._watchedFileFilters = WatchdogFilters.ToArray();
            this._directoryWatchers = new List<FileSystemWatcher>();
            this._watchedDirectory = Path.GetFullPath(WatchedDirectory);
            this._watchedFiles = new ObservableCollection<WatchdogFile>();
            this._executionGap = ValueLoaders.GetConfigValue<int>("FulcrumWatchdog.ExecutionGap");
         
            // Log that we've built and registered our logger targets here
            _watchdogLogger.WriteLog($"SPAWNED NEW WATCHDOG LOGGER FOR DIRECTORY {this._watchedDirectory}!", LogType.InfoLog);
            _watchdogLogger.WriteLog($"FILE FILTERS STORED: {string.Join(", ", this._watchedFileFilters)}");
            _watchdogLogger.WriteLog("BUILDING NEW FILESYSTEM WATCHERS FOR THE FILERS REQUESTED NOW...");
            _watchdogLogger.WriteLog("BUILT NEW COLLECTIONS FOR WATCHDOG FILES AND FILESYSTEM WATCHERS OK!", LogType.InfoLog);

            // Loop all of the supported extension types and build watcher objects for each of them
            foreach (string FileExtension in this._watchedFileFilters.Select(ExtValue => $"*{ExtValue}"))
            {
                FileSystemWatcher NextWatcher = new FileSystemWatcher(this.WatchedDirectoryPath);
                NextWatcher.EnableRaisingEvents = true;
                NextWatcher.IncludeSubdirectories = false;
                NextWatcher.Filter = FileExtension;
                NextWatcher.NotifyFilter =
                    NotifyFilters.Attributes | NotifyFilters.CreationTime |
                    NotifyFilters.DirectoryName | NotifyFilters.FileName |
                    NotifyFilters.LastAccess | NotifyFilters.LastWrite |
                    NotifyFilters.Security | NotifyFilters.Size;

                // Event ties for changes and modified files we can't process directly here
                // These three event types can't be compared inside our file objects because they would require pointless
                // duplicate assignments. So the solution is to tie these changes into the execute watchdog action
                NextWatcher.Created += OnWatchdogFolderChanged;
                NextWatcher.Deleted += OnWatchdogFolderChanged;
                NextWatcher.Renamed += OnWatchdogFolderChanged;

                // Log what extension was just watched
                this._directoryWatchers.Add(NextWatcher);
            }

            // Now store our new watchdog file instances for this path location
            _watchdogLogger.WriteLog("ATTEMPTING TO LOAD AND CREATE NEW WATCHDOG FILE INSTANCES NOW...");
            var MatchingFilesFound = this.WatchedFileFilters
                .SelectMany(FileFilter => Directory.GetFiles(this.WatchedDirectoryPath, FileFilter))
                .ToArray();

            // If no files were found, then don't import them
            if (MatchingFilesFound.Length != 0)
            {
                // Loop all the found file values here and store them as watched file objects
                _watchdogLogger.WriteLog($"FOUND A TOTAL OF {MatchingFilesFound.Length} FILES IN {this.WatchedDirectoryPath}!", LogType.InfoLog);
                foreach (string FilePath in MatchingFilesFound)
                {
                    // Build a new file, store the execute routine as an event handler, and add it onto our list
                    this._watchedFiles.Add(new WatchdogFile(FilePath));
                    _watchdogLogger.WriteLog($"--> ADDED NEW WATCHDOG FILE! FILE NAME {FilePath}", LogType.TraceLog);
                }
            }

            // Setup our default watchdog action here
            _watchdogLogger.WriteLog("STORING DEFAULT ACTION ROUTINE FOR THIS WATCHED FOLDER NOW...");
            this._watchdogAction = () =>
            {
                // Log out the information about our watchdog event being fired here
                _watchdogLogger.WriteLog($"[WATCHDOG EVENT] ::: PROCESSED EVENT FOR WATCHDOG FOLDER {this.WatchedDirectoryName}!");
                _watchdogLogger.WriteLog($"[WATCHDOG EVENT] ::: TOTAL OF {this.WatchedFiles.Length} FILES ARE SEEN IN THIS PATH");
            };

            // Log setup of this folder is now complete
            _watchdogLogger.WriteLog($"CONFIGURED NEW WATCHDOG DIRECTORY {this.WatchedDirectoryName} OK!", LogType.InfoLog);
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Stores a new method value for the action to invoke on our folder when file contents are updated
        /// </summary>
        /// <param name="WatchdogAction">The action to execute when this folder content is updated</param>
        public void AssignWatchdogAction(Action WatchdogAction)
        {
            // Store the new action value and log out we've set a new action routine
            this._watchdogAction = WatchdogAction;
            if (this._watchdogAction == null) _watchdogLogger.WriteLog($"WARNING! NO ACTION IS SET FOR DIRECTORY {this.WatchedDirectoryName}!", LogType.WarnLog);
            else
            {
                // Log out that we've built a new action to set on this folder instance
                _watchdogLogger.WriteLog($"STORED NEW ACTION FOR DIRECTORY {this.WatchedDirectoryName}!", LogType.InfoLog);
                _watchdogLogger.WriteLog("THIS EVENT WILL FIRE WHENEVER EVENTS ARE CALLED FROM THIS WATCHDOG", LogType.InfoLog);
            }
        }
        /// <summary>
        /// Executes the watchdog action if we're able to do so.
        /// If not, we wait for the given timespan needed then run the action again
        /// </summary>
        public void ExecuteWatchdogAction()
        {
            // Make sure we're not running at this point in time
            if (this._isExecuting) return;
            this._isExecuting = true;

            // Check our execution time difference to see if we need to wait to run again or not
            int ElapsedSinceRun = DateTime.Now.Subtract(this._lastExecutionTime).Milliseconds;
            if (ElapsedSinceRun > this._executionGap)
            {
                // Log we're waiting for execution to be allowed and wait for it
                _watchdogLogger.WriteLog("DELAYING EXECUTION FOR GAP!", LogType.WarnLog);
                Thread.Sleep(this._executionGap - ElapsedSinceRun);
            }

            // Now once we've waited long enough, invoke the action and set executing to false once done
            this._watchdogAction.Invoke();

            // Set executing to false and log out when it was run
            this._isExecuting = false; this._lastExecutionTime = DateTime.Now;
            _watchdogLogger.WriteLog($"INVOKED ACTION WITHOUT ISSUES AT {this._lastExecutionTime:G}", LogType.TraceLog);
        }
    }
}