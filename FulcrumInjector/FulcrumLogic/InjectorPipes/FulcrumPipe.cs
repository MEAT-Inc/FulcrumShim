﻿using System;
using System.IO;
using System.Threading.Tasks;
using FulcrumInjector.FulcrumLogic.InjectorPipes.PipeEvents;
using FulcrumInjector.FulcrumLogic.JsonHelpers;
using SharpLogger.LoggerObjects;
using SharpLogger.LoggerSupport;

namespace FulcrumInjector.FulcrumLogic.InjectorPipes
{
    /// <summary>
    /// Enums for pipe types
    /// </summary>
    public enum FulcrumPipeType
    {
        FulcrumPipeAlpha,      // Pipe number 1 (Input)
        FulcrumPipeBravo,      // Pipe number 2 (Output)
    }
    /// <summary>
    /// Possible states for our pipe objects.
    /// </summary>
    public enum FulcrumPipeState
    {
        Faulted,            // Failed to build
        Open,               // Open and not connected
        Connected,          // Connected
        Disconnected,       // Disconnected
        Closed,             // Open but closed manually
    }

    /// <summary>
    /// Instance object for reading pipe server data from our fulcrum DLL
    /// </summary>
    public class FulcrumPipe
    {
        // Fulcrum Logger. Build this once the pipe is built.
        internal readonly SubServiceLogger PipeLogger;

        // State of the pipe reading client object
        protected FulcrumPipeState _pipeState;
        public FulcrumPipeState PipeState
        {
            get => _pipeState;
            protected set
            {
                // Fire new event state args
                this.OnPipeStateChanged(new FulcrumPipeStateChangedEventArgs()
                {
                    NewState = value,
                    OldState = this._pipeState,
                    TimeChanged = DateTime.Now
                });

                // Store new value objects
                this._pipeState = value;
                PipeLogger?.WriteLog($"PIPE {this.PipeType} STATE IS NOW: {this._pipeState}", LogType.TraceLog);
            }
        }

        // Location of the FulcrumShim DLL. THIS MUST BE CORRECT!
        // Changed this for debugging mode. THis way the DLL is pulled local if debugging or live if in release
        // If we're not inside the program files dir, then use our local debug fallback path value
        public readonly string FulcrumDLLPath =
#if DEBUG
            "..\\..\\..\\FulcrumShim\\Debug\\FulcrumShim.dll";
#else
            ValueLoaders.GetConfigValue<string>("FulcrumInjectorConstants.InjectorDllInformation.FulcrumDLL");
#endif

        // Pipe Configurations for the default values.
        public string FulcrumPipeAlpha =>
            ValueLoaders.GetConfigValue<string>("FulcrumInjectorConstants.InjectorPipeLocations.ShimOutputPipe");
        public string FulcrumPipeBravo =>
            ValueLoaders.GetConfigValue<string>("FulcrumInjectorConstants.InjectorPipeLocations.ShimInputPipe");

        // Pipe configuration information.
        public readonly string PipeLocation;
        public readonly FulcrumPipeType PipeType;

        // ---------------------------------------------------------------------------------------------------------------

        // Event triggers for pipe data input
        public event EventHandler<FulcrumPipeStateChangedEventArgs> PipeStateChanged;
        protected void OnPipeStateChanged(FulcrumPipeStateChangedEventArgs EventArgs) { PipeStateChanged?.Invoke(this, EventArgs); }

        // ---------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Checks if the fulcrum DLL Path is locked or not.
        /// </summary>
        /// <returns>True if the file is locked. False if not.</returns>
        public bool FulcrumDllLoaded()
        {
            try
            {
                // Find if the file is locked or not. Get path to validate 
                if (!File.Exists(FulcrumDLLPath)) { PipeLogger.WriteLog("WARNING! DLL FILE WAS NOT FOUND!", LogType.WarnLog); }
                FileStream DllStream = File.Open(FulcrumDLLPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                DllStream.Close();

                // Return not locked here.
                return false;
            }
            catch (Exception ex)
            {
                // Set locked value to true and return it here.
                if (ex is not FileNotFoundException) return true;

                // Throw a file not located Ex here.
                PipeLogger.WriteLog("EXCEPTION THROWN DURING DLL IN USE CHECK!", LogType.ErrorLog);
                PipeLogger.WriteLog($"DLL FILE PROVIDED AT LOCATION {FulcrumDLLPath} COULD NOT BE FOUND!", ex);
                throw ex;
            }
        }

        // ---------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Builds a new fulcrum pipe listener
        /// </summary>
        /// <param name="PipeId">ID Of the pipe in use for this object</param>
        private protected FulcrumPipe(FulcrumPipeType PipeId)
        {
            // Configure logger object.
            this.PipeState = FulcrumPipeState.Faulted;
            this.PipeLogger = new SubServiceLogger($"{PipeId}");
            this.PipeLogger.WriteLog($"BUILT NEW PIPE LOGGER FOR PIPE TYPE {PipeId} OK!", LogType.InfoLog);

            // Store information about the pipe being configured
            this.PipeType = PipeId;
            this.PipeLocation = this.PipeType == FulcrumPipeType.FulcrumPipeAlpha ? FulcrumPipeAlpha : FulcrumPipeBravo;
            this.PipeLogger.WriteLog("STORED NEW PIPE DIRECTIONAL INFO AND TYPE ON THIS INSTANCE CORRECTLY!", LogType.InfoLog);
        }

        // ---------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Base method for new pipe init configuration
        /// </summary>
        /// <returns>Always true.</returns>
        internal virtual bool AttemptPipeConnection(out Task<bool> ConnectionTask)
        {
            // Log information about building new pipe.
            ConnectionTask = null;
            this.PipeLogger.WriteLog($"BUILDING NEW PIPE OBJECT FROM MAIN PIPE TYPE FOR PIPE ID {this.PipeType}", LogType.WarnLog);
            return true;
        }
    }
}