﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using FulcrumInjector.AppLogic;
using FulcrumInjector.JsonHelpers;
using FulcrumInjector.ViewControl.Models;
using FulcrumInjector.ViewControl.Views;
using SharpLogger;
using SharpLogger.LoggerObjects;
using SharpLogger.LoggerSupport;

namespace FulcrumInjector.ViewControl.ViewModels
{
    /// <summary>
    /// View Model for Injection Test View
    /// </summary>
    public class FulcrumDllInjectionTestViewModel : ViewModelControlBase
    {
        // Logger object.
        private static SubServiceLogger ViewModelLogger => (SubServiceLogger)LogBroker.LoggerQueue.GetLoggers(LoggerActions.SubServiceLogger)
            .FirstOrDefault(LoggerObj => LoggerObj.LoggerName.StartsWith("InjectorTestViewModelLogger")) ?? new SubServiceLogger("InjectorTestViewModelLogger");

        // Private control values
        private bool _injectionLoadPassed;      // Pass or fail for our injection load process
        private string _injectorDllPath;        // Private value for title view title text
        private string _injectorTestResult;     // Private value for title view version text

        // Public values for our view to bind onto 
        public string InjectorDllPath { get => _injectorDllPath; set => PropertyUpdated(value); }
        public string InjectorTestResult { get => _injectorTestResult; set => PropertyUpdated(value); }
        public bool InjectionLoadPassed { get => _injectionLoadPassed; set => PropertyUpdated(value); }

        // --------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Builds a new VM and generates a new logger object for it.
        /// </summary>
        public FulcrumDllInjectionTestViewModel()
        {
            // Log information and store values 
            ViewModelLogger.WriteLog($"VIEWMODEL LOGGER FOR VM {this.GetType().Name} HAS BEEN STARTED OK!", LogType.InfoLog);
            ViewModelLogger.WriteLog("SETTING UP INJECTOR TEST VIEW BOUND VALUES NOW...", LogType.WarnLog);

            // Store title and version string values now.
            this.InjectorDllPath = ValueLoaders.GetConfigValue<string>("FulcrumInjectorSettings.FulcrumDLL");
            this.InjectorTestResult = "Not Yet Tested";
            ViewModelLogger.WriteLog("LOCATED NEW DLL PATH VALUE OK!", LogType.InfoLog);
            ViewModelLogger.WriteLog($"DLL PATH VALUE PULLED: {this.InjectorDllPath}");
            
            // Log completed setup.
            ViewModelLogger.WriteLog("SETUP NEW DLL INJECTION TESTER VALUES OK!", LogType.InfoLog);
        }

        // --------------------------------------------------------------------------------------------------------------------------

        // PT Open Method object
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int DelegatePassThruOpen(IntPtr DllPointer, out uint DeviceId);
        public DelegatePassThruOpen PTOpen;

        // PT Open Method object
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int DelegatePassThruClose(uint DeviceId);
        public DelegatePassThruClose PTClose;

        // --------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Writes text to our output logging box for the debugging of the injection process
        /// </summary>
        /// <param name="LogText"></param>
        internal void WriteToLogBox(string LogText)
        {
            // Build the current View object into our output and then log into it.
            ViewModelLogger.WriteLog($"[INJECTION TEST OUTPUT] ::: {LogText}", LogType.TraceLog);
        }

