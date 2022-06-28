﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using FulcrumInjector.FulcrumLogic.JsonLogic.JsonHelpers;
using FulcrumInjector.FulcrumLogic.PassThruLogic.PassThruSimulation;
using FulcrumInjector.FulcrumViewContent.Models.SimulationModels;
using FulcrumInjector.FulcrumViewSupport.AvalonEditHelpers.FIlteringFormatters;
using FulcrumInjector.FulcrumViewSupport.AvalonEditHelpers.InjectorSyntaxFormatters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SharpLogger;
using SharpLogger.LoggerObjects;
using SharpLogger.LoggerSupport;
using SharpSimulator;
using SharpSimulator.SimulationEvents;
using SharpSimulator.SimulationObjects;
using SharpSimulator.SupportingLogic;
using SharpWrap2534.PassThruTypes;
using SharpWrap2534.SupportingLogic;

namespace FulcrumInjector.FulcrumViewContent.ViewModels.InjectorCoreViewModels
{
    /// <summary>
    /// View model for playback during the injector simulation processing
    /// </summary>
    public class FulcrumSimulationPlaybackViewModel : ViewModelControlBase
    {
        // Logger object.
        private static SubServiceLogger ViewModelLogger => (SubServiceLogger)LogBroker.LoggerQueue.GetLoggers(LoggerActions.SubServiceLogger)
            .FirstOrDefault(LoggerObj => LoggerObj.LoggerName.StartsWith("InjectorSimPlaybackViewModelLogger")) ?? new SubServiceLogger("InjectorSimPlaybackViewModelLogger");

        // Simulation Helper objects
        public SimulationLoader SimLoader;
        public SimulationPlayer SimPlayer;

        // Private control values
        private bool _isSimLoaded;
        private string _loadedSimFile;
        private string _loadedSimFileContent;

        // Control values for events processed
        private ObservableCollection<SimChannelEventObject> _simChannelsProcessed;
        private ObservableCollection<SimMessageEventObject> _simMessagesProcessed;

        // Public values to bind our UI onto
        public bool IsSimLoaded { get => this._isSimLoaded; set => PropertyUpdated(value); }
        public string LoadedSimFile { get => this._loadedSimFile; set => PropertyUpdated(value); }
        public string LoadedSimFileContent { get => this._loadedSimFileContent; set => PropertyUpdated(value); }

        // Lists of Messages that are being tracked by our simulation
        public ObservableCollection<SimChannelEventObject> SimChannelsProcessed { get => this._simChannelsProcessed; set => PropertyUpdated(value); }
        public ObservableCollection<SimMessageEventObject> SimMessagesProcessed { get => this._simMessagesProcessed; set => PropertyUpdated(value); }

