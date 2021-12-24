﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using ControlzEx.Theming;
using FulcrumInjector.FulcrumLogic.InjectorPipes;
using FulcrumInjector.FulcrumLogic.JsonHelpers;
using FulcrumInjector.FulcrumViewContent.Models;
using FulcrumInjector.FulcrumViewSupport;
using NLog;
using SharpLogger;
using SharpLogger.LoggerObjects;
using SharpLogger.LoggerSupport;

namespace FulcrumInjector
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // Color and Setting Configuration Objects from the config helpers
        public static WindowBlurSetup WindowBlurHelper;
        public static AppThemeConfiguration ThemeConfiguration;

        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Runs this on startup to configure themes and other settings
        /// </summary>
        /// <param name="e">Event args</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            // Startup override
            base.OnStartup(e);

            // Force the working directory. Build JSON settings objects
            string RunningLocation = Assembly.GetExecutingAssembly().Location;
            Directory.SetCurrentDirectory(Path.GetDirectoryName(RunningLocation));
            JsonConfigFiles.SetNewAppConfigFile("FulcrumInjectorSettings.json");

            // Run single instance configuration first
            this.ConfigureSingleInstance();

            // Logging config and app theme config.
            this.ConfigureLogging();
            this.ConfigureLogCleanup();
            LogBroker.Logger?.WriteLog("LOGGING CONFIGURATION ROUTINE HAS BEEN COMPLETED OK!", LogType.InfoLog);

            // Configure settings and app theme
            this.ConfigureMergedDicts();
            this.ConfigureCurrentTheme();
            this.ConfigureUserSettings();
            LogBroker.Logger?.WriteLog("SETTINGS AND THEME SETUP ARE COMPLETE! BOOTING INTO MAIN INSTANCE NOW...", LogType.InfoLog);
        }

        // ------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Checks for an existing fulcrum process object and kill all but the running one.
        /// </summary>
        private void ConfigureSingleInstance()
        {
            // Find all the fulcrum process objects now.
            var CurrentInjector = Process.GetCurrentProcess();
            LogBroker.Logger?.WriteLog("KILLING EXISTING FULCRUM INSTANCES NOW!", LogType.WarnLog);
            LogBroker.Logger?.WriteLog($"CURRENT FULCRUM PROCESS IS SEEN TO HAVE A PID OF {CurrentInjector.Id}", LogType.InfoLog);

            // Find the process values here.
            string CurrentInstanceName = ValueLoaders.GetConfigValue<string>("FulcrumInjectorConstants.AppInstanceName");
            LogBroker.Logger?.WriteLog($"CURRENT INJECTOR PROCESS NAME FILTERS ARE: {CurrentInstanceName} AND {CurrentInjector.ProcessName}");
            var InjectorsTotal = Process.GetProcesses()
                .Where(ProcObj => ProcObj.Id != CurrentInjector.Id)
                .Where(ProcObj => ProcObj.ProcessName.Contains(CurrentInstanceName)
                                  || ProcObj.ProcessName.Contains(CurrentInjector.ProcessName))
                .ToList();

            // Now kill any existing instances
            LogBroker.Logger?.WriteLog($"FOUND A TOTAL OF {InjectorsTotal.Count} INJECTORS ON OUR MACHINE");
            if (InjectorsTotal.Count > 0)
            {
                // Log removing files and delete the log output
                LogBroker.Logger?.WriteLog("SINCE AN EXISTING INJECTOR WAS FOUND, KILLING ALL BUT THE EXISTING INSTANCE!", LogType.InfoLog);
                File.Delete(LogBroker.MainLogFileName);
                Environment.Exit(100);
            }

            // Return passed output.
            LogBroker.Logger?.WriteLog("NO OTHER INSTANCES FOUND! CLAIMING SINGLETON RIGHTS FOR THIS PROCESS OBJECT NOW...");
        }

        // ------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Configure new logging instance setup for configurations.
        /// </summary>
        private void ConfigureLogging()
        {
            // Start by building a new logging configuration object and init the broker.
            string AppName = ValueLoaders.GetConfigValue<string>("FulcrumInjectorConstants.AppInstanceName");
            string LoggingPath = ValueLoaders.GetConfigValue<string>("FulcrumInjectorLogging.DefaultLoggingPath");

            // Make logger and build global logger object.
            LogBroker.ConfigureLoggingSession(AppName, LoggingPath);
            LogBroker.BrokerInstance.FillBrokerPool();

            // Log information and current application version.
            string CurrentAppVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            LogBroker.Logger?.WriteLog($"LOGGING FOR {AppName} HAS BEEN STARTED OK!", LogType.WarnLog);
            LogBroker.Logger?.WriteLog($"{AppName} APPLICATION IS NOW LIVE! VERSION: {CurrentAppVersion}", LogType.WarnLog);
        }
        /// <summary>
        /// Configures logging cleanup to archives if needed.
        /// </summary>
        private void ConfigureLogCleanup()
        {
            // Pull values for log archive trigger and set values
            var ConfigObj = ValueLoaders.GetConfigValue<dynamic>("FulcrumInjectorLogging.LogArchiveSetup");

            // Check to see if we need to archive or not.
            LogBroker.Logger?.WriteLog($"CLEANUP ARCHIVE FILE SETUP STARTED! CHECKING FOR {ConfigObj.ArchiveOnFileCount} OR MORE LOG FILES...");
            if (Directory.GetFiles(LogBroker.BaseOutputPath).Length < (int)ConfigObj.ArchiveOnFileCount)
            {
                // Log not cleaning up and return.
                LogBroker.Logger?.WriteLog("NO NEED TO ARCHIVE FILES AT THIS TIME! MOVING ON", LogType.WarnLog);
                if (Directory.GetFiles((string)ConfigObj.LogArchivePath).Length < (int)ConfigObj.ArchiveCleanupFileCount)
                {
                    // Log not cleaning anything up since all values are under thresholds
                    LogBroker.Logger?.WriteLog("NOT CONFIGURING ARCHIVE CLEANUP AT THIS TIME EITHER!", LogType.WarnLog);
                    return;
                }

                // Configure cleanup for archive entries
                LogBroker.Logger?.WriteLog("CLEANING UP ARCHIVE FILE ENTRIES NOW...", LogType.InfoLog);
                LogBroker.CleanupArchiveHistory((string)ConfigObj.LogArchivePath, "", (int)ConfigObj.ArchiveOnFileCount);

                // Cleanup the shim entries now
                LogBroker.Logger?.WriteLog("CLEANING UP SHIM ENTRIES AND ARCHIVES NOW...", LogType.InfoLog);
                LogBroker.CleanupArchiveHistory(
                    (string)ConfigObj.LogArchivePath,
                    ValueLoaders.GetConfigValue<string>("FulcrumInjectorConstants.ShimInstanceName"),
                    (int)ConfigObj.ArchiveOnFileCount
                );

                // Log complete
                LogBroker.Logger?.WriteLog("DONE CLEANING UP ARCHIVE SETS FOR BOTH THE SHIM AND INJECTOR!", LogType.InfoLog);
                return;
            }

            // Begin archive process 
            var ShimFileFilterName = ValueLoaders.GetConfigValue<string>("FulcrumInjectorConstants.ShimInstanceName"); ;
            LogBroker.Logger?.WriteLog($"ARCHIVE PROCESS IS NEEDED! PATH TO STORE FILES IS SET TO {ConfigObj.LogArchivePath}");
            LogBroker.Logger?.WriteLog($"SETTING UP SETS OF {ConfigObj.ArchiveFileSetSize} FILES IN EACH ARCHIVE OBJECT!");
            Task.Run(() =>
            {
                // Run on different thread to avoid clogging up UI
                LogBroker.CleanupLogHistory(ConfigObj.ToString(), "");
                LogBroker.CleanupLogHistory(ConfigObj.ToString(), ShimFileFilterName);

                // See if we have too many archives
                string[] ArchivesFound = Directory.GetFiles(ConfigObj.LogArchivePath);
                int ArchiveSetCount = ConfigObj.ArchiveFileSetSize is int ? (int)ConfigObj.ArchiveFileSetSize : 0;
                if (ArchivesFound.Length >= ArchiveSetCount * 2)
                {
                    // List of files to remove now.
                    LogBroker.Logger?.WriteLog("REMOVING OVERFLOW OF ARCHIVE VALUES NOW...", LogType.WarnLog);
                    var RemoveThese = ArchivesFound
                        .OrderByDescending(FileObj => new FileInfo(FileObj).LastWriteTime)
                        .Skip(ArchiveSetCount * 2);

                    // Remove the remainder now.
                    LogBroker.Logger?.WriteLog($"FOUND A TOTAL OF {RemoveThese.Count()} FILES TO PRUNE");
                    foreach (var FileObject in RemoveThese) { File.Delete(FileObject); }
                    LogBroker.Logger?.WriteLog($"REMOVED ALL THE REQUIRED ARCHIVES OK! LEFT A TOTAL OF {ArchiveSetCount * 2} ARCHIVES BEHIND!", LogType.InfoLog);
                }

                // Log done.
                LogBroker.Logger?.WriteLog($"DONE CLEANING UP LOG FILES! CHECK {ConfigObj.LogArchivePath} FOR NEWLY BUILT ARCHIVE FILES", LogType.InfoLog);
            });
        }

        // ------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Pulls in the resource dictionaries from the given resource path and stores them in the app
        /// </summary>
        private void ConfigureMergedDicts()
        {
            // Log information. Pull files in and store them all
            LogBroker.Logger?.WriteLog("IMPORTING RESOURCE DICTIONARIES FROM XAML OUTPUT DIRECTORY NOW...", LogType.WarnLog);
        }

        /// <summary>
        /// Configure new theme setup for instance objects.
        /// </summary>
        private void ConfigureCurrentTheme()
        {
            // Log infos and set values.
            LogBroker.Logger?.WriteLog("SETTING UP MAIN APPLICATION THEME VALUES NOW...", LogType.InfoLog);

            // Set theme configurations
            ThemeManager.Current.SyncTheme();
            ThemeConfiguration = new AppThemeConfiguration();
            ThemeConfiguration.CurrentAppTheme = ThemeConfiguration.PresetThemes[0];
            LogBroker.Logger?.WriteLog("CONFIGURED NEW APP THEME VALUES OK! THEME HAS BEEN APPLIED TO APP INSTANCE!", LogType.InfoLog);
        }
        /// <summary>
        /// Pulls in the user settings from our JSON configuration file and stores them to the injector store 
        /// </summary>
        private void ConfigureUserSettings()
        {
            // Build a logger for this method
            var SettingsLogger = (SubServiceLogger)LogBroker.LoggerQueue.GetLoggers(LoggerActions.SubServiceLogger)
                .FirstOrDefault(LoggerObj => LoggerObj.LoggerName.StartsWith("UserSettingConfigLogger")) ?? new SubServiceLogger("UserSettingConfigLogger");

            // Pull our settings objects out from the settings file.
            var SettingsLoaded =
                ValueLoaders.GetConfigValue<SettingsEntryCollectionModel[]>("FulcrumUserSettings");

            // Log information and build UI content view outputs
            SettingsLogger?.WriteLog($"PULLED IN {SettingsLoaded.Length} SETTINGS SEGMENTS OK!", LogType.InfoLog);
            SettingsLogger?.WriteLog("SETTINGS ARE BEING LOGGED OUT TO THE DEBUG LOG FILE NOW...", LogType.InfoLog);
            foreach (var SettingSet in SettingsLoaded) SettingsLogger?.WriteLog($"[SETTINGS COLLECTION] ::: {SettingSet}");

            // Log passed and return output
            SettingsLogger?.WriteLog("IMPORTED SETTINGS OBJECTS CORRECTLY! READY TO GENERATE UI COMPONENTS FOR THEM NOW...");
        }
    }
}
