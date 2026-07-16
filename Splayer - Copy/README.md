# Splayer

Spotify helper scripts for Streamer.bot. It provides Spotify authorization, playback control, device selection, playlist selection, and simple overlay/widget support.

## Installation

1. Copy this folder into your Streamer.bot scripts location.
2. Open Streamer.bot and add the scripts to your actions.
3. Run the setup form from `SpotForm.cs`.
4. Enter your Spotify app `Client ID`, `Client Secret`, and `Redirect URI`.
5. Click the Spotify authorization button and confirm access.

## Quick Setup

1. Create a Spotify app in the Spotify Developer Dashboard.
2. Use the same redirect URI in Spotify and in the form.
3. Authorize once so Streamer.bot can save the access and refresh tokens.
4. Open the settings form again to load devices and playlists.

## Usage

- Use the control actions to play/pause, skip, and change volume.
- Enable forced device if you want Spotify to play on a specific device.
- Enable forced playlist if you want requests to go to a specific playlist.
- Use the HTML files if you want the on-stream widget/control display.

## Files

- `SpotEngine.cs` - Spotify playback and API logic.
- `SpotForm.cs` - authorization and settings UI.
- `SplayerControl.html` - web control panel.
- `SplayerWidget.html` - on-stream widget.

## Notes

- The first authorization may be needed before playlists and devices can load.
- If playlists do not appear, re-authorize after changing scopes in Spotify.
