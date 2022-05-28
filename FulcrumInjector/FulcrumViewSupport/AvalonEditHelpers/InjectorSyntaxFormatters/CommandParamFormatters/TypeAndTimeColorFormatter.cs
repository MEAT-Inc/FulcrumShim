﻿using System.Text.RegularExpressions;
using FulcrumInjector.FulcrumViewContent.Models.PassThruModels;
using ICSharpCode.AvalonEdit.Document;

namespace FulcrumInjector.FulcrumViewSupport.AvalonEditHelpers.InjectorSyntaxFormatters.CommandParamFormatters
{
    /// <summary>
    /// Colors the stacktrace on the log line
    /// </summary>
    public class TypeAndTimeColorFormatter : InjectorDocFormatterBase
    {
        /// <summary>
        /// Builds a new color format helping object.
        /// </summary>
        public TypeAndTimeColorFormatter(OutputFormatHelperBase FormatBase) : base(FormatBase) { }

        // --------------------------------------------------------------------------------------------------

        /// <summary>
        /// Color our line output for the stack trace
        /// </summary>
        /// <param name="InputLine"></param>
        protected override void ColorizeLine(DocumentLine InputLine)
        {
            // Find the command type for our input object here. If none, drop out
            Regex TimeMatchRegex = new(PassThruRegexModelShare.PassThruTime.ExpressionPattern);
            Match MatchesFound = TimeMatchRegex.Match(CurrentContext.Document.GetText(InputLine));

            // Now run our coloring definitions and return out.
            if (!MatchesFound.Success) return;
            this.ColorNewMatches(InputLine, MatchesFound);
        }
    }
}