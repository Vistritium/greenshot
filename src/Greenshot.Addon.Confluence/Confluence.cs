#region Greenshot GNU General Public License

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
using System.ComponentModel.Composition;
using System.Windows.Forms;
using Dapplo.Log;
using Greenshot.Addon.Confluence.Web_References.confluence;
using Greenshot.Addons.Core;
using Greenshot.Addons.Core.Credentials;

#endregion

namespace Greenshot.Addon.Confluence
{
	/// <summary>
	///     For details see the Confluence API site
	///     See: http://confluence.atlassian.com/display/CONFDEV/Remote+API+Specification
	/// </summary>
	[Export]
	public class ConfluenceConnector : IDisposable
	{
		private const string AuthFailedExceptionName = "com.atlassian.confluence.rpc.AuthenticationFailedException";
		private const string V2Failed = "AXIS";
		private static readonly LogSource Log = new LogSource();
		private readonly IConfluenceConfiguration _confluenceConfig;
		private readonly Cache<string, RemotePage> _pageCache;
		private ConfluenceSoapServiceService _confluence;
		private string _credentials;
		private DateTime _loggedInTime = DateTime.Now;
		private string _url;

	    public const string DEFAULT_POSTFIX1 = "/rpc/soap-axis/confluenceservice-v1?wsdl";
	    public const string DEFAULT_POSTFIX2 = "/rpc/soap-axis/confluenceservice-v2?wsdl";
	    public const string DEFAULT_PREFIX = "http://";
	    private const string DEFAULT_URL = DEFAULT_PREFIX + "confluence";

        [ImportingConstructor]
        public ConfluenceConnector(IConfluenceConfiguration confluenceConfiguration)
        {
            _confluenceConfig = confluenceConfiguration;
            _pageCache = new Cache<string, RemotePage>(60 * _confluenceConfig.Timeout);
        }

		public bool IsLoggedIn { get; private set; }

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected void Dispose(bool disposing)
		{
			if (_confluence != null)
			{
				Logout();
			}
			if (disposing)
			{
				if (_confluence != null)
				{
					_confluence.Dispose();
					_confluence = null;
				}
			}
		}

		private void Init(string url)
		{
			_url = url;
			_confluence = new ConfluenceSoapServiceService
			{
				Url = url,
				Proxy = NetworkHelper.CreateProxy(new Uri(url))
			};
		}

		~ConfluenceConnector()
		{
			Dispose(false);
		}

		/// <summary>
		///     Internal login which catches the exceptions
		/// </summary>
		/// <returns>true if login was done sucessfully</returns>
		private bool DoLogin(string user, string password)
		{
			try
			{
				_credentials = _confluence.login(user, password);
				_loggedInTime = DateTime.Now;
				IsLoggedIn = true;
			}
			catch (Exception e)
			{
				// Check if confluence-v2 caused an error, use v1 instead
				if (e.Message.Contains(V2Failed) && _url.Contains("v2"))
				{
					Init(_url.Replace("v2", "v1"));
					return DoLogin(user, password);
				}
				// check if auth failed
				if (e.Message.Contains(AuthFailedExceptionName))
				{
					return false;
				}
				// Not an authentication issue
				IsLoggedIn = false;
				_credentials = null;
				e.Data.Add("user", user);
				e.Data.Add("url", _url);
				throw;
			}
			return true;
		}

		public void Login()
		{
			Logout();
			try
			{
				// Get the system name, so the user knows where to login to
				var systemName = _url.Replace(DEFAULT_POSTFIX1, "");
				systemName = systemName.Replace(DEFAULT_POSTFIX2, "");
				var dialog = new CredentialsDialog(systemName)
				{
					Name = null
				};
				while (dialog.Show(null, dialog.Name) == DialogResult.OK)
				{
					if (DoLogin(dialog.Name, dialog.Password))
					{
						if (dialog.SaveChecked)
						{
							dialog.Confirm(true);
						}
						return;
					}
					try
					{
						dialog.Confirm(false);
					}
					catch (ApplicationException e)
					{
						// exception handling ...
						Log.Error().WriteLine(e, "Problem using the credentials dialog");
					}
					// For every windows version after XP show an incorrect password baloon
					dialog.IncorrectPassword = true;
					// Make sure the dialog is display, the password was false!
					dialog.AlwaysDisplay = true;
				}
			}
			catch (ApplicationException e)
			{
				// exception handling ...
				Log.Error().WriteLine(e, "Problem using the credentials dialog");
			}
		}

		public void Logout()
		{
			if (_credentials != null)
			{
				_confluence.logout(_credentials);
				_credentials = null;
				IsLoggedIn = false;
			}
		}

		private void CheckCredentials()
		{
			if (IsLoggedIn)
			{
				if (_loggedInTime.AddMinutes(_confluenceConfig.Timeout - 1).CompareTo(DateTime.Now) < 0)
				{
					Logout();
					Login();
				}
			}
			else
			{
				Login();
			}
		}

		public void AddAttachment(long pageId, string mime, string comment, string filename, IBinaryContainer image)
		{
			CheckCredentials();
			// Comment is ignored, see: http://jira.atlassian.com/browse/CONF-9395
			var attachment = new RemoteAttachment
			{
				comment = comment,
				fileName = filename,
				contentType = mime
			};
			_confluence.addAttachment(_credentials, pageId, attachment, image.ToByteArray());
		}

		public Page GetPage(string spaceKey, string pageTitle)
		{
			RemotePage page = null;
			var cacheKey = spaceKey + pageTitle;
			if (_pageCache.Contains(cacheKey))
			{
				page = _pageCache[cacheKey];
			}
			if (page == null)
			{
				CheckCredentials();
				page = _confluence.getPage(_credentials, spaceKey, pageTitle);
				_pageCache.Add(cacheKey, page);
			}
			return new Page(page);
		}

		public Page GetPage(long pageId)
		{
			RemotePage page = null;
			var cacheKey = "" + pageId;

			if (_pageCache.Contains(cacheKey))
			{
				page = _pageCache[cacheKey];
			}
			if (page == null)
			{
				CheckCredentials();
				page = _confluence.getPage(_credentials, pageId);
				_pageCache.Add(cacheKey, page);
			}
			return new Page(page);
		}

		public Page GetSpaceHomepage(Space spaceSummary)
		{
			CheckCredentials();
			var spaceDetail = _confluence.getSpace(_credentials, spaceSummary.Key);
			var page = _confluence.getPage(_credentials, spaceDetail.homePage);
			return new Page(page);
		}

		public IEnumerable<Space> GetSpaceSummaries()
		{
			CheckCredentials();
			var spaces = _confluence.getSpaces(_credentials);
			foreach (var space in spaces)
			{
				yield return new Space(space);
			}
		}

		public IEnumerable<Page> GetPageChildren(Page parentPage)
		{
			CheckCredentials();
			var pages = _confluence.getChildren(_credentials, parentPage.Id);
			foreach (var page in pages)
			{
				yield return new Page(page);
			}
		}

		public IEnumerable<Page> GetPageSummaries(Space space)
		{
			CheckCredentials();
			var pages = _confluence.getPages(_credentials, space.Key);
			foreach (var page in pages)
			{
				yield return new Page(page);
			}
		}

		public IEnumerable<Page> SearchPages(string query, string space)
		{
			CheckCredentials();
			foreach (var searchResult in _confluence.search(_credentials, query, 20))
			{
				Log.Debug().WriteLine("Got result of type {0}", searchResult.type);
				if ("page".Equals(searchResult.type))
				{
					yield return new Page(searchResult, space);
				}
			}
		}
	}
}