        // ------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Processes an event for a new Simulation channel being built or closed out
        /// </summary>
        /// <param name="SendingObject">Object firing this event</param>
        /// <param name="ChannelEventArgs">Channel changed event arguments</param>
        private void SimPlayer_SimChannelChanged(object SendingObject, SimChannelEventArgs ChannelEventArgs)
        {
            // Convert and build new event to object. Store in temp copy to trigger property updated
            var NextChannelObject = new SimChannelEventObject(ChannelEventArgs);
            var TempCopy = this.SimChannelsProcessed;
            TempCopy.Add(NextChannelObject);

            // Reset value of the collection here. Log passed and return output
            this.SimChannelsProcessed = TempCopy;
            ViewModelLogger.WriteLog("BUILT NEW CONVERSION FOR SIMULATION CHANNEL INTO OBJECT FOR UI BINDING OK!", LogType.TraceLog);
        }
        /// <summary>
        /// Processes changes in events for message contents being pulled in
        /// </summary>
        /// <param name="SendingObject">Object that sent the event</param>
        /// <param name="MessageEventArgs">Event arguments</param>
        private void SimPlayer_SimMessageProcessed(object SendingObject, SimMessageEventArgs MessageEventArgs)
        {
            // Convert and build new event to object. Store in temp copy to trigger property updated
            var NextMessageObject = new SimMessageEventObject(MessageEventArgs);
            var TempCopy = this.SimMessagesProcessed; 
            TempCopy.Add(NextMessageObject);

            // Reset value of the collection here. Log passed and return output
            this.SimMessagesProcessed = TempCopy; 
            ViewModelLogger.WriteLog("BUILT NEW CONVERSION FOR SIMULATION MESSAGE INTO OBJECT FOR UI BINDING OK!", LogType.TraceLog);
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Builds a new simulation playback view model
        /// </summary>
        public FulcrumSimulationPlaybackViewModel()
        {
            // Log built VM OK and build a new sim loader/generator
            this.IsSimLoaded = false;
            ViewModelLogger.WriteLog("BUILT NEW SIMULATION PLAYBACK VIEW MODEL LOGGER AND INSTANCE OK!", LogType.InfoLog);
        }


        /// <summary>
        /// Loads in a new simulation file and stores it onto our view model and view
        /// </summary>
        /// <param name="SimFile">File to load into our simulation playback helper</param>
        /// <returns>True if loaded, false if not</returns>
        public bool LoadSimulation(string SimFile)
        {
            // Try and load the simulation file in first
            if (!File.Exists(SimFile)) {
                ViewModelLogger.WriteLog($"FILE {SimFile} DOES NOT EXIST! CAN NOT LOAD NEW FILE!");
                this.IsSimLoaded = false;
                return false;
            }

            // Build a new Simulation Loader and parse contents of the sim file into it.
            this.SimLoader = new SimulationLoader();
            this.LoadedSimFileContent = File.ReadAllText(SimFile);
            try
            { 
                // Testing JSON conversion
                var PulledChannels = JArray.Parse(this.LoadedSimFileContent);
                foreach (var ChildToken in PulledChannels.Children())
                {
                    try
                    {            
                        // Try and build our channel here
                        SimulationChannel BuiltChannel = ChildToken.ToObject<SimulationChannel>();
                        this.SimLoader.AddSimChannel(BuiltChannel);
                    }
                    catch (Exception ConvertEx)
                    {
                        // Log failures out here
                        ViewModelLogger.WriteLog("FAILED TO CONVERT SIMULATION CHANNEL FROM JSON TO OBJECT!", LogType.ErrorLog);
                        ViewModelLogger.WriteLog("EXCEPTION AND CHANNEL OBJECT ARE BEING LOGGED BELOW...", LogType.WarnLog);
                        ViewModelLogger.WriteLog($"SIM CHANNEL JSON:\n{ChildToken.ToString(Formatting.Indented)}", LogType.TraceLog);
                        ViewModelLogger.WriteLog("EXCEPTION THROWN:", ConvertEx);
                    }
                }

                // Load file contents and store name of file on our view model
                this.IsSimLoaded = true;
                this.LoadedSimFile = SimFile;
                ViewModelLogger.WriteLog($"LOADED NEW SIMULATION FILE {SimFile} OK! STORING CONTENTS OF IT ON VIEW MODEL FOR EDITOR NOW...", LogType.WarnLog);
                ViewModelLogger.WriteLog($"PULLED IN A TOTAL OF {this.SimLoader.SimulationChannels.Length} INPUT SIMULATION CHANNELS INTO OUR LOADER WITHOUT FAILURE!", LogType.InfoLog);
                return true;
            }
            catch (Exception LoadSimEx)
            {
                // Log failure out and return false
                ViewModelLogger.WriteLog($"FAILED TO LOAD IN SIMULATION FILE {SimFile}!", LogType.ErrorLog);
                ViewModelLogger.WriteLog("SIMULATION LOAD EXCEPTION IS BEING LOGGED BELOW!", LoadSimEx);

                // Set Loaded to false and return false
                this.IsSimLoaded = false;
                return false;
            }
        }

        /// <summary>
        /// Kicks off our new Simulation player passing in the specified device and DLL name for our instances
        /// </summary>
        /// <param name="Version">Version of J2534 API to use</param>
        /// <param name="DllName">Name of DLL to use</param>
        /// <param name="DeviceName">Name of device to use</param>
        /// <returns></returns>
        public bool StartSimulation(JVersion Version, string DllName, string DeviceName)
        {
            // Setup the simulation player
            ViewModelLogger.WriteLog("SETTING UP NEW SIMULATION PLAYER FOR THE CURRENTLY BUILT LOADER OBJECT...", LogType.WarnLog);

            // Pull in a base configuration and build a new reader
            // TODO: CONFIGURE THIS TO USE INPUT VALUES FROM SETUP
            var SimConfig = SimulationConfigLoader.LoadSimulationConfig(ProtocolId.ISO15765);
            this.SimPlayer = new SimulationPlayer(this.SimLoader, Version, DllName, DeviceName);
            ViewModelLogger.WriteLog($"NEW SIMULATION PLAYER WITH FOR VERSION {Version} USING DEVICE {DeviceName} ({DllName}) HAS BEEN SETUP!", LogType.InfoLog);

            // Subscribe to the player events and channel events
            this.SimPlayer.SimChannelChanged += SimPlayer_SimChannelChanged;
            this.SimPlayer.SimMessageProcessed += SimPlayer_SimMessageProcessed;
            ViewModelLogger.WriteLog("SUBSCRIBED OUR VIEW MODEL TO OUR SIMULATION PLAYER OK!", LogType.InfoLog);

            // Configure our simulation player here
            this.SimPlayer.SetResponsesEnabled(true);
            this.SimPlayer.SetDefaultConfigurations(SimConfig.ReaderConfigs);
            this.SimPlayer.SetDefaultMessageFilters(SimConfig.ReaderFilters);
            this.SimPlayer.SetDefaultMessageValues(SimConfig.ReaderTimeout, SimConfig.ReaderMsgCount);
            this.SimPlayer.SetDefaultConnectionType(SimConfig.ReaderProtocol, SimConfig.ReaderChannelFlags, SimConfig.ReaderBaudRate);
            ViewModelLogger.WriteLog("CONFIGURED ALL NEEDED SETUP VALUES FOR OUR SIMULATION PLAYER OK! STARTING INIT ROUTINE NOW...", LogType.InfoLog);

            // Run the init routine and start reading output here
            this.SimPlayer.InitializeSimReader(); 
            this.SimPlayer.StartSimulationReader(); 
            ViewModelLogger.WriteLog("STARTED SIMULATION PLAYER FOR OUR LOADED SIMULATION OK! MESSAGE DATA IS BEING PROCESSED TILL THIS TASK IS KILLED!");

            // Return done.
            return true;
        }
        /// <summary>
        /// Stops the simulation reader currently running
        /// </summary>
        /// <returns>True if stopped. False if not</returns>
        public bool StopSimulation()
        {
            // Stop the reader object here if it's playing
            if (!this.SimPlayer.SimulationReading) {
                ViewModelLogger.WriteLog("CAN NOT STOP SIM READER SINCE IT IS NOT CURRENTLY RUNNING!", LogType.ErrorLog);
                return false;
            }

            // Stop it now and log passed
            this.SimPlayer.StopSimulationReader();
            ViewModelLogger.WriteLog("STOPPED SIMULATION READER WITHOUT ISSUES!", LogType.InfoLog);
            return false;
        }
    }
}
