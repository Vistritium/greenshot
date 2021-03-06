﻿#region Greenshot GNU General Public License

// Greenshot - a free and open source screenshot tool
// Copyright (C) 2007-2018 Thomas Braun, Jens Klingen, Robin Krom
// 
// For more information see: http://getgreenshot.org/
// The Greenshot project is hosted on GitHub https://github.com/greenshot/greenshot
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 1 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

#endregion

#region Usings

using System;
using System.ComponentModel.Composition;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Greenshot.Configuration;
using Dapplo.Log;
using Greenshot.Addons.Addons;
using Greenshot.Addons.Controls;
using Greenshot.Addons.Core;
using Greenshot.Addons.Interfaces;
using Greenshot.Addons.Interfaces.Plugin;
using Greenshot.Forms;

#endregion

namespace Greenshot.Destinations
{
    /// <summary>
    ///     Description of FileSaveAsDestination.
    /// </summary>
    [Destination("FileNoDialog", 0)]
    public class FileDestination : AbstractDestination
	{
		private static readonly LogSource Log = new LogSource();
	    private readonly ICoreConfiguration _coreConfiguration;
	    private readonly SettingsForm _settingsForm;

	    [ImportingConstructor]
	    public FileDestination(ICoreConfiguration coreConfiguration, SettingsForm settingsForm)
	    {
	        _coreConfiguration = coreConfiguration;
	        _settingsForm = settingsForm;
	    }

		public override string Description => Language.GetString(LangKey.quicksettings_destination_file);

		public override Keys EditorShortcutKeys => Keys.Control | Keys.S;

		public override Bitmap DisplayIcon => GreenshotResources.GetBitmap("Save.Image");

	    protected override ExportInformation ExportCapture(bool manuallyInitiated, ISurface surface, ICaptureDetails captureDetails)
		{
			var exportInformation = new ExportInformation(Designation, Description);
			bool outputMade;
			bool overwrite;
			string fullPath;
			// Get output settings from the configuration
			var outputSettings = new SurfaceOutputSettings();

			if (captureDetails?.Filename != null)
			{
				// As we save a pre-selected file, allow to overwrite.
				overwrite = true;
				Log.Info().WriteLine("Using previous filename");
				fullPath = captureDetails.Filename;
				outputSettings.Format = ImageOutput.FormatForFilename(fullPath);
			}
			else
			{
				fullPath = CreateNewFilename(captureDetails);
				// As we generate a file, the configuration tells us if we allow to overwrite
				overwrite = _coreConfiguration.OutputFileAllowOverwrite;
			}
			if (_coreConfiguration.OutputFilePromptQuality)
			{
				var qualityDialog = new QualityDialog(outputSettings);
				qualityDialog.ShowDialog();
			}

			// Catching any exception to prevent that the user can't write in the directory.
			// This is done for e.g. bugs #2974608, #2963943, #2816163, #2795317, #2789218, #3004642
			try
			{
				ImageOutput.Save(surface, fullPath, overwrite, outputSettings, _coreConfiguration.OutputFileCopyPathToClipboard);
				outputMade = true;
			}
			catch (ArgumentException ex1)
			{
				// Our generated filename exists, display 'save-as'
				Log.Info().WriteLine("Not overwriting: {0}", ex1.Message);
				// when we don't allow to overwrite present a new SaveWithDialog
				fullPath = ImageOutput.SaveWithDialog(surface, captureDetails);
				outputMade = fullPath != null;
			}
			catch (Exception ex2)
			{
				Log.Error().WriteLine(ex2, "Error saving screenshot!");
				// Show the problem
				MessageBox.Show(Language.GetString(LangKey.error_save), Language.GetString(LangKey.error));
				// when save failed we present a SaveWithDialog
				fullPath = ImageOutput.SaveWithDialog(surface, captureDetails);
				outputMade = fullPath != null;
			}
			// Don't overwrite filename if no output is made
			if (outputMade)
			{
				exportInformation.ExportMade = true;
				exportInformation.Filepath = fullPath;
				if (captureDetails != null)
				{
					captureDetails.Filename = fullPath;
				}
			    _coreConfiguration.OutputFileAsFullpath = fullPath;
			}

			ProcessExport(exportInformation, surface);
			return exportInformation;
		}

		private string CreateNewFilename(ICaptureDetails captureDetails)
		{
			string fullPath;
			Log.Info().WriteLine("Creating new filename");
			var pattern = _coreConfiguration.OutputFileFilenamePattern;
			if (string.IsNullOrEmpty(pattern))
			{
				pattern = "greenshot ${capturetime}";
			}
			var filename = FilenameHelper.GetFilenameFromPattern(pattern, _coreConfiguration.OutputFileFormat, captureDetails);
		    _coreConfiguration.ValidateAndCorrectOutputFilePath();
			var filepath = FilenameHelper.FillVariables(_coreConfiguration.OutputFilePath, false);
			try
			{
				fullPath = Path.Combine(filepath, filename);
			}
			catch (ArgumentException)
			{
				// configured filename or path not valid, show error message...
				Log.Info().WriteLine("Generated path or filename not valid: {0}, {1}", filepath, filename);

				MessageBox.Show(Language.GetString(LangKey.error_save_invalid_chars), Language.GetString(LangKey.error));
				// ... lets get the pattern fixed....
				var dialogResult = _settingsForm.ShowDialog();
				if (dialogResult == DialogResult.OK)
				{
					// ... OK -> then try again:
					fullPath = CreateNewFilename(captureDetails);
				}
				else
				{
					// ... cancelled.
					fullPath = null;
				}
			}
			return fullPath;
		}
	}
}