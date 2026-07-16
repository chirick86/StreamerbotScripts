using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

public class CPHInline
{
    private const string REQUEST_ADD_TO_PLAYLIST_VAR = "spotify.request.add_to_playlist";
    private const string REQUEST_TARGET_PLAYLIST_ID_VAR = "spotify.request.target_playlist_id";
	private const string FORCE_DEVICE_ENABLED_VAR = "spotify.player.force_device_enabled";
	private const string FORCE_DEVICE_ID_VAR = "spotify.player.force_device_id";
	private const string LVOLUME_VAR = "spotify.player.lvolume";

	private class PlaylistOption
	{
		public string Id { get; set; }
		public string Name { get; set; }

		public override string ToString()
		{
			return Name ?? string.Empty;
		}
	}

	private class DeviceOption
	{
		public string Id { get; set; }
		public string Name { get; set; }
		public bool IsActive { get; set; }

		public override string ToString()
		{
			return Name ?? string.Empty;
		}
	}

	public bool Execute()
	{
		return OpenTestForm();
	}

	public bool OpenTestForm()
	{
		try
		{
			CPH.LogInfo("SpotForm: opening settings form");

			string value1 = CPH.GetGlobalVar<string>("spotify.auth.client_id", true) ?? string.Empty;
			string value2 = CPH.GetGlobalVar<string>("spotify.auth.client_secret", true) ?? string.Empty;
			string value3 = CPH.GetGlobalVar<string>("spotify.auth.redirect_uri", true) ?? string.Empty;
			string value4Raw = CPH.GetGlobalVar<string>("spotify.plaer.logarithmic", true);

			bool value4;
			if (string.IsNullOrWhiteSpace(value4Raw))
			{
				value4 = true;
				CPH.SetGlobalVar("spotify.plaer.logarithmic", true, true);
			}
			else if (!bool.TryParse(value4Raw, out value4))
			{
				value4 = true;
				CPH.SetGlobalVar("spotify.plaer.logarithmic", true, true);
			}

			bool saved = false;
			Exception uiThreadError = null;

			Thread uiThread = new Thread(() =>
			{
				try
				{
					using (Form form = BuildForm(
						value1,
						value2,
						value3,
						value4,
						(v1, v2, v3, v4) =>
						{
							value1 = v1;
							value2 = v2;
							value3 = v3;
							value4 = v4;
							saved = true;
						}
					))
					{
						form.ShowDialog();
					}
				}
				catch (Exception ex)
				{
					uiThreadError = ex;
					CPH.LogError("SpotForm UI thread error: " + ex);
				}
			});

			try
			{
				uiThread.SetApartmentState(ApartmentState.STA);
			}
			catch (Exception ex)
			{
				CPH.LogError("SpotForm: failed to set STA apartment state: " + ex.Message);
			}

			uiThread.IsBackground = false;
			uiThread.Start();

			if (!uiThread.Join(TimeSpan.FromMinutes(3)))
			{
				CPH.LogError("SpotForm: form did not close within timeout; aborting action wait");
				return false;
			}

			if (uiThreadError != null)
			{
				CPH.LogError("SpotForm: UI thread failed, form was not opened");
				return false;
			}

			if (!saved)
			{
				CPH.LogInfo("SpotForm: canceled by user");
				return false;
			}

			CPH.SetGlobalVar("spotify.auth.client_id", value1, true);
			CPH.SetGlobalVar("spotify.auth.client_secret", value2, true);
			CPH.SetGlobalVar("spotify.auth.redirect_uri", value3, true);
			CPH.SetGlobalVar("spotify.plaer.logarithmic", value4, true);

			CPH.LogInfo("SpotForm: spotify auth variables saved");
			return true;
		}
		catch (Exception ex)
		{
			CPH.LogError("SpotForm Error: " + ex.Message);
			return false;
		}
	}

