﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace FulcrumInjector.FulcrumLogic.FulcrumUpdater
{
    /// <summary>
    /// Class object containing information about the current version of this application
    /// </summary>
    public class InjectorVersionInfo : IComparable
    {
        // Versions of the currently installed shim and injector objects
        public readonly Version ShimVersion;
        public readonly Version InjectorVersion;

        // Same version information as above but shown as string content
        public string ShimVersionString => this.ShimVersion.ToString();
        public string InjectorVersionString => this.InjectorVersion.ToString();

        // ------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Comparision routines for checking two version objects
        /// </summary>
        /// <param name="CompareAgainst">Input object to compare</param>
        /// <returns>An int value showing which version is newer and by how much.</returns>
        public int CompareTo(object CompareAgainst)
        {
            // Check type for conversion information
            if (CompareAgainst is not InjectorVersionInfo)
                throw new InvalidOperationException($"INVALID INPUT TYPE OF {CompareAgainst.GetType()}");

            // Run the comparison. Return a positive number if the input version is newer. Negative if it's lower
            InjectorVersionInfo CastInput = CompareAgainst as InjectorVersionInfo;
            int CurrentInjectorVersionInt =
                this.InjectorVersion.Major +
                this.InjectorVersion.Minor +
                this.InjectorVersion.Build +
                this.InjectorVersion.Revision;
            int InputInjectorVersionInt =
                CastInput.InjectorVersion.Major +
                CastInput.InjectorVersion.Minor +
                CastInput.InjectorVersion.Build +
                CastInput.InjectorVersion.Revision;

            // Return the difference in the two of the int values
            return InputInjectorVersionInt - CurrentInjectorVersionInt;
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Builds a new injector information version object
        /// </summary>
        /// <param name="InjectorVersion">Forced version of the injector</param>
        /// <param name="ShimVersion">Forced version of the shim</param>
        public InjectorVersionInfo(Version InjectorVersion, Version ShimVersion)
        {
            // Store forced values and build string output
            this.ShimVersion = ShimVersion;
            this.InjectorVersion = InjectorVersion;
        }
        /// <summary>
        /// Builds a new injector information version object
        /// </summary>
        /// <param name="InjectorVersion">Forced version of the injector</param>
        /// <param name="ShimVersion">Forced version of the shim</param>
        public InjectorVersionInfo(string InjectorVersion, string ShimVersion)
        {
            // Parse and store versions
            this.ShimVersion = Version.Parse(ShimVersion);
            this.InjectorVersion = Version.Parse(InjectorVersion);
        }

        /// <summary>
        /// Builds a new injector version information object using the current running assembly
        /// </summary>
        public InjectorVersionInfo()
        {
            // Build version information from current object executing
            this.InjectorVersion = Assembly.GetEntryAssembly()?.GetName().Version;
            if (this.InjectorVersion == null)
                throw new InvalidOperationException("FAILED TO FIND OUR CURRENT INJECTOR VERSION!");

            // Now find the ShimDLL version
            string InjectorDllPath =
#if DEBUG
                Path.GetFullPath("..\\..\\..\\FulcrumShim\\Debug\\FulcrumShim.dll");
#else
                ValueLoaders.GetConfigValue<string>("FulcrumInjectorConstants.InjectorDllInformation.FulcrumDLL");  
#endif

            // Make sure the injector DLL Exists
            if (!File.Exists(InjectorDllPath))
                throw new InvalidOperationException($"FAILED TO FIND OUR INJECTOR DLL AT {InjectorDllPath}!");

            // Store version information about the injector shim DLL
            FileVersionInfo InjectorShimFileInfo = FileVersionInfo.GetVersionInfo(InjectorDllPath);
            this.ShimVersion = Version.Parse(InjectorShimFileInfo.FileVersion);
        }
    }
}