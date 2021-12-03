﻿using System;
using System.IO;
using FulcrumInjector.FulcrumJsonHelpers;
using SharpLogger.LoggerObjects;
using SharpLogger.LoggerSupport;

namespace FulcrumInjector.FulcrumPipeLogic
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
                this._pipeState = value;
                PipeLogger?.WriteLog($"PIPE {this.PipeType} STATE IS NOW: {this._pipeState}", LogType.TraceLog);
            }
        }

        // Location of the FulcrumShim DLL. THIS MUST BE CORRECT!
        public readonly string FulcrumDLLPath = ValueLoaders.GetConfigValue<string>("FulcrumDllPath");

        // Pipe Configurations for the default values.
        public readonly string FulcrumPipeAlpha = "2CC3F0FB08354929BB453151BBAA5A15";
        public readonly string FulcrumPipeBravo = "1D16333944F74A928A932417074DD2B3";

        // Pipe configuration information.
        public readonly string PipeLocation;
        public readonly FulcrumPipeType PipeType;

        // ---------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Checks if the fulcrum DLL Path is locked or not.
        /// </summary>
        /// <returns>True if the file is locked. False if not.</returns>
        public bool FulcrumDllLoaded()
        {
            // Find if the file is locked or not. Get path to validate 
            bool Locked = false;
            try
            {
                // Try open request here.
                FileStream DllStream = File.Open(FulcrumDLLPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                DllStream.Close();
            }
            catch (Exception ex)
            {
                if (ex is IOException) Locked = true;
                if (ex is FileNotFoundException)
                {
                    // Throw a file not located Ex here.
                    PipeLogger.WriteLog("EXCEPTION THROWN DURING DLL IN USE CHECK!", LogType.ErrorLog);
                    PipeLogger.WriteLog($"DLL FILE PROVIDED AT LOCATION {FulcrumDLLPath} COULD NOT BE FOUND!", ex);
                    throw ex;
                }

                // Throw generic Ex
                PipeLogger.WriteLog("FAILED TO CHECK STATE OF OUR DLL FILE!", LogType.ErrorLog);
                PipeLogger.WriteLog("A GENERIC EXCEPTION WAS THROWN DURING THIS CHECK!", ex);
                throw ex;
            }

            // Return the locked status value of our DLL
            return Locked;
        }
        /// <summary>
        /// This method rechecks to see if a new pipe instance can be booted or not.
        /// </summary>
        public void RecheckForNewPipe()
        {
            // Check if the DLL is loaded or not. If it is, then run the new pipe booting instance.
            if (!FulcrumDllLoaded()) { return; }

            // Log starting new pipe and run the init method.
            PipeLogger.WriteLog("FOUND NEW USE CONSUMER OF THE DLL INSTANCE! BOOTING NEW PIPES NOW...", LogType.WarnLog);
            this.ConfigureNewPipe();
        }

        // ---------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Builds a new fulcrum pipe listener
        /// </summary>
        /// <param name="PipeId">ID Of the pipe in use for this object</param>
        protected FulcrumPipe(FulcrumPipeType PipeId)
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
        internal virtual bool ConfigureNewPipe()
        {
            // Log information about building new pipe.
            this.PipeLogger.WriteLog($"BUILDING NEW PIPE OBJECT FROM MAIN PIPE TYPE FOR PIPE ID {this.PipeType}", LogType.WarnLog);
            return true;
        }
    }
}