	private Form BuildForm(
		string value1,
		string value2,
		string value3,
		bool value4,
		Action<string, string, string, bool> onSave)
	{
		Form form = new Form();
		form.Text = "Input Values";
		form.StartPosition = FormStartPosition.CenterScreen;
		form.TopMost = true;
		form.FormBorderStyle = FormBorderStyle.FixedDialog;
		form.MaximizeBox = false;
		form.MinimizeBox = false;
		form.ClientSize = new Size(900, 600);
		form.AutoScroll = true;
		form.BackColor = Color.FromArgb(32, 32, 34);
		form.ForeColor = Color.White;
		form.Font = new Font(SystemFonts.DefaultFont.FontFamily, 12f);
		form.Shown += (s, e) =>
		{
			form.Activate();
			form.BringToFront();
		};

		int margin = 8;
		int rowGap = 8;
		int labelGap = 8;
		int blockWidth = form.ClientSize.Width - (margin * 2);
		int labelWidth = 184;
		int fieldLeft = margin + labelWidth + labelGap;
		int fieldWidth = form.ClientSize.Width - fieldLeft*2 - margin;
		int labelLeft = margin;
		int buttonLeft = fieldLeft;
		int buttonWidth = fieldWidth;
		int currentTop = margin;
		int titleHeight = 24;
		int buttonHeight = 63;
		int fieldHeight = 24;

		Label infoLabel = new Label
		{
			Text = "1. Follow link below, create app and fill the fields",
			Left = margin,
			Top = currentTop,
			Width = blockWidth,
			TextAlign = ContentAlignment.MiddleCenter,
			Font = new Font(SystemFonts.DefaultFont.FontFamily, 16f)
		};
		currentTop += titleHeight + rowGap;

		Button dashboardButton = new Button
		{
			Text = "Open Developer Dashboard",
			Left = buttonLeft,
			Top = currentTop,
			Width = buttonWidth,
			Height = buttonHeight,
			Font = new Font(SystemFonts.DefaultFont.FontFamily, 12f, FontStyle.Bold),
			TextAlign = ContentAlignment.MiddleCenter
		};
		currentTop += buttonHeight + rowGap;
		dashboardButton.TextAlign = ContentAlignment.MiddleCenter;
		dashboardButton.BackColor = Color.FromArgb(45, 45, 48);
		dashboardButton.ForeColor = Color.White;
		dashboardButton.FlatStyle = FlatStyle.Flat;
		dashboardButton.FlatAppearance.BorderSize = 0;
		dashboardButton.UseVisualStyleBackColor = false;
		dashboardButton.Click += (s, e) =>
		{
			System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://developer.spotify.com/dashboard")
			{
				UseShellExecute = true
			});
		};

		Label label1 = new Label { Text = "client ID:", Left = labelLeft, Top = currentTop, Width = labelWidth, TextAlign = ContentAlignment.MiddleRight, Font = new Font(SystemFonts.DefaultFont.FontFamily, 12f) };
		TextBox box1 = new TextBox { Left = fieldLeft, Top = currentTop, Width = fieldWidth, Height = fieldHeight, Text = value1 };
		label1.ForeColor = Color.White;
		box1.BackColor = Color.FromArgb(45, 45, 48);
		box1.ForeColor = Color.White;
		box1.BorderStyle = BorderStyle.FixedSingle;
		box1.TextAlign = HorizontalAlignment.Center;
		currentTop += fieldHeight + rowGap;

		Label label2 = new Label { Text = "client secret:", Left = labelLeft, Top = currentTop, Width = labelWidth, TextAlign = ContentAlignment.MiddleRight, Font = new Font(SystemFonts.DefaultFont.FontFamily, 12f) };
		TextBox box2 = new TextBox { Left = fieldLeft, Top = currentTop, Width = fieldWidth, Height = fieldHeight, Text = value2 };
		label2.ForeColor = Color.White;
		box2.BackColor = Color.FromArgb(45, 45, 48);
		box2.ForeColor = Color.White;
		box2.BorderStyle = BorderStyle.FixedSingle;
		box2.TextAlign = HorizontalAlignment.Center;
		currentTop += fieldHeight + rowGap;

		Label label3 = new Label { Text = "URI:", Left = labelLeft, Top = currentTop, Width = labelWidth, TextAlign = ContentAlignment.MiddleRight, Font = new Font(SystemFonts.DefaultFont.FontFamily, 12f) };
		TextBox box3 = new TextBox { Left = fieldLeft, Top = currentTop, Width = fieldWidth, Height = fieldHeight, Text = value3 };
		label3.ForeColor = Color.White;
		box3.BackColor = Color.FromArgb(45, 45, 48);
		box3.ForeColor = Color.White;
		box3.BorderStyle = BorderStyle.FixedSingle;
		box3.TextAlign = HorizontalAlignment.Center;
		currentTop += fieldHeight + (rowGap * 2);

		Label step2Label = new Label
		{
			Text = "2. Follow the link below and approve access in the browser",
			Left = margin,
			Top = currentTop,
			Width = blockWidth,
			TextAlign = ContentAlignment.MiddleCenter,
			Font = new Font(SystemFonts.DefaultFont.FontFamily, 16f)
		};
		step2Label.ForeColor = Color.White;
		currentTop += titleHeight + rowGap;

		Button authorizeButton = new Button
		{
			Text = "Open Spotify Authorization",
			Left = buttonLeft,
			Top = currentTop,
			Width = buttonWidth,
			Height = 63,
			Font = new Font(SystemFonts.DefaultFont.FontFamily, 12f, FontStyle.Bold),
			TextAlign = ContentAlignment.MiddleCenter
		};
		currentTop += buttonHeight + rowGap;

		Action reloadDevices = null;
		Action reloadPlaylists = null;