        /// <summary>
        /// Test the loading process of the fulcrum DLL Injection objects
        /// </summary>
        /// <param name="InjectionResult">Result String of the injection</param>
        /// <returns>True if the DLL Injects OK. False if not.</returns>
        internal bool PerformDllInjectionTest(out string ResultString)
        {
            // Begin by loading the DLL Object
            this.InjectorTestResult = "Testing...";
            WriteToLogBox($"PULLING IN FULCRUM DLL NOW");
            IntPtr LoadResult = FulcrumWin32Invokers.LoadLibrary(this.InjectorDllPath);
            WriteToLogBox($"RESULT FROM LOADING DLL: {LoadResult}");

            // Make sure the pointer is not 0s. 
            if (LoadResult == IntPtr.Zero)
            {
                // Log failure, set output value and return false
                var ErrorCode = FulcrumWin32Invokers.GetLastError();
                WriteToLogBox("FAILED TO LOAD OUR NEW DLL INSTANCE FOR OUR APPLICATION!");
                WriteToLogBox($"ERROR CODE PROCESSED FROM LOADING REQUEST WAS: {ErrorCode}");

                // Store failure message output
                this.InjectorTestResult = $"Failed! IntPtr.Zero! ({ErrorCode})";
                ResultString = this.InjectorTestResult;
                return false;
            }

            // If Pipes are open, don't try test injection methods
            if (InjectorConstants.FulcrumPipeStatusViewModel.ReaderPipeState != "Connected" &&
                InjectorConstants.FulcrumPipeStatusViewModel.WriterPipeState != "Connected")
            {

                try
                {
                    // Now try and open the selection box view 
                    WriteToLogBox("IMPORTING PT OPEN METHOD AND ATTEMPTING TO INVOKE IT NOW...");
                    IntPtr PassThruOpenCommand = FulcrumWin32Invokers.GetProcAddress(LoadResult, "PassThruOpen");
                    PTOpen = (DelegatePassThruOpen)Marshal.GetDelegateForFunctionPointer(PassThruOpenCommand, typeof(DelegatePassThruOpen));
                    WriteToLogBox("IMPORTED METHOD OK! CALLING IT NOW...");

                    // Invoke method now.
                    PTOpen.Invoke(LoadResult, out uint DeviceId);
                    WriteToLogBox("INVOKE METHOD PASSED! OUTPUT IS BEING LOGGED CORRECTLY AND ALL SELECTION BOX ENTRIES NEEDED ARE POPULATING NOW");
                    WriteToLogBox($"DEVICE ID RETURNED: {DeviceId}");
                }
                catch (Exception ImportEx)
                {
                    // Log failed to connect to our pipe.
                    WriteToLogBox($"FAILED TO ISSUE A PASSTHRU OPEN METHOD USING OUR INJECTED DLL!");
                    WriteToLogBox("EXCEPTION THROWN DURING DYNAMIC CALL OF THE UNMANAGED PT OPEN COMMAND!");
                    ViewModelLogger.WriteLog("EXCEPTION THROWN", ImportEx);

                    // Store output values and fail
                    ResultString = "PTOpen Failed!";
                    return false;
                }
            }
            else { WriteToLogBox("PIPES ARE SEEN TO BE OPEN! NOT TESTING INJECTION SELECTION BOX ROUTINE!"); }

            // Log Passed and then unload our DLL
            WriteToLogBox($"DLL LOADING WAS SUCCESSFUL! POINTER ASSIGNED: {LoadResult}");
            WriteToLogBox("UNLOADING DLL FOR USE BY THE OE APPS LATER ON...");
            if (!FulcrumWin32Invokers.FreeLibrary(LoadResult))
            {
                // Get Error code and build message
                var ErrorCode = FulcrumWin32Invokers.GetLastError();
                this.InjectorTestResult = $"Unload Error! ({ErrorCode})";
                ResultString = this.InjectorTestResult;

                // Write log output
                WriteToLogBox("FAILED TO UNLOAD DLL! THIS IS FATAL!");
                WriteToLogBox($"ERROR CODE PROCESSED FROM UNLOADING REQUEST WAS: {ErrorCode}");
                return false;
            }

            // Return passed and set results.
            WriteToLogBox("UNLOADED DLL OK!");
            this.InjectorTestResult = "Injection Passed!";
            ResultString = this.InjectorTestResult;

            // Log information output
            WriteToLogBox("----------------------------------------------");
            WriteToLogBox("IMPORT PROCESS SHOULD NOT HAVE ISSUES GOING FORWARD!");
            WriteToLogBox("THIS MEANS THE FULCRUM APP SHOULD WORK AS EXPECTED!");
            WriteToLogBox("----------------------------------------------");
            return true;
        }
    }
}
