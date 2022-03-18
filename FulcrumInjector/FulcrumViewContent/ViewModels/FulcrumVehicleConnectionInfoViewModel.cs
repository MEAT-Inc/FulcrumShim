﻿using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FulcrumInjector.FulcrumLogic.JsonHelpers;
using FulcrumInjector.FulcrumLogic.PassThruAutoID;
using FulcrumInjector.FulcrumLogic.PassThruWatchdog;
using FulcrumInjector.FulcrumViewContent.Models.EventModels;
using FulcrumInjector.FulcrumViewContent.Models.SettingsModels;
using Newtonsoft.Json;
using SharpLogger;
using SharpLogger.LoggerObjects;
using SharpLogger.LoggerSupport;
using SharpWrap2534;
using SharpWrap2534.J2534Objects;
using SharpWrap2534.PassThruImport;
using SharpWrap2534.PassThruTypes;
using SharpWrap2534.SupportingLogic;

namespace FulcrumInjector.FulcrumViewContent.ViewModels
{
    /// <summary>
    /// View model object for our connected vehicle information helper
    /// </summary>
    public class FulcrumVehicleConnectionInfoViewModel : ViewModelControlBase
    {
        // Logger object.
        private static SubServiceLogger ViewModelLogger => (SubServiceLogger)LogBroker.LoggerQueue.GetLoggers(LoggerActions.SubServiceLogger)
            .FirstOrDefault(LoggerObj => LoggerObj.LoggerName.StartsWith("VehicleConnectionViewModelLogger")) ?? new SubServiceLogger("VehicleConnectionViewModelLogger");

        // --------------------------------------------------------------------------------------------------------------------------

        // Task control for stopping refresh operations for our background voltage reading.
        private Sharp2534Session InstanceSession;
        private CancellationTokenSource RefreshSource;

        // Private control values
        private JVersion _versionType;         // J2534 Version in use
        private string _selectedDLL;           // Name of the currently selected DLL
        private string _selectedDevice;        // Name of the currently connected and consumed Device
        private double _deviceVoltage;         // Last known voltage value. If no device found, this returns 0.00
        private string _vehicleVIN;            // VIN Of the current vehicle connected
        private string _vehicleInfo;           // YMM String of the current vehicle
        private bool _autoIdRunning;           // Sets if AUTO Id routines are running at this time or not.
        private bool _canManualId;             // Sets if we can start a new manual ID value
        private bool _isMonitoring;            // Sets if we're monitoring input voltage on the vehicle or not.

        // Public values for our view to bind onto 
        public string VehicleVin
        {
            get => string.IsNullOrWhiteSpace(_vehicleVIN) ? "No VIN Number" : _vehicleVIN; 
            set => PropertyUpdated(value);
        }            
        public string VehicleInfo
        {
            get => string.IsNullOrWhiteSpace(_vehicleInfo) ? "No VIN Number To Decode" : _vehicleInfo; 
            set => PropertyUpdated(value);
        }