		authorizeButton.BackColor = Color.FromArgb(45, 45, 48);
		authorizeButton.ForeColor = Color.White;
		authorizeButton.FlatStyle = FlatStyle.Flat;
		authorizeButton.FlatAppearance.BorderSize = 0;
		authorizeButton.UseVisualStyleBackColor = false;
		authorizeButton.Click += (s, e) =>
		{
			string clientId = box1.Text ?? string.Empty;
			string clientSecret = box2.Text ?? string.Empty;
			string redirectUri = box3.Text ?? string.Empty;

			if (string.IsNullOrWhiteSpace(clientId) ||
				string.IsNullOrWhiteSpace(clientSecret) ||
				string.IsNullOrWhiteSpace(redirectUri))
			{
				MessageBox.Show(
					"Fill client ID, client secret and URI first.",
					"Missing required values",
					MessageBoxButtons.OK,
					MessageBoxIcon.Warning
				);
				return;
			}

			Button clickedButton = (Button)s;
			clickedButton.Enabled = false;

			Thread authThread = new Thread(() =>
			{
				try
				{
					string scopes =
						"user-read-playback-state " +
						"user-modify-playback-state " +
						"user-read-currently-playing " +
						"playlist-read-private " +
						"playlist-read-collaborative " +
						"playlist-modify-public " +
						"playlist-modify-private";

					string state = Guid.NewGuid().ToString("N");
					string authorizeUrl = BuildSpotifyAuthorizeUrl(clientId, redirectUri, scopes, state);

					System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(authorizeUrl)
					{
						UseShellExecute = true
					});

					string authCode;
					string callbackError;
					if (!TryCaptureAuthorizationCode(redirectUri, state, out authCode, out callbackError))
					{
						form.BeginInvoke(new Action(() =>
						{
							clickedButton.Enabled = true;
							MessageBox.Show(
								callbackError,
								"Authorization error",
								MessageBoxButtons.OK,
								MessageBoxIcon.Error
							);
						}));
						return;
					}

					string accessToken;
					string refreshToken;
					string tokenError;
					if (!TryExchangeAuthorizationCodeForTokens(clientId, clientSecret, redirectUri, authCode, out accessToken, out refreshToken, out tokenError))
					{
						form.BeginInvoke(new Action(() =>
						{
							clickedButton.Enabled = true;
							MessageBox.Show(
								tokenError,
								"Token exchange error",
								MessageBoxButtons.OK,
								MessageBoxIcon.Error
							);
						}));
						return;
					}

					CPH.SetGlobalVar("spotify.auth.access_token", accessToken, false);
					CPH.SetGlobalVar("spotify.auth.refresh_token", refreshToken, true);

					form.BeginInvoke(new Action(() =>
					{
						clickedButton.Text = "Authorized ✓";
						clickedButton.Enabled = true;
						clickedButton.ForeColor = Color.FromArgb(0, 255, 0);
						onSave(
							box1.Text ?? string.Empty,
							box2.Text ?? string.Empty,
							box3.Text ?? string.Empty,
							value4
						);

						CPH.LogInfo("SpotForm: authorization succeeded, reloading devices and playlists");
						if (reloadDevices != null)
						{
							reloadDevices();
						}
						if (reloadPlaylists != null)
						{
							reloadPlaylists();
						}
					}));
				}
				catch (Exception ex)
				{
					form.BeginInvoke(new Action(() =>
					{
						clickedButton.Enabled = true;
						MessageBox.Show(
							"Spotify auth error: " + ex.Message,
							"Error",
							MessageBoxButtons.OK,
							MessageBoxIcon.Error
						);
					}));
				}
			});

