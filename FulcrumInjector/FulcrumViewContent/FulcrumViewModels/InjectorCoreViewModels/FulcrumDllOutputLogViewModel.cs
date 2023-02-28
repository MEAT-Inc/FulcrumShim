﻿using System;
using System.Windows.Controls;
using FulcrumInjector.FulcrumViewContent.FulcrumViews.InjectorCoreViews;
using FulcrumInjector.FulcrumViewSupport.FulcrumLogFormatters.FilteringFormatters;
using FulcrumInjector.FulcrumViewSupport.FulcrumLogFormatters.InjectorSyntaxFormatters;
using SharpLogging;
using SharpPipes;

namespace FulcrumInjector.FulcrumViewContent.FulcrumViewModels.InjectorCoreViewModels
{
    /// <summary>
    /// ViewModel for the DLL output content view
    /// </summary>
    internal class FulcrumDllOutputLogViewModel : FulcrumViewModelBase
    {
        #region Custom Events
        #endregion // Custom Events

        #region Fields

        // Helpers for editing Text box contents
        public LogOutputFilteringHelper LogFilteringHelper;
        public InjectorOutputSyntaxHelper InjectorSyntaxHelper;

        // Private backing fields for our public properties
        private bool _hasOutput;
        private string[] _sessionLogs;

        #endregion // Fields

        #region Properties

        // Public properties for the view to bind onto  
        public bool HasOutput { get => _hasOutput; set => PropertyUpdated(value); }
        public string[] SessionLogs { get => _sessionLogs; set => PropertyUpdated(value); }

        #endregion // Properties

        #region Structs and Classes
        #endregion // Structs and Classes

        // ------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Builds a new VM and generates a new logger object for it.
        /// </summary>
        /// <param name="DllOutputUserControl">UserControl which holds the content for the DLL output view</param>
        public FulcrumDllOutputLogViewModel(UserControl DllOutputUserControl) : base(DllOutputUserControl)
        {
            // Spawn a new logger for this view model instance 
            this.ViewModelLogger = new SharpLogger(LoggerActions.UniversalLogger);
            this.ViewModelLogger.WriteLog("SETTING UP INJECTOR TEST VIEW BOUND VALUES NOW...", LogType.WarnLog);
            this.ViewModelLogger.WriteLog($"VIEWMODEL LOGGER FOR VM {this.GetType().Name} HAS BEEN STARTED OK!", LogType.InfoLog);

            // Build default value for session log files.
            this.ViewModelLogger.WriteLog("BUILDING EMPTY ARRAY FOR SESSION LOG FILES NOW...");
            this.SessionLogs = Array.Empty<string>();

            // Build log content helper and return
            this.ViewModelLogger.WriteLog("SETUP NEW DLL INJECTION OUTPUT LOG VALUES OK!");
            this.ViewModelLogger.WriteLog($"VIEW MODEL TYPE {this.GetType().Name} HAS BEEN CONSTRUCTED CORRECTLY!", LogType.InfoLog);
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Searches the AvalonEdit object for text matching what we want.
        /// </summary>
        /// <param name="TextToFind"></param>
        public void SearchForText(string TextToFind)
        {
            // Make sure transformer is built
            if (LogFilteringHelper == null) return;
            this.LogFilteringHelper.SearchForText(TextToFind);
        }
        /// <summary>
        /// Event object to run when the injector output gets new content.
        /// </summary>
        /// <param name="PipeInstance">Pipe object calling these events</param>
        /// <param name="EventArgs">The events themselves.</param>
        public void OnPipeReaderContentProcessed(object PipeInstance, PassThruPipe.PipeDataEventArgs EventArgs)
        {
            // Attach output content into our session log box.
            FulcrumDllOutputLogView ViewCast = this.BaseViewControl as FulcrumDllOutputLogView;
            if (ViewCast == null) this.ViewModelLogger.WriteLog("WARNING: CAST VIEW ENTRY WAS NULL!", LogType.TraceLog); 
            else ViewCast?.Dispatcher.Invoke(() => { ViewCast.DebugRedirectOutputEdit.Text += EventArgs.PipeDataString + "\n"; });
        }
    }
}