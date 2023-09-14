﻿using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FulcrumInjector.FulcrumViewContent.FulcrumViewModels;
using FulcrumInjector.FulcrumViewContent.FulcrumViewModels.InjectorMiscViewModels;
using SharpLogging;

namespace FulcrumInjector.FulcrumViewContent.FulcrumViews.InjectorMiscViews
{
    /// <summary>
    /// Interaction logic for FulcrumGoogleDriveView.xaml
    /// </summary>
    public partial class FulcrumGoogleDriveView : UserControl
    {
        #region Custom Events
        #endregion // Custom Events

        #region Fields

        // Logger instance for this view content
        private readonly SharpLogger _viewLogger;

        #endregion // Fields

        #region Properties

        // ViewModel object to bind onto
        internal FulcrumGoogleDriveViewModel ViewModel { get; set; }

        #endregion // Properties

        #region Structs and Classes
        #endregion // Structs and Classes

        // ------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Builds a new view object instance for our simulation playback
        /// </summary>
        public FulcrumGoogleDriveView()
        {
            // Spawn a new logger and setup our view model
            this._viewLogger = new SharpLogger(LoggerActions.UniversalLogger);
            this.ViewModel = FulcrumConstants.FulcrumGoogleDriveViewModel ?? new FulcrumGoogleDriveViewModel(this);

            // Initialize new UI Component
            InitializeComponent();

            // Setup our data context and log information out
            this.DataContext = this.ViewModel;
            this._viewLogger.WriteLog("CONFIGURED VIEW CONTROL VALUES FOR THE GOOGLE DRIVE VIEW OK!", LogType.InfoLog);
            this._viewLogger.WriteLog($"BUILT NEW INSTANCE FOR VIEW TYPE {this.GetType().Name} OK!", LogType.InfoLog);
        }
        /// <summary>
        /// On loaded, we want to setup our new viewmodel object and populate values
        /// </summary>
        /// <param name="sender">Sending object</param>
        /// <param name="e">Events attached to it.</param>
        private void FulcrumGoogleDriveView_OnLoaded(object sender, RoutedEventArgs e)
        {
            // Hook in a new event for the button click on the check for updates title button
            FulcrumConstants.FulcrumTitleView.btnGoogleDrive.Click += this.ToggleGoogleDriveFlyout_OnClick;
            this._viewLogger.WriteLog("HOOKED IN A NEW EVENT FOR THE ABOUT THIS APP BUTTON ON OUR TITLE VIEW!", LogType.InfoLog);

            // Invoke a background refresh for pulling in all log files for our explorer
            this.RefreshGoogleDrive_OnClick(this.btnRefreshInjectorFiles, null);
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Button click event for the google drive icon. This will trigger our google drive view.
        /// </summary>
        /// <param name="Sender"></param>
        /// <param name="E"></param>
        private void ToggleGoogleDriveFlyout_OnClick(object Sender, RoutedEventArgs E)
        {
            // Log processed and show if we have to.
            this._viewLogger.WriteLog("PROCESSED BUTTON CLICK FOR THE GOOGLE DRIVE ICON CORRECTLY!", LogType.WarnLog);
            if (FulcrumConstants.FulcrumMainWindow?.GoogleDriveFlyout == null) { _viewLogger.WriteLog("ERROR! GOOGLE DRIVE FLYOUT IS NULL!", LogType.ErrorLog); }
            else
            {
                // Toggle the information pane
                bool IsOpen = FulcrumConstants.FulcrumMainWindow.GoogleDriveFlyout.IsOpen;
                FulcrumConstants.FulcrumMainWindow.GoogleDriveFlyout.IsOpen = !IsOpen;
                this._viewLogger.WriteLog("PROCESSED VIEW TOGGLE REQUEST FOR GOOGLE DRIVE FLYOUT OK!", LogType.InfoLog);
            }
        }
        /// <summary>
        /// Button click event for the refresh button on this flyout. Refreshes all contents of the google drive and stores it back on the view
        /// </summary>
        /// <param name="Sender"></param>
        /// <param name="E"></param>
        private void RefreshGoogleDrive_OnClick(object Sender, RoutedEventArgs E)
        {
            // Disable the sending button and open the refreshing flyout 
            if (Sender is not Button SendingButton) return;
            this.GoogleDriveRefreshingFlyout.IsOpen = true;
            SendingButton.IsEnabled = false;

            // Disable the filtering ComboBoxes and text boxes
            this.tbVinFilter.IsEnabled = false;
            this.cbYearFilter.IsEnabled = false; 
            this.cbMakeFilter.IsEnabled = false; 
            this.cbModelFilter.IsEnabled = false;

            // Background refresh all files from the drive here
            this._viewLogger.WriteLog("REFRESHING INJECTOR LOG FILE SETS IN THE BACKGROUND NOW...", LogType.InfoLog);
            Task.Run(() =>
            {
                // Wrap this routine in a try catch to avoid bombing/hanging the UI
                try
                {
                    // Run the refresh routine. Enable the sending button once the refresh is complete
                    var RefreshResult = this.ViewModel.LocateInjectorLogFiles(out _);

                    // Throw an exception if the refresh routine fails
                    if (!RefreshResult) throw new InvalidOperationException("Error! Failed to load any files in from the Injector Google Drive!");
                    this._viewLogger.WriteLog("REFRESHED INJECTOR LOG SETS CORRECTLY! RESULTS ARE STORED ON OUR VIEW MODEL FOR DOWNLOADING!", LogType.InfoLog);
                }
                catch (Exception RefreshEx)
                {
                    // Log out the exception and move on
                    this._viewLogger.WriteLog("ERROR! FAILED TO REFRESH LOG FILES IN THE INJECTOR DRIVE!", LogType.ErrorLog);
                    this._viewLogger.WriteException("EXCEPTION IS BEING LOGGED BELOW", RefreshEx);
                }

                // Reset our UI contents here and close the refreshing progress bar
                this.Dispatcher.Invoke(() =>
                {
                    // Close the refreshing flyout and enable the sending button
                    this.GoogleDriveRefreshingFlyout.IsOpen = false;
                    SendingButton.IsEnabled = true;

                    // Enable the filtering ComboBoxes and text boxes
                    this.tbVinFilter.IsEnabled = true;
                    this.cbYearFilter.IsEnabled = true;
                    this.cbMakeFilter.IsEnabled = true;
                    this.cbModelFilter.IsEnabled = true;
                });
            });
        }
        /// <summary>
        /// Event handler used to process a selection changed event on any of the filtering combo boxes for the log set list
        /// </summary>
        /// <param name="Sender"></param>
        /// <param name="E"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void cbLogSetFilter_OnSelectionChanged(object Sender, SelectionChangedEventArgs E)
        {
            // Store the sending ComboBox and pull the selected filter value
            if (Sender is not ComboBox SendingComboBox) return;

            // Pull the value from it and clear it out if it's a default
            string FilterValue = SendingComboBox.SelectedItem.ToString();
            if (FilterValue.Contains("--")) FilterValue = string.Empty;

            // Find the filter type based on the name of the combo box provided
            FulcrumGoogleDriveViewModel.FilterTypes FilterType = SendingComboBox.Name switch
            {
                // Switch on the name of the sending object. Store the filter matching our name
                nameof(this.cbYearFilter) => FulcrumGoogleDriveViewModel.FilterTypes.YEAR_FILTER,
                nameof(this.cbMakeFilter) => FulcrumGoogleDriveViewModel.FilterTypes.MAKE_FILTER,
                nameof(this.cbModelFilter) => FulcrumGoogleDriveViewModel.FilterTypes.MODEL_FILTER,
                _ => throw new InvalidOperationException("Error! Can not determine filter type from sending ComboBox!")
            };

            // Apply the filter on the view model now
            this.ViewModel.ApplyLogSetFilter(FilterType, FilterValue);
        }
        /// <summary>
        /// Event handler used to process a text changed event on the VIN filtering text box for the log set list
        /// </summary>
        /// <param name="Sender"></param>
        /// <param name="E"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void tbVinFilter_OnTextChanged(object Sender, TextChangedEventArgs E)
        {
            // Store the sending TextBox and pull the selected filter value
            if (Sender is not TextBox SendingTextBox) return;

            // Pull the value from it and clear it out if it's a default
            string FilterValue = SendingTextBox.Text;
            this.ViewModel.ApplyLogSetFilter(FulcrumGoogleDriveViewModel.FilterTypes.VIN_FILTER, FilterValue);
        }
    }
}