			authThread.IsBackground = true;
			authThread.Start();
		};

		Label step3Label = new Label
		{
			Text = "3. Settings",
			Left = margin,
			Top = currentTop,
			Width = blockWidth,
			TextAlign = ContentAlignment.MiddleCenter,
			Font = new Font(SystemFonts.DefaultFont.FontFamily, 16f)
		};
		step3Label.ForeColor = Color.White;
		currentTop += titleHeight + rowGap;

		CheckBox logarithmicVolumeCheckBox = new CheckBox
		{
			Text = "Logarithmic Volume",
			Left = buttonLeft,
			Top = currentTop,
			Width = fieldWidth,
			Checked = value4,
			ForeColor = Color.White,
			BackColor = Color.Transparent,
			TextAlign = ContentAlignment.MiddleLeft,
			Font = new Font(SystemFonts.DefaultFont.FontFamily, 12f)
		};
		currentTop += fieldHeight + (rowGap * 2);

		logarithmicVolumeCheckBox.CheckedChanged += (s, e) =>
		{
			value4 = logarithmicVolumeCheckBox.Checked;
			CPH.SetGlobalVar("spotify.plaer.logarithmic", value4, true);
			SyncLVolumeFromCurrentVolume(value4);
		};

		SyncLVolumeFromCurrentVolume(value4);

		string forceDeviceRaw = CPH.GetGlobalVar<string>(FORCE_DEVICE_ENABLED_VAR, true);
		bool forceDeviceEnabled;
		if (string.IsNullOrWhiteSpace(forceDeviceRaw) || !bool.TryParse(forceDeviceRaw, out forceDeviceEnabled))
		{
			forceDeviceEnabled = false;
			CPH.SetGlobalVar(FORCE_DEVICE_ENABLED_VAR, false, true);
		}

		string forceDeviceId = CPH.GetGlobalVar<string>(FORCE_DEVICE_ID_VAR, true) ?? string.Empty;

		CheckBox forceDeviceCheckBox = new CheckBox
		{
			Text = "Force device",
			Left = buttonLeft,
			Top = currentTop,
			Width = fieldWidth,
			Checked = forceDeviceEnabled,
			ForeColor = Color.White,
			BackColor = Color.Transparent,
			TextAlign = ContentAlignment.MiddleLeft,
			Font = new Font(SystemFonts.DefaultFont.FontFamily, 12f)
		};
		currentTop += fieldHeight + rowGap;

		ComboBox forceDeviceComboBox = new ComboBox
		{
			Left = fieldLeft,
			Top = currentTop,
			Width = fieldWidth,
			DropDownStyle = ComboBoxStyle.DropDownList,
			Enabled = forceDeviceEnabled,
			BackColor = Color.FromArgb(45, 45, 48),
			ForeColor = Color.White,
			FlatStyle = FlatStyle.Flat
		};
		currentTop += fieldHeight + (rowGap * 2);

		forceDeviceCheckBox.CheckedChanged += (s, e) =>
		{
			bool enabled = forceDeviceCheckBox.Checked;
			forceDeviceComboBox.Enabled = enabled;
			CPH.SetGlobalVar(FORCE_DEVICE_ENABLED_VAR, enabled, true);
		};

		forceDeviceComboBox.SelectedIndexChanged += (s, e) =>
		{
			DeviceOption selected = forceDeviceComboBox.SelectedItem as DeviceOption;
			if (selected == null || string.IsNullOrWhiteSpace(selected.Id))
			{
				CPH.SetGlobalVar(FORCE_DEVICE_ID_VAR, string.Empty, true);
				return;
			}

			CPH.SetGlobalVar(FORCE_DEVICE_ID_VAR, selected.Id, true);
		};

		reloadDevices = () =>
		{
			Thread devicesThread = new Thread(() =>
			{
				try
				{
					List<DeviceOption> devices;
					string devicesError;
					if (!TryGetUserDevices(out devices, out devicesError))
					{
						CPH.LogInfo("SpotForm: devices list load skipped: " + devicesError);
						return;
					}

					form.BeginInvoke(new Action(() =>
					{
						string desiredDeviceId = forceDeviceId;
						DeviceOption currentSelected = forceDeviceComboBox.SelectedItem as DeviceOption;
						if (currentSelected != null && !string.IsNullOrWhiteSpace(currentSelected.Id))
						{
							desiredDeviceId = currentSelected.Id;
						}

						forceDeviceComboBox.Items.Clear();
						for (int i = 0; i < devices.Count; i++)
						{
							forceDeviceComboBox.Items.Add(devices[i]);
						}

						if (forceDeviceComboBox.Items.Count == 0)
						{
							forceDeviceComboBox.Enabled = false;
							CPH.SetGlobalVar(FORCE_DEVICE_ID_VAR, string.Empty, true);
							return;
						}

						forceDeviceComboBox.Enabled = forceDeviceCheckBox.Checked;

						int selectedIndex = -1;
						if (!string.IsNullOrWhiteSpace(desiredDeviceId))
						{
							for (int i = 0; i < forceDeviceComboBox.Items.Count; i++)
							{
								DeviceOption option = forceDeviceComboBox.Items[i] as DeviceOption;
								if (option != null && string.Equals(option.Id, desiredDeviceId, StringComparison.OrdinalIgnoreCase))
								{
									selectedIndex = i;
									break;
								}
							}
						}

						if (selectedIndex < 0)
						{
							for (int i = 0; i < forceDeviceComboBox.Items.Count; i++)
							{
								DeviceOption option = forceDeviceComboBox.Items[i] as DeviceOption;
								if (option != null && option.IsActive)
								{
									selectedIndex = i;
									break;
								}
							}
						}

						if (selectedIndex < 0)
						{
							selectedIndex = 0;
						}

						forceDeviceComboBox.SelectedIndex = selectedIndex;
					}));
				}
				catch (Exception ex)
				{
					CPH.LogError("SpotForm device loader error: " + ex.Message);
				}
			});
			devicesThread.IsBackground = true;
			devicesThread.Start();
		};
		reloadDevices();

		string addToPlaylistRaw = CPH.GetGlobalVar<string>(REQUEST_ADD_TO_PLAYLIST_VAR, true);
		bool addToPlaylist;
		if (string.IsNullOrWhiteSpace(addToPlaylistRaw) || !bool.TryParse(addToPlaylistRaw, out addToPlaylist))
		{
			addToPlaylist = false;
			CPH.SetGlobalVar(REQUEST_ADD_TO_PLAYLIST_VAR, false, true);
		}

		string targetPlaylistId = CPH.GetGlobalVar<string>(REQUEST_TARGET_PLAYLIST_ID_VAR, true) ?? string.Empty;

		CheckBox addRequestToPlaylistCheckBox = new CheckBox
		{
			Text = "Forced playlist for requests",
			Left = buttonLeft,
			Top = currentTop,
			Width = fieldWidth,
			Checked = addToPlaylist,
			ForeColor = Color.White,
			BackColor = Color.Transparent,
			TextAlign = ContentAlignment.MiddleLeft,
			Font = new Font(SystemFonts.DefaultFont.FontFamily, 12f)
		};
		currentTop += fieldHeight + rowGap;

		ComboBox requestPlaylistComboBox = new ComboBox
		{
			Left = fieldLeft,
			Top = currentTop,
			Width = fieldWidth,
			DropDownStyle = ComboBoxStyle.DropDownList,
			Enabled = addToPlaylist,
			BackColor = Color.FromArgb(45, 45, 48),
			ForeColor = Color.White,
			FlatStyle = FlatStyle.Flat
		};
		currentTop += fieldHeight + (rowGap * 2);

		addRequestToPlaylistCheckBox.CheckedChanged += (s, e) =>
		{
			bool enabled = addRequestToPlaylistCheckBox.Checked;
			requestPlaylistComboBox.Enabled = enabled;
			CPH.SetGlobalVar(REQUEST_ADD_TO_PLAYLIST_VAR, enabled, true);
		};

		requestPlaylistComboBox.SelectedIndexChanged += (s, e) =>
		{
			PlaylistOption selected = requestPlaylistComboBox.SelectedItem as PlaylistOption;
			if (selected == null || string.IsNullOrWhiteSpace(selected.Id))
			{
				CPH.SetGlobalVar(REQUEST_TARGET_PLAYLIST_ID_VAR, string.Empty, true);
				return;
			}

			CPH.SetGlobalVar(REQUEST_TARGET_PLAYLIST_ID_VAR, selected.Id, true);
		};

		reloadPlaylists = () =>
		{
			Thread playlistsThread = new Thread(() =>
			{
				try
				{
					List<PlaylistOption> playlists;
					string playlistsError;
					if (!TryGetUserPlaylists(out playlists, out playlistsError))
					{
						CPH.LogInfo("SpotForm: playlist list load skipped: " + playlistsError);
						return;
					}

					form.BeginInvoke(new Action(() =>
					{
						string desiredPlaylistId = targetPlaylistId;
						PlaylistOption currentSelected = requestPlaylistComboBox.SelectedItem as PlaylistOption;
						if (currentSelected != null && !string.IsNullOrWhiteSpace(currentSelected.Id))
						{
							desiredPlaylistId = currentSelected.Id;
						}

						requestPlaylistComboBox.Items.Clear();
						foreach (PlaylistOption playlist in playlists)
						{
							requestPlaylistComboBox.Items.Add(playlist);
						}

						if (requestPlaylistComboBox.Items.Count == 0)
						{
							CPH.SetGlobalVar(REQUEST_TARGET_PLAYLIST_ID_VAR, string.Empty, true);
							requestPlaylistComboBox.Enabled = false;
							return;
						}

						requestPlaylistComboBox.Enabled = addRequestToPlaylistCheckBox.Checked;

						int selectedIndex = -1;
						if (!string.IsNullOrWhiteSpace(desiredPlaylistId))
						{
							for (int i = 0; i < requestPlaylistComboBox.Items.Count; i++)
							{
								PlaylistOption option = requestPlaylistComboBox.Items[i] as PlaylistOption;
								if (option == null)
								{
									continue;
								}

								if (string.Equals(option.Id, desiredPlaylistId, StringComparison.OrdinalIgnoreCase))
								{
									selectedIndex = i;
									break;
								}
							}
						}

						if (selectedIndex < 0)
						{
							selectedIndex = 0;
						}

						requestPlaylistComboBox.SelectedIndex = selectedIndex;
					}));
				}
				catch (Exception ex)
				{
					CPH.LogError("SpotForm playlist loader error: " + ex.Message);
				}
			});
			playlistsThread.IsBackground = true;
			playlistsThread.Start();
		};
		reloadPlaylists();

		form.Controls.Add(infoLabel);
		form.Controls.Add(dashboardButton);
		form.Controls.Add(label1);
		form.Controls.Add(box1);
		form.Controls.Add(label2);
		form.Controls.Add(box2);
		form.Controls.Add(label3);
		form.Controls.Add(box3);
		form.Controls.Add(step2Label);
		form.Controls.Add(authorizeButton);
		form.Controls.Add(step3Label);
		form.Controls.Add(addRequestToPlaylistCheckBox);
		form.Controls.Add(requestPlaylistComboBox);
		form.Controls.Add(forceDeviceCheckBox);
		form.Controls.Add(forceDeviceComboBox);
		form.Controls.Add(logarithmicVolumeCheckBox);

		return form;
	}

	private string BuildSpotifyAuthorizeUrl(string clientId, string redirectUri, string scopes, string state)
	{
		return
			"https://accounts.spotify.com/authorize" +
			"?response_type=code" +
			"&client_id=" + Uri.EscapeDataString(clientId) +
			"&redirect_uri=" + Uri.EscapeDataString(redirectUri) +
			"&scope=" + Uri.EscapeDataString(scopes) +
			"&state=" + Uri.EscapeDataString(state) +
			"&show_dialog=true";
	}

	private bool TryCaptureAuthorizationCode(string redirectUri, string expectedState, out string authCode, out string errorMessage)
	{
		authCode = string.Empty;
		errorMessage = string.Empty;

		string prefix = redirectUri.EndsWith("/") ? redirectUri : redirectUri + "/";

		HttpListener listener = new HttpListener();
		listener.Prefixes.Add(prefix);

		try
		{
			listener.Start();
		}
		catch (Exception ex)
		{
			errorMessage = "Spotify auth listener error: " + ex.Message;
			return false;
		}

		try
		{
			IAsyncResult asyncResult = listener.BeginGetContext(null, null);
			if (!asyncResult.AsyncWaitHandle.WaitOne(TimeSpan.FromMinutes(3)))
			{
				errorMessage = "Spotify auth timed out waiting for callback.";
				return false;
			}

			HttpListenerContext context = listener.EndGetContext(asyncResult);
			HttpListenerRequest request = context.Request;

			string returnedCode;
			string returnedState;
			string returnedError;

			if (!TryReadCallbackParameters(request.Url?.Query ?? string.Empty, out returnedCode, out returnedState, out returnedError))
			{
				WriteBrowserResponse(context, "<html><body>Spotify authorization failed. You can close this tab.</body></html>");
				errorMessage = string.IsNullOrWhiteSpace(returnedError)
					? "Spotify authorization failed."
					: returnedError;
				return false;
			}

			if (!string.Equals(returnedState, expectedState, StringComparison.Ordinal))
			{
				WriteBrowserResponse(context, "<html><body>Spotify authorization state mismatch. You can close this tab.</body></html>");
				errorMessage = "Spotify auth state mismatch.";
				return false;
			}

			authCode = returnedCode;
			WriteBrowserResponse(context, "<html><body>Authorization completed. You can close this tab.</body></html>");
			return true;
		}
		catch (Exception ex)
		{
			errorMessage = "Spotify auth callback error: " + ex.Message;
			return false;
		}
		finally
		{
			listener.Stop();
			listener.Close();
		}
	}

	private bool TryReadCallbackParameters(string query, out string code, out string state, out string error)
	{
		code = string.Empty;
		state = string.Empty;
		error = string.Empty;

		if (string.IsNullOrWhiteSpace(query))
		{
			error = "Spotify auth callback did not contain query parameters.";
			return false;
		}

		if (!TryReadQueryValue(query, "code", out code))
		{
			if (TryReadQueryValue(query, "error", out error) && !string.IsNullOrWhiteSpace(error))
			{
				error = "Spotify authorization error: " + error;
				return false;
			}

			error = "Spotify auth callback did not contain code.";
			return false;
		}

		TryReadQueryValue(query, "state", out state);
		return true;
	}

	private bool TryReadQueryValue(string query, string key, out string value)
	{
		value = string.Empty;

		if (string.IsNullOrWhiteSpace(query))
		{
			return false;
		}

		string trimmed = query.TrimStart('?');
		string[] pairs = trimmed.Split('&');

		foreach (string pair in pairs)
		{
			if (string.IsNullOrWhiteSpace(pair))
			{
				continue;
			}

			int equalsIndex = pair.IndexOf('=');
			if (equalsIndex < 0)
			{
				continue;
			}

			string pairKey = Uri.UnescapeDataString(pair.Substring(0, equalsIndex));
			if (!string.Equals(pairKey, key, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			value = Uri.UnescapeDataString(pair.Substring(equalsIndex + 1));
			return true;
		}

		return false;
	}

	private bool TryExchangeAuthorizationCodeForTokens(
		string clientId,
		string clientSecret,
		string redirectUri,
		string authCode,
		out string accessToken,
		out string refreshToken,
		out string errorMessage)
	{
		accessToken = string.Empty;
		refreshToken = string.Empty;
		errorMessage = string.Empty;

		try
		{
			string auth = Convert.ToBase64String(Encoding.ASCII.GetBytes(clientId + ":" + clientSecret));

			string postData =
				"grant_type=authorization_code" +
				"&code=" + Uri.EscapeDataString(authCode) +
				"&redirect_uri=" + Uri.EscapeDataString(redirectUri);

			byte[] data = Encoding.UTF8.GetBytes(postData);

			HttpWebRequest request =
				(HttpWebRequest)WebRequest.Create("https://accounts.spotify.com/api/token");

			request.Method = "POST";
			request.ContentType = "application/x-www-form-urlencoded";
			request.Headers["Authorization"] = "Basic " + auth;
			request.ContentLength = data.Length;

			using (var stream = request.GetRequestStream())
			{
				stream.Write(data, 0, data.Length);
			}

			var response = (HttpWebResponse)request.GetResponse();

			using (var reader = new System.IO.StreamReader(response.GetResponseStream()))
			{
				string json = reader.ReadToEnd();
				JObject obj = JObject.Parse(json);

				accessToken = obj["access_token"]?.ToString() ?? string.Empty;
				refreshToken = obj["refresh_token"]?.ToString() ?? string.Empty;

				if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(refreshToken))
				{
					errorMessage = "Spotify token response did not include both access_token and refresh_token.";
					return false;
				}

				string scope = obj["scope"]?.ToString() ?? string.Empty;
				if (!string.IsNullOrWhiteSpace(scope))
				{
					CPH.LogInfo("Spotify token scope: " + scope);
				}

				return true;
			}
		}
		catch (WebException exWeb)
		{
			try
			{
				using (var reader = new System.IO.StreamReader(exWeb.Response.GetResponseStream()))
				{
					errorMessage = "Spotify token exchange error: " + reader.ReadToEnd();
					return false;
				}
			}
			catch
			{
				errorMessage = "Spotify token exchange error: " + exWeb.Message;
				return false;
			}
		}
		catch (Exception ex)
		{
			errorMessage = "Spotify token exchange error: " + ex.Message;
			return false;
		}
	}

	private void WriteBrowserResponse(HttpListenerContext context, string html)
	{
		byte[] buffer = Encoding.UTF8.GetBytes(html);
		context.Response.ContentType = "text/html; charset=utf-8";
		context.Response.ContentLength64 = buffer.Length;
		context.Response.OutputStream.Write(buffer, 0, buffer.Length);
		context.Response.OutputStream.Close();
	}

	private bool TryGetUserPlaylists(out List<PlaylistOption> playlists, out string errorMessage)
	{
		playlists = new List<PlaylistOption>();
		errorMessage = string.Empty;

		string accessToken = CPH.GetGlobalVar<string>("spotify.auth.access_token", false) ?? string.Empty;
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			errorMessage = "No access token available.";
			CPH.LogError("SpotForm: playlist fetch aborted, access token is missing");
			return false;
		}

		List<string> addedIds = new List<string>();
		string nextUrl = "https://api.spotify.com/v1/me/playlists?limit=50";
		int pageNumber = 1;

		CPH.LogInfo("SpotForm: playlist fetch start");

		while (!string.IsNullOrWhiteSpace(nextUrl))
		{
			CPH.LogInfo("SpotForm: playlist fetch page " + pageNumber + " request => " + nextUrl);

			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(nextUrl);
			request.Method = "GET";
			request.Headers["Authorization"] = "Bearer " + accessToken;

			HttpWebResponse response;
			try
			{
				response = (HttpWebResponse)request.GetResponse();
			}
			catch (WebException exWeb)
			{
				HttpWebResponse errorResponse = exWeb.Response as HttpWebResponse;
				int statusCode = errorResponse != null ? (int)errorResponse.StatusCode : 0;
				string statusText = errorResponse != null ? errorResponse.StatusCode.ToString() : "NoStatus";

				try
				{
					using (var reader = new System.IO.StreamReader(exWeb.Response.GetResponseStream()))
					{
						string responseBody = reader.ReadToEnd();
						string shortBody = TruncateForLog(responseBody, 700);

						errorMessage = "Spotify playlists fetch error: HTTP " + statusCode + " (" + statusText + ") " + shortBody;
						CPH.LogError("SpotForm: playlist fetch page " + pageNumber + " failed. HTTP " + statusCode + " (" + statusText + "), body: " + shortBody);
						return false;
					}
				}
				catch
				{
					errorMessage = "Spotify playlists fetch error: HTTP " + statusCode + " (" + statusText + ") " + exWeb.Message;
					CPH.LogError("SpotForm: playlist fetch page " + pageNumber + " failed without readable body. HTTP " + statusCode + " (" + statusText + "), message: " + exWeb.Message);
					return false;
				}
			}

			CPH.LogInfo("SpotForm: playlist fetch page " + pageNumber + " response <= HTTP " + (int)response.StatusCode + " (" + response.StatusCode + ")");

			using (response)
			using (var reader = new System.IO.StreamReader(response.GetResponseStream()))
			{
				string json = reader.ReadToEnd();
				CPH.LogInfo("SpotForm: playlist fetch page " + pageNumber + " payload chars=" + json.Length);
				JObject root = JObject.Parse(json);
				JArray items = root["items"] as JArray;
				int addedOnPage = 0;

				if (items != null)
				{
					foreach (JToken item in items)
					{
						string id = item["id"]?.ToString() ?? string.Empty;
						string name = item["name"]?.ToString() ?? string.Empty;

						if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
						{
							continue;
						}

						bool alreadyAdded = false;
						for (int i = 0; i < addedIds.Count; i++)
						{
							if (string.Equals(addedIds[i], id, StringComparison.OrdinalIgnoreCase))
							{
								alreadyAdded = true;
								break;
							}
						}

						if (alreadyAdded)
						{
							continue;
						}

						addedIds.Add(id);
						playlists.Add(new PlaylistOption
						{
							Id = id,
							Name = name
						});
						addedOnPage++;
					}
				}

				CPH.LogInfo("SpotForm: playlist fetch page " + pageNumber + " parsed items=" + (items != null ? items.Count : 0) + ", added_unique=" + addedOnPage + ", total_unique=" + playlists.Count);

				nextUrl = root["next"]?.ToString() ?? string.Empty;
				CPH.LogInfo("SpotForm: playlist fetch page " + pageNumber + " next=" + (string.IsNullOrWhiteSpace(nextUrl) ? "<none>" : nextUrl));
				pageNumber++;
			}
		}

		CPH.LogInfo("SpotForm: playlist fetch completed, total playlists=" + playlists.Count);

		return true;
	}

	private bool TryGetUserDevices(out List<DeviceOption> devices, out string errorMessage)
	{
		devices = new List<DeviceOption>();
		errorMessage = string.Empty;

		string accessToken = CPH.GetGlobalVar<string>("spotify.auth.access_token", false) ?? string.Empty;
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			errorMessage = "No access token available.";
			CPH.LogError("SpotForm: devices fetch aborted, access token is missing");
			return false;
		}

		CPH.LogInfo("SpotForm: devices fetch request => https://api.spotify.com/v1/me/player/devices");

		HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://api.spotify.com/v1/me/player/devices");
		request.Method = "GET";
		request.Headers["Authorization"] = "Bearer " + accessToken;

		HttpWebResponse response;
		try
		{
			response = (HttpWebResponse)request.GetResponse();
		}
		catch (WebException exWeb)
		{
			HttpWebResponse errorResponse = exWeb.Response as HttpWebResponse;
			int statusCode = errorResponse != null ? (int)errorResponse.StatusCode : 0;
			string statusText = errorResponse != null ? errorResponse.StatusCode.ToString() : "NoStatus";

			try
			{
				using (var reader = new System.IO.StreamReader(exWeb.Response.GetResponseStream()))
				{
					string responseBody = reader.ReadToEnd();
					string shortBody = TruncateForLog(responseBody, 700);

					errorMessage = "Spotify devices fetch error: HTTP " + statusCode + " (" + statusText + ") " + shortBody;
					CPH.LogError("SpotForm: devices fetch failed. HTTP " + statusCode + " (" + statusText + "), body: " + shortBody);
					return false;
				}
			}
			catch
			{
				errorMessage = "Spotify devices fetch error: HTTP " + statusCode + " (" + statusText + ") " + exWeb.Message;
				CPH.LogError("SpotForm: devices fetch failed without readable body. HTTP " + statusCode + " (" + statusText + "), message: " + exWeb.Message);
				return false;
			}
		}

		CPH.LogInfo("SpotForm: devices fetch response <= HTTP " + (int)response.StatusCode + " (" + response.StatusCode + ")");

		using (response)
		using (var reader = new System.IO.StreamReader(response.GetResponseStream()))
		{
			string json = reader.ReadToEnd();
			CPH.LogInfo("SpotForm: devices fetch payload chars=" + json.Length);
			JObject root = JObject.Parse(json);
			JArray items = root["devices"] as JArray;

			if (items == null)
			{
				CPH.LogInfo("SpotForm: devices fetch returned no devices array");
				return true;
			}

			for (int i = 0; i < items.Count; i++)
			{
				JToken item = items[i];
				string id = item["id"]?.ToString() ?? string.Empty;
				string name = item["name"]?.ToString() ?? string.Empty;
				bool isActive = (bool?)item["is_active"] ?? false;
				bool isRestricted = (bool?)item["is_restricted"] ?? false;

				if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
				{
					continue;
				}

				string label = name;
				if (isActive)
				{
					label += " (Active)";
				}
				else if (isRestricted)
				{
					label += " (Restricted)";
				}

				devices.Add(new DeviceOption
				{
					Id = id,
					Name = label,
					IsActive = isActive
				});
			}

			CPH.LogInfo("SpotForm: devices fetch completed, total devices=" + devices.Count);
		}

		return true;
	}

	private string TruncateForLog(string value, int maxLength)
	{
		if (string.IsNullOrEmpty(value))
		{
			return string.Empty;
		}

		string normalized = value.Replace("\r", " ").Replace("\n", " ").Trim();
		if (normalized.Length <= maxLength)
		{
			return normalized;
		}

		return normalized.Substring(0, maxLength) + "...";
	}

	private void SyncLVolumeFromCurrentVolume(bool useLogarithmic)
	{
		int currentVolume = CPH.GetGlobalVar<int>("spotify.player.volume", false);
		if (currentVolume < 0) currentVolume = 0;
		if (currentVolume > 100) currentVolume = 100;

		int lvolume = currentVolume;
		if (useLogarithmic)
		{
			double normalized = currentVolume / 100.0;
			if (normalized <= 0.0)
			{
				lvolume = 0;
			}
			else
			{
				lvolume = (int)Math.Round(Math.Pow(normalized, 2.2) * 100.0);
				if (lvolume < 0) lvolume = 0;
				if (lvolume > 100) lvolume = 100;
			}
		}

		CPH.SetGlobalVar(LVOLUME_VAR, lvolume, false);
	}
}