        // Device Information
        public string SelectedDLL { get => _selectedDLL; set => PropertyUpdated(value); }
        public double DeviceVoltage { get => _deviceVoltage; set => PropertyUpdated(value); }
        public string SelectedDevice
        {
            get => _selectedDevice ?? "No Device Selected";
            set
            {
                // Check for the same value passed in again or nothing passed at all.
                if (value == this._selectedDevice) {
                    ViewModelLogger.WriteLog("NOT BUILDING NEW SESSION FOR IDENTICAL DEVICE NAME!", LogType.TraceLog);
                    return;
                }

                // Check for a null device name or no device name provided.
                if (string.IsNullOrWhiteSpace(value) || value == "No Device Selected") 
                {
                    // Update private values, dispose the instance.
                    PropertyUpdated(value);
                    if (this.IsMonitoring) this.StopVehicleMonitoring();
                    
                    // Log information and return output.
                    ViewModelLogger.WriteLog("STOPPED SESSION INSTANCE OK AND CLEARED OUT DEVICE NAME!", LogType.InfoLog);
                    return;
                }

                // Update private values, dispose of the instance
                PropertyUpdated(value);
                if (this.IsMonitoring) this.StopVehicleMonitoring();
 
                // If our new device name is not null, then we can build a new object.
                this.InstanceSession = new Sharp2534Session(this._versionType, this._selectedDLL, this._selectedDevice);
                ViewModelLogger.WriteLog("CONFIGURED VIEW MODEL CONTENT OBJECTS FOR BACKGROUND REFRESHING OK!", LogType.InfoLog);

                try
                {
                    // Check if we want to use voltage monitoring or not.
                    if (!FulcrumSettingsShare.InjectorGeneralSettings.GetSettingValue("Enable Vehicle Monitoring", true)) {
                        ViewModelLogger.WriteLog("NOT USING VOLTAGE MONITORING ROUTINES SINCE THE USER HAS SET THEM TO OFF!", LogType.WarnLog);
                        ViewModelLogger.WriteLog("TRYING TO PULL A VOLTAGE READING ONCE!", LogType.InfoLog);
                        return;
                    }

                    // Start monitoring. Throw if this fails.
                    if (!this.StartVehicleMonitoring()) throw new InvalidOperationException("FAILED TO START OUR MONITORING ROUTINE!");
                    ViewModelLogger.WriteLog("STARTED MONITORING ROUTINE OK!", LogType.InfoLog);
                    ViewModelLogger.WriteLog("WHEN A VOLTAGE OVER 11.0 IS FOUND, A VIN REQUEST WILL BE MADE!", LogType.InfoLog);
                }
                catch (Exception SetupSessionEx)
                {
                    // Log failures for starting routine here
                    ViewModelLogger.WriteLog("FAILED TO START OUR MONITORING ROUTINES!", LogType.ErrorLog);
                    ViewModelLogger.WriteLog("THIS IS LIKELY DUE TO A DEVICE IN USE OR SOMETHING CONSUMING OUR PT INTERFACE!", LogType.ErrorLog);
                    ViewModelLogger.WriteLog("IF THE DEVICE IS NOT IN USE AND THIS IS HAPPENING, IT'S LIKELY A BAD DEVICE", LogType.ErrorLog);
                    ViewModelLogger.WriteLog("EXCEPTION THROWN DURING SETUP ROUTINE!", SetupSessionEx);
                }
            }
        }

        // Auto ID control values
        public bool AutoIdRunning
        {
            get => _autoIdRunning;
            set
            {
                // Set the new value and set Can ID to false if value is now true
                PropertyUpdated(value);
                this.CanManualId = !value;
            }
        }
        public bool CanManualId { get => _canManualId; set => PropertyUpdated(value); }
        public bool IsMonitoring { get => _isMonitoring; set => PropertyUpdated(value); }

        // --------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Builds a new VM and generates a new logger object for it.
        /// </summary>
        public FulcrumVehicleConnectionInfoViewModel()
        {
            // Log information and store values 
            ViewModelLogger.WriteLog($"VIEWMODEL LOGGER FOR VM {this.GetType().Name} HAS BEEN STARTED OK!", LogType.InfoLog);
            ViewModelLogger.WriteLog("SETTING UP HARDWARE INSTANCE VIEW BOUND VALUES NOW...", LogType.WarnLog);

            // Build an instance session if our DLL and Device are not null yet. 
            var DLLSelected = InjectorConstants.FulcrumInstalledHardwareViewModel?.SelectedDLL;
            if (DLLSelected == null) {
                ViewModelLogger.WriteLog("NO DLL ENTRY WAS FOUND TO BE USED YET! NOT CONFIGURING A NEW SESSION...", LogType.InfoLog);
                return;
            }

            // Store DLL Values and device instance
            this._selectedDLL = DLLSelected.Name;
            this._versionType = DLLSelected.DllVersion;
            ViewModelLogger.WriteLog($"ATTEMPTING TO BUILDING NEW SESSION FOR DEVICE NAMED {SelectedDevice} FROM CONNECTION VM...", LogType.WarnLog);
            ViewModelLogger.WriteLog($"WITH DLL {DLLSelected.Name} (VERSION: {DLLSelected.DllVersion}", LogType.WarnLog);

            // Build our session instance here.
            this.SelectedDevice = InjectorConstants.FulcrumInstalledHardwareViewModel?.SelectedDevice;
            ViewModelLogger.WriteLog($"STORED NEW DEVICE VALUE OF {this.SelectedDevice}", LogType.InfoLog);
        }

