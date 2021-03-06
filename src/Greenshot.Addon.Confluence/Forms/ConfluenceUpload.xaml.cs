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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;

#endregion

namespace Greenshot.Addon.Confluence.Forms
{
	/// <summary>
	///     Interaction logic for ConfluenceUpload.xaml
	/// </summary>
	public partial class ConfluenceUpload : Window
	{
	    private readonly ConfluenceConnector _confluenceConnector;
	    private static DateTime _lastLoad = DateTime.Now;
		private static IList<Space> _spaces;

		private System.Windows.Controls.Page _browsePage;
		private System.Windows.Controls.Page _pickerPage;

		private System.Windows.Controls.Page _searchPage;

		private Page _selectedPage;

		public ConfluenceUpload(ConfluenceConnector confluenceConnector, string filename)
		{
		    _confluenceConnector = confluenceConnector;
		    Filename = filename;
			InitializeComponent();
			DataContext = this;
			UpdateSpaces();
			if (PickerPage == null)
			{
				PickerTab.Visibility = Visibility.Collapsed;
				SearchTab.IsSelected = true;
			}
		}

		public System.Windows.Controls.Page PickerPage
		{
			get
			{
				if (_pickerPage == null)
				{
					var pages = ConfluenceUtils.GetCurrentPages(_confluenceConnector);
					if (pages != null && pages.Count > 0)
					{
						_pickerPage = new ConfluencePagePicker(this, pages);
					}
				}
				return _pickerPage;
			}
		}

		public System.Windows.Controls.Page SearchPage
		{
			get
			{
				if (_searchPage == null)
				{
					_searchPage = new ConfluenceSearch(_confluenceConnector, this);
				}
				return _searchPage;
			}
		}

		public System.Windows.Controls.Page BrowsePage
		{
			get
			{
				if (_browsePage == null)
				{
					_browsePage = new ConfluenceTreePicker(_confluenceConnector, this);
				}
				return _browsePage;
			}
		}

		public Page SelectedPage
		{
			get { return _selectedPage; }
			set
			{
				_selectedPage = value;
				if (_selectedPage != null)
				{
					Upload.IsEnabled = true;
				}
				else
				{
					Upload.IsEnabled = false;
				}
				IsOpenPageSelected = false;
			}
		}

		public bool IsOpenPageSelected { get; set; }

		public string Filename { get; set; }

		public IList<Space> Spaces
		{
			get
			{
				UpdateSpaces();
				while (_spaces == null)
				{
					Thread.Sleep(300);
				}
				return _spaces;
			}
		}

		private void UpdateSpaces()
		{
			if (_spaces != null && DateTime.Now.AddMinutes(-60).CompareTo(_lastLoad) > 0)
			{
				// Reset
				_spaces = null;
			}
			// Check if load is needed
			if (_spaces == null)
			{
				new Thread(() =>
				{
					_spaces = _confluenceConnector.GetSpaceSummaries().OrderBy(s => s.Name).ToList();
					_lastLoad = DateTime.Now;
				}) {Name = "Loading spaces for confluence"}.Start();
			}
		}

		private void Upload_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = true;
		}
	}
}