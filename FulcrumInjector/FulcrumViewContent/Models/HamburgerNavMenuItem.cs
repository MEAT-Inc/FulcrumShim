﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MahApps.Metro.Controls;

namespace FulcrumInjector.FulcrumViewContent.Models
{
    /// <summary>
    /// Dependency property object for navigation menu types
    /// </summary>
    public class HamburgerNavMenuItem : HamburgerMenuGlyphItem
    {
        // Sets if we can navigate on this object or not.
        public bool IsNavigation => this.NavUserControlType != null && this.NavViewModelType != null;

        // Destination property based on Type
        public static readonly DependencyProperty NavUserControlTypeProperty = DependencyProperty.Register(
            nameof(NavUserControlType), typeof(Type), typeof(HamburgerNavMenuItem), new PropertyMetadata(default(Type)));

        // Destination property based on Type
        public static readonly DependencyProperty NavViewModelTypeProperty = DependencyProperty.Register(
            nameof(NavViewModelType), typeof(Type), typeof(HamburgerNavMenuItem), new PropertyMetadata(default(Type)));

        // --------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Publicly controlled type for destination
        /// </summary>
        public Type NavUserControlType
        {
            get => (Type)this.GetValue(NavUserControlTypeProperty);
            set => this.SetValue(NavUserControlTypeProperty, value);
        }
        /// <summary>
        /// Publicly controlled type for destination view model
        /// </summary>
        public Type NavViewModelType
        {
            get => (Type)this.GetValue(NavViewModelTypeProperty);
            set => this.SetValue(NavViewModelTypeProperty, value);
        }
    }
}