        // -------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Pulls out the VIN number of the current vehicle and stores the voltage of it.
        /// </summary>
        /// <returns>True if pulled ok. False if not.</returns>
        internal bool ReadVoltageAndVin()
        {
            // Now build our session instance and pull voltage first.
            bool NeedsMonitoringReset = this.IsMonitoring;
            if (NeedsMonitoringReset) this.StopVehicleMonitoring();
            ViewModelLogger.WriteLog($"BUILT NEW SESSION INSTANCE FOR DEVICE NAME {this.SelectedDevice} OK!", LogType.InfoLog);

            // Store voltage value, log information. If voltage is less than 11.0, then exit.
            this.InstanceSession.PTOpen();
            this.DeviceVoltage = this.ReadDeviceVoltage();
            if (this.DeviceVoltage < 11.0) {
                ViewModelLogger.WriteLog("ERROR! VOLTAGE VALUE IS LESS THAN THE ACCEPTABLE 11.0V CUTOFF! NOT AUTO IDENTIFYING THIS CAR!", LogType.ErrorLog);
                return false;
            }

            // Return passed and store our new values
            bool VinResult = this.ReadVehicleVin(out string NewVin, out var ProcPulled);
            if (!VinResult) { ViewModelLogger.WriteLog("FAILED TO PULL A VIN VALUE!", LogType.ErrorLog); }
            else 
            {
                // Log information, store new values.
                this.VehicleVin = NewVin;
                ViewModelLogger.WriteLog($"VOLTAGE VALUE PULLED OK! READ IN NEW VALUE {this.DeviceVoltage:F2}!", LogType.InfoLog);
                ViewModelLogger.WriteLog($"PULLED VIN NUMBER: {this.VehicleVin} WITH PROTOCOL ID: {ProcPulled}!", LogType.InfoLog);
            }

            // Kill our session here
            ViewModelLogger.WriteLog("CLOSING REQUEST SESSION MANUALLY NOW...", LogType.WarnLog); 

            // Return the result of our VIN Request
            ViewModelLogger.WriteLog("SESSION CLOSED AND NULLIFIED OK!", LogType.InfoLog);
            if (NeedsMonitoringReset) this.StartVehicleMonitoring();
            return VinResult;
        }
        /// <summary>
        /// Updates our device voltage value based on our currently selected device information
        /// </summary>
        /// <returns>Voltage of the device connected and selected</returns>
        private double ReadDeviceVoltage() 
        {
            try 
            {
                // Now with our new channel ID, we open an instance and pull the channel voltage.
                this.InstanceSession.PTReadVoltage(out var DoubleVoltage, true); 
                return DoubleVoltage;
            }
            catch 
            {
                // Log failed to read value, return 0.00v
                ViewModelLogger.WriteLog("FAILED TO READ NEW VOLTAGE VALUE!", LogType.ErrorLog);
                return 0.00;
            }
        }
        /// <summary>
        /// Pulls a VIN From a vehicle connected to our car
        /// </summary>
        /// <param name="VinString"></param>
        /// <param name="ProtocolUsed"></param>
        /// <returns></returns>
        private bool ReadVehicleVin(out string VinString, out ProtocolId ProtocolUsed)
        {
            // Get a list of all supported protocols and then pull in all the types of auto ID routines we can use
            var SupportedRoutines = ValueLoaders.GetConfigValue<string[]>("FulcrumAutoIdRoutines");
            var UsableTypes = SupportedRoutines.Select(ProtocolTypeString =>
            {
                // Get the type for the Auto ID routine here.
                var AutoIdBaseType = Assembly.GetExecutingAssembly().GetTypes()?.FirstOrDefault(TypeObj => TypeObj.Name.Contains("AutoIdRoutine"));
                if (AutoIdBaseType == null) throw new TypeAccessException("FAILED TO FIND TYPE BASE FOR OUR AUTO ID ROUTINE!");

                // Now build the type for our auto ID instance based on the protocol.
                string AutoIdTypeName = $"{AutoIdBaseType.Namespace}.AutoIdRoutine_{ProtocolTypeString}";
                ViewModelLogger.WriteLog($"TRYING TO BUILD TYPE FOR AUTO ID NAMED {AutoIdTypeName}", LogType.InfoLog);
                try
                {
                    // Get the type, build arguments, and generate object
                    Type AutoIdType = Type.GetType(AutoIdTypeName);
                    ViewModelLogger.WriteLog($"--> TYPE PARSED OK! TYPE FOUND WAS: {AutoIdType.FullName}", LogType.InfoLog);
                    return AutoIdType;
                }
                catch (Exception TypeLookupEx)
                {
                    // Log the failures and return nothing.
                    ViewModelLogger.WriteLog($"FAILED TO FIND TYPE: {ProtocolTypeString}!", LogType.ErrorLog);
                    ViewModelLogger.WriteLog("EXCEPTION THROWN DURING TYPE DETECTION ROUTINE!", TypeLookupEx);
                    return null;
                }
            }).Where(TypeObj => TypeObj != null).ToArray();

            // Now one by one build instances and attempt connections
            this.AutoIdRunning = true;
            foreach (var TypeValue in UsableTypes)
            {
                // Cast the protocol object and built arguments for our instance constructor.
                string ProtocolTypeString = TypeValue.Name.Split('_')[1];
                ProtocolId CastProtocol = (ProtocolId)Enum.Parse(typeof(ProtocolId), ProtocolTypeString);
                object[] InitArgs = { this.InstanceSession };
                ViewModelLogger.WriteLog("--> BUILT NEW ARGUMENTS FOR TYPE GENERATION OK!", LogType.InfoLog);
                ViewModelLogger.WriteLog($"--> TYPE ARGUMENTS: {JsonConvert.SerializeObject(InitArgs, Formatting.None)}", LogType.TraceLog);

                // Generate our instance here and try to store our VIN
                AutoIdRoutine AutoIdInstance = (AutoIdRoutine)Activator.CreateInstance(TypeValue, InitArgs);
                ViewModelLogger.WriteLog($"BUILT NEW INSTANCE OF SESSION FOR TYPE {TypeValue} OK!", LogType.InfoLog);
                ViewModelLogger.WriteLog("PULLING VIN AND OPENING CHANNEL FOR TYPE INSTANCE NOW...", LogType.InfoLog);

                // Connect our channel, read the vin, and then close it.
                AutoIdInstance.RetrieveVinNumber(out VinString);
                ViewModelLogger.WriteLog("VIN REQUEST ROUTINE AND CONNECTION PASSED!", LogType.InfoLog);
                ViewModelLogger.WriteLog($"USED CHANNEL ID: {AutoIdInstance.ChannelIdOpened}", LogType.TraceLog);
                ViewModelLogger.WriteLog("CLOSING OUR SESSION INSTANCE DOWN NOW...", LogType.TraceLog);
                AutoIdInstance.CloseSession();

                // Check our VIN Value
                ProtocolUsed = CastProtocol;
                if (VinString is not { Length: 17 }) ViewModelLogger.WriteLog("NO VIN NUMBER WAS FOUND! MOVING ONTO NEXT PROTOCOL...", LogType.WarnLog);
                else
                {
                    // Log our new vin number pulled, return out of this method
                    ViewModelLogger.WriteLog($"VIN VALUE LOCATED: {VinString}", LogType.InfoLog);
                    ViewModelLogger.WriteLog("VIN NUMBER WAS PULLED CORRECTLY! STORING IT ONTO OUR CLASS INSTANCE NOW...", LogType.InfoLog);
                    this.AutoIdRunning = false;
                    return true;
                }
            }

            // If we got here, fail out.
            this.AutoIdRunning = false;
            VinString = null; ProtocolUsed = default;
            ViewModelLogger.WriteLog($"FAILED TO FIND A VIN NUMBER AFTER SCANNING {UsableTypes.Length} DIFFERENT TYPE PROTOCOLS!", LogType.ErrorLog);
            return false;
        }

        
        /// <summary>
        /// Consumes our active device and begins a voltage reading routine.
        /// </summary>
        /// <returns>True if consumed, false if not.</returns>
        private bool StartVehicleMonitoring()
        {
            // Try and kill old sessions then begin refresh routine
            this.RefreshSource = new CancellationTokenSource();
            int RefreshTimer = 500; IsMonitoring = true; bool VinReadRun = false;
            ViewModelLogger.WriteLog("STARTING VOLTAGE REFRESH ROUTINE NOW...", LogType.InfoLog);

            // Run as a task to avoid locking up UI
            this.InstanceSession.PTOpen();
            Task.Run(() =>
            {
                // Do this as long as we need to keep reading based on the token
                while (!this.RefreshSource.IsCancellationRequested)
                {
                    // Pull in our next voltage value here. Check for voltage gained or removed
                    Thread.Sleep(RefreshTimer);
                    this.DeviceVoltage = this.ReadDeviceVoltage();

                    // Check our voltage value. Perform actions based on value pulled
                    if (this.DeviceVoltage >= 11 && !VinReadRun)
                    {
                        // Log information, pull our vin number, then restart this process using the OnLost value.
                        RefreshTimer = 1500;
                        if (!FulcrumSettingsShare.InjectorGeneralSettings.GetSettingValue("Enable Auto ID Routines", true)) {
                            ViewModelLogger.WriteLog("NOT USING VEHICLE AUTO ID ROUTINES SINCE THE USER HAS SET THEM TO OFF!", LogType.WarnLog);
                            continue;
                        }

                        // Pull our Vin number of out the vehicle now.
                        if (this.ReadVehicleVin(out var VinFound, out ProtocolId ProtocolUsed))
                        {
                            // Log information, store these values.
                            this.VehicleVin = VinFound; VinReadRun = true;
                            ViewModelLogger.WriteLog("PULLED NEW VIN NUMBER VALUE OK!", LogType.InfoLog);
                            ViewModelLogger.WriteLog($"VIN PULLED: {VinFound}", LogType.InfoLog);
                            ViewModelLogger.WriteLog($"PROTOCOL USED TO PULL VIN: {ProtocolUsed}", LogType.InfoLog);

                            // Store class values, cancel task, and restart it for on lost.
                            ViewModelLogger.WriteLog("STARTING NEW TASK TO WAIT FOR VOLTAGE BEING LOST NOW...", LogType.WarnLog);
                            continue;
                        }

                        // Log failures and move on. This only happens when a VIN is not found.
                        ViewModelLogger.WriteLog("FAILED TO FIND A NEW VIN NUMBER FOR OUR VEHICLE!", LogType.ErrorLog);
                        this.VehicleVin = "VIN REQUEST ERROR!"; VinReadRun = true;
                        continue;
                    }

                    // Check for voltage lost instead of connected.
                    RefreshTimer = 250; this.VehicleVin = null;
                    ViewModelLogger.WriteLog("LOST OBD 12V INPUT! CLEARING OUT STORED VALUES NOW...", LogType.InfoLog);
                    ViewModelLogger.WriteLog("CLEARED OUT LAST KNOWN VALUES FOR LOCATED VEHICLE VIN OK!", LogType.InfoLog);
                };
            }, this.RefreshSource.Token);

            // Log started, return true.
            ViewModelLogger.WriteLog("LOGGING VOLTAGE TO OUR LOG FILES AND PREPARING TO READ TO VIEW MODEL", LogType.InfoLog);
            return true;
        }
        /// <summary>
        /// Stops a refresh session. 
        /// </summary>
        /// <returns>True if stopped ok. False if not.</returns>
        private void StopVehicleMonitoring()
        {
            // Reset all values here.
            ViewModelLogger.WriteLog($"STOPPING REFRESH SESSION TASK FOR DEVICE {SelectedDevice} NOW...", LogType.WarnLog);
            this.RefreshSource?.Cancel();

            // Dispose our instance object here
            this.InstanceSession.Dispose();
            if (this.SelectedDevice != "No Device Selected") 
                this.InstanceSession = new Sharp2534Session(this._versionType, this._selectedDLL, this.SelectedDevice);
            ViewModelLogger.WriteLog("DISPOSING AND RECREATION PASSED FOR SHARP SESSION! KILLED OUR INSTANCE WITHOUT ISSUES!", LogType.InfoLog);

            // Setup task objects again.
            IsMonitoring = false; this.VehicleVin = null; this.DeviceVoltage = 0.00;
            ViewModelLogger.WriteLog("FORCING VOLTAGE BACK TO 0.00 AND RESETTING INFO STRINGS", LogType.WarnLog);
            ViewModelLogger.WriteLog("STOPPED REFRESHING AND KILLED OUR INSTANCE OK!", LogType.InfoLog);
        }
    }
}
