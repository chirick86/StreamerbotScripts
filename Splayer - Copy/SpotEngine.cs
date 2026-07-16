using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json.Linq;

public class CPHInline
{
    // =========================================
    // GLOBAL VARIABLE NAMES
    // =========================================

    private const string ACCESS_TOKEN_VAR = "spotify.auth.access_token";
    private const string DEFAULT_DEVICE_VAR = "spotify.player.default_device_id";
    private const string LAST_DEVICE_VAR = "spotify.player.last_device_id";
    private const string FORCE_DEVICE_ENABLED_VAR = "spotify.player.force_device_enabled";
    private const string FORCE_DEVICE_ID_VAR = "spotify.player.force_device_id";
    private const string VOLUME_VAR = "spotify.player.volume";
    private const string REQUEST_ADD_TO_PLAYLIST_VAR = "spotify.request.add_to_playlist";
    private const string REQUEST_TARGET_PLAYLIST_ID_VAR = "spotify.request.target_playlist_id";
    private const string CURRENT_PLAYLIST_ID = "CURRENT_PLAYLIST";
    private const string SEARCH_TRACK_VAR = "search.track";
    private const string SEARCH_QUERY_VAR = "spotify.search.query";

    // =========================================
    // HTTP CLIENT
    // =========================================

    private HttpClient CreateClient()
    {
        string token = CPH.GetGlobalVar<string>(ACCESS_TOKEN_VAR, false);

        HttpClient client = new HttpClient();

        client.DefaultRequestHeaders.Add(
            "Authorization",
            "Bearer " + token
        );

        return client;
    }

    private string ReadResponseBody(HttpResponseMessage response)
    {
        if (response == null || response.Content == null)
        {
            return string.Empty;
        }

        return response
            .Content
            .ReadAsStringAsync()
            .Result ?? string.Empty;
    }

    private bool TrySendWithAuthRetry(
        string context,
        Func<HttpClient, HttpResponseMessage> send,
        out HttpResponseMessage response,
        out string json)
    {
        response = null;
        json = string.Empty;

        try
        {
            HttpClient client = CreateClient();

            response = send(client);
            json = ReadResponseBody(response);

            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                return true;
            }

            CPH.LogInfo("Spotify " + context + ": HTTP 401, refreshing token and retrying");
            if (!RefreshAccessToken())
            {
                CPH.LogError("Spotify " + context + ": token refresh failed");
                return false;
            }

            client = CreateClient();
            response = send(client);
            json = ReadResponseBody(response);

            return true;
        }
        catch (Exception ex)
        {
            CPH.LogError("Spotify " + context + " request error: " + ex.Message);
            return false;
        }
    }

    private bool TryGetPlayerData(HttpClient client, string context, out JObject data)
    {
        data = null;

        HttpResponseMessage response;
        string json;
        if (!TrySendWithAuthRetry(
            context,
            c => c.GetAsync("https://api.spotify.com/v1/me/player").Result,
            out response,
            out json))
        {
            return false;
        }

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            CPH.LogInfo("Spotify " + context + ": no active device/playback (HTTP 204)");
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            CPH.LogError(
                "Spotify " + context + " HTTP " +
                ((int)response.StatusCode) + " " + response.StatusCode +
                " | body: " + (string.IsNullOrWhiteSpace(json) ? "<empty>" : json)
            );
            return false;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            CPH.LogError("Spotify " + context + ": empty response body");
            return false;
        }

        try
        {
            data = JObject.Parse(json);
            return true;
        }
        catch (Exception ex)
        {
            CPH.LogError(
                "Spotify " + context + " JSON parse error: " + ex.Message +
                " | body: " + json
            );
            return false;
        }
    }

    private bool TryGetDevices(HttpClient client, string context, out JArray devices)
    {
        devices = null;

        HttpResponseMessage response;
        string json;
        if (!TrySendWithAuthRetry(
            context + " devices",
            c => c.GetAsync("https://api.spotify.com/v1/me/player/devices").Result,
            out response,
            out json))
        {
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            CPH.LogError(
                "Spotify " + context + " devices HTTP " +
                ((int)response.StatusCode) + " " + response.StatusCode +
                " | body: " + (string.IsNullOrWhiteSpace(json) ? "<empty>" : json)
            );
            return false;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            CPH.LogError("Spotify " + context + " devices: empty response body");
            return false;
        }

        try
        {
            JObject obj = JObject.Parse(json);
            devices = obj["devices"] as JArray;
            return devices != null;
        }
        catch (Exception ex)
        {
            CPH.LogError(
                "Spotify " + context + " devices JSON parse error: " + ex.Message +
                " | body: " + json
            );
            return false;
        }
    }

    private string GetPreferredDeviceId(HttpClient client)
    {
        string defaultDeviceId = CPH.GetGlobalVar<string>(DEFAULT_DEVICE_VAR, true);
        if (!string.IsNullOrWhiteSpace(defaultDeviceId))
        {
            return defaultDeviceId;
        }

        string lastDeviceId = CPH.GetGlobalVar<string>(LAST_DEVICE_VAR, true);
        if (!string.IsNullOrWhiteSpace(lastDeviceId))
        {
            return lastDeviceId;
        }

        JArray devices;
        if (!TryGetDevices(client, "GetPreferredDeviceId", out devices))
        {
            return string.Empty;
        }

        foreach (JToken d in devices)
        {
            bool isActive = (bool?)d["is_active"] ?? false;
            if (isActive)
            {
                return d["id"]?.ToString() ?? string.Empty;
            }
        }

        foreach (JToken d in devices)
        {
            bool isRestricted = (bool?)d["is_restricted"] ?? false;
            if (!isRestricted)
            {
                return d["id"]?.ToString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private string GetForcedPlayDeviceId()
    {
        bool forceEnabled = CPH.GetGlobalVar<bool>(FORCE_DEVICE_ENABLED_VAR, true);
        if (!forceEnabled)
        {
            return string.Empty;
        }

        string forceDeviceId = CPH.GetGlobalVar<string>(FORCE_DEVICE_ID_VAR, true) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(forceDeviceId))
        {
            CPH.LogInfo("Spotify force device is enabled but no device id is configured");
            return string.Empty;
        }

        return forceDeviceId;
    }

    private int ClampVolume(int value)
    {
        if (value < 0) return 0;
        if (value > 100) return 100;
        return value;
    }

    private int ToLinearVolumeFromLog(int logVolume)
    {
        double normalized = ClampVolume(logVolume) / 100.0;
        return ClampVolume((int)Math.Round(Math.Pow(normalized, 1.0 / 2.2) * 100.0));
    }

    private int ToLogVolumeFromLinear(int linearVolume)
    {
        double normalized = ClampVolume(linearVolume) / 100.0;
        if (normalized <= 0.0)
        {
            return 0;
        }

        return ClampVolume((int)Math.Round(Math.Pow(normalized, 2.2) * 100.0));
    }

    private bool TransferPlaybackToDevice(HttpClient client, string deviceId, bool play)
    {
        string body = "{\"device_ids\":[\"" + deviceId + "\"],\"play\":" + (play ? "true" : "false") + "}";
        HttpResponseMessage response;
        string json;
        if (!TrySendWithAuthRetry(
            "transfer playback",
            c =>
            {
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                return c.PutAsync("https://api.spotify.com/v1/me/player", content).Result;
            },
            out response,
            out json))
        {
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            CPH.LogError(
                "Spotify transfer playback HTTP " +
                ((int)response.StatusCode) + " " + response.StatusCode +
                " | body: " + (string.IsNullOrWhiteSpace(json) ? "<empty>" : json)
            );
            return false;
        }

        GetStatus();
        CPH.SetGlobalVar(LAST_DEVICE_VAR, deviceId, true);
        return true;
    }

    private bool TryGetCurrentPlaylistId(HttpClient client, out string playlistId)
    {
        playlistId = string.Empty;

        JObject data;
        if (!TryGetPlayerData(client, "TryGetCurrentPlaylistId", out data))
        {
            return false;
        }

        string contextType = data["context"]?["type"]?.ToString() ?? string.Empty;
        string contextUri = data["context"]?["uri"]?.ToString() ?? string.Empty;

        if (!string.Equals(contextType, "playlist", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        const string playlistPrefix = "spotify:playlist:";
        if (!contextUri.StartsWith(playlistPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        playlistId = contextUri.Substring(playlistPrefix.Length);
        return !string.IsNullOrWhiteSpace(playlistId);
    }

    private bool TryGetPlaylistName(HttpClient client, string playlistId, out string playlistName)
    {
        playlistName = string.Empty;

        if (string.IsNullOrWhiteSpace(playlistId))
        {
            return false;
        }

        HttpResponseMessage response;
        string json;
        if (!TrySendWithAuthRetry(
            "playlist details",
            c => c.GetAsync("https://api.spotify.com/v1/playlists/" + Uri.EscapeDataString(playlistId)).Result,
            out response,
            out json))
        {
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            CPH.LogError(
                "Spotify playlist details HTTP " +
                ((int)response.StatusCode) + " " + response.StatusCode +
                " | body: " + (string.IsNullOrWhiteSpace(json) ? "<empty>" : json)
            );
            return false;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            CPH.LogError("Spotify playlist details: empty response body");
            return false;
        }

        try
        {
            JObject obj = JObject.Parse(json);
            playlistName = obj["name"]?.ToString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(playlistName);
        }
        catch (Exception ex)
        {
            CPH.LogError(
                "Spotify playlist details JSON parse error: " + ex.Message +
                " | body: " + json
            );
            return false;
        }
    }

    private bool AddTrackToPlaylist(HttpClient client, string playlistId, string trackUri)
    {
        string playlistName = CPH.GetGlobalVar<string>("spotify.playlist.name", false) ?? string.Empty;

        CPH.LogInfo(
            "Spotify add-to-playlist target | playlistId: " + playlistId +
            " | playlistName: " + (string.IsNullOrWhiteSpace(playlistName) ? "<unknown>" : playlistName) +
            " | trackUri: " + trackUri
        );

        string url = "https://api.spotify.com/v1/playlists/" + Uri.EscapeDataString(playlistId) + "/items";
        string body = "{\"uris\":[\"" + trackUri + "\"]}";
        HttpResponseMessage response;
        string json;
        if (!TrySendWithAuthRetry(
            "add-to-playlist",
            c =>
            {
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                return c.PostAsync(url, content).Result;
            },
            out response,
            out json))
        {
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            CPH.LogError(
                "Spotify add-to-playlist HTTP " +
                ((int)response.StatusCode) + " " + response.StatusCode +
                " | body: " + (string.IsNullOrWhiteSpace(json) ? "<empty>" : json)
            );
            return false;
        }

        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                JObject obj = JObject.Parse(json);
                string snapshotId = obj["snapshot_id"]?.ToString() ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(snapshotId))
                {
                    CPH.LogInfo("Spotify add-to-playlist snapshot_id: " + snapshotId);
                }
            }
            catch (Exception ex)
            {
                CPH.LogError("Spotify add-to-playlist response parse error: " + ex.Message + " | body: " + json);
            }
        }

        return true;
    }

    // =========================================
    // GET STATUS
    // =========================================

    public bool GetStatus()
    {
        try
        {
            System.Threading.Thread.Sleep(1000);

            HttpClient client = CreateClient();

            JObject data;
            if (!TryGetPlayerData(client, "GetStatus", out data))
            {
                CPH.SetGlobalVar("spotify.player.status", "NO_ACTIVE_DEVICE", false);
                CPH.SetGlobalVar("spotify.player.track", "", false);
                CPH.SetGlobalVar("spotify.player.artist", "", false);
                CPH.SetGlobalVar("spotify.player.svolume", 0, false);
                CPH.SetGlobalVar(VOLUME_VAR, 0, false);
                CPH.SetGlobalVar("spotify.player.device", "", false);
                CPH.SetGlobalVar("spotify.playlist.id", "", false);
                CPH.SetGlobalVar("spotify.playlist.name", "", false);
                return false;
            }

            bool isPlaying = (bool)data["is_playing"];

            string status = isPlaying
                ? "PLAYING"
                : "PAUSED";

            string track =
                data["item"]?["name"]?.ToString() ?? "";

            string artist =
                data["item"]?["artists"]?[0]?["name"]?.ToString() ?? "";

            int volume =
                (int?)data["device"]?["volume_percent"] ?? 0;
            volume = ClampVolume(volume);

            bool useLogarithmic = CPH.GetGlobalVar<bool>("spotify.plaer.logarithmic", true);
            int lvolume = useLogarithmic
                ? ToLogVolumeFromLinear(volume)
                : volume;

            string device =
                data["device"]?["name"]?.ToString() ?? "";

            string deviceId =
                data["device"]?["id"]?.ToString() ?? "";

            string playlistId = string.Empty;
            string playlistName = string.Empty;

            if (TryGetCurrentPlaylistId(client, out playlistId))
            {
                TryGetPlaylistName(client, playlistId, out playlistName);
            }

            // =========================================
            // SAVE RUNTIME STATE
            // =========================================

            CPH.SetGlobalVar(
                "spotify.player.status",
                status,
                false
            );

            CPH.SetGlobalVar(
                "spotify.player.track",
                track,
                false
            );

            CPH.SetGlobalVar(
                "spotify.player.artist",
                artist,
                false
            );

            CPH.SetGlobalVar(
                "spotify.player.svolume",
                volume,
                false
            );

            CPH.SetGlobalVar(
                VOLUME_VAR,
                lvolume,
                false
            );

            CPH.SetGlobalVar(
                "spotify.player.device",
                device,
                false
            );

            CPH.SetGlobalVar(
                "spotify.playlist.id",
                playlistId,
                false
            );

            CPH.SetGlobalVar(
                "spotify.playlist.name",
                playlistName,
                false
            );

            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                CPH.SetGlobalVar(LAST_DEVICE_VAR, deviceId, true);
            }

            CPH.LogInfo(
                $"Spotify Updated | {artist} - {track} | {status}"
            );

            return true;
        }
        catch(Exception ex)
        {
            CPH.LogError(
                "Spotify GetStatus Error: " + ex.Message
            );

            return false;
        }
    }

    // =========================================
    // PLAY ON LAST / DEFAULT DEVICE
    // =========================================

    public bool PlayOnLastDevice()
    {
        try
        {
            HttpClient client = CreateClient();

            string deviceId = GetForcedPlayDeviceId();
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                deviceId = GetPreferredDeviceId(client);
            }

            if (string.IsNullOrWhiteSpace(deviceId))
            {
                CPH.LogError("Spotify PlayOnLastDevice: no available device");
                return false;
            }

            if (!TransferPlaybackToDevice(client, deviceId, true))
            {
                return false;
            }

            GetStatus();

            CPH.LogInfo("Spotify PlayOnLastDevice executed");
            return true;
        }
        catch (Exception ex)
        {
            CPH.LogError("Spotify PlayOnLastDevice Error: " + ex.Message);
            return false;
        }
    }

    public bool SetDefaultToLastDevice()
    {
        try
        {
            string lastDeviceId = CPH.GetGlobalVar<string>(LAST_DEVICE_VAR, true);
            if (string.IsNullOrWhiteSpace(lastDeviceId))
            {
                CPH.LogError("Spotify SetDefaultToLastDevice: no last device id");
                return false;
            }

            CPH.SetGlobalVar(DEFAULT_DEVICE_VAR, lastDeviceId, true);
            CPH.LogInfo("Spotify default device saved: " + lastDeviceId);
            return true;
        }
        catch (Exception ex)
        {
            CPH.LogError("Spotify SetDefaultToLastDevice Error: " + ex.Message);
            return false;
        }
    }

    // =========================================
    // PLAY / PAUSE TOGGLE
    // =========================================

    public bool TogglePlay()
    {
        try
        {
            bool targetIsPlay = true; // If live state is unavailable, prefer PLAY to initialize playback.
            string activeDeviceId = string.Empty;
            HttpClient client = CreateClient();

            JObject stateData;
            if (TryGetPlayerData(client, "TogglePlay state check", out stateData))
            {
                bool isPlaying = (bool?)stateData["is_playing"] ?? false;
                targetIsPlay = !isPlaying;

                activeDeviceId = stateData["device"]?["id"]?.ToString() ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(activeDeviceId))
                {
                    CPH.SetGlobalVar(LAST_DEVICE_VAR, activeDeviceId, true);
                }
            }
            else
            {
                CPH.LogInfo("Spotify TogglePlay: live state unavailable, fallback target=" + (targetIsPlay ? "PLAY" : "PAUSE"));
            }

            string forcedPlayDeviceId = GetForcedPlayDeviceId();
            if (targetIsPlay && !string.IsNullOrWhiteSpace(forcedPlayDeviceId))
            {
                if (!TransferPlaybackToDevice(client, forcedPlayDeviceId, true))
                {
                    CPH.LogError("Spotify TogglePlay: failed to force playback device " + forcedPlayDeviceId);
                    return false;
                }

                activeDeviceId = forcedPlayDeviceId;
            }
            else if (targetIsPlay && string.IsNullOrWhiteSpace(activeDeviceId))
            {
                string preferredDeviceId = GetPreferredDeviceId(client);
                if (!string.IsNullOrWhiteSpace(preferredDeviceId))
                {
                    if (!TransferPlaybackToDevice(client, preferredDeviceId, true))
                    {
                        CPH.LogError("Spotify TogglePlay: failed to activate preferred device " + preferredDeviceId);
                        return false;
                    }

                    activeDeviceId = preferredDeviceId;
                }
                else
                {
                    CPH.LogInfo("Spotify TogglePlay: no preferred device found, trying play without device_id");
                }
            }

            string url = targetIsPlay
                ? "https://api.spotify.com/v1/me/player/play"
                : "https://api.spotify.com/v1/me/player/pause";

            if (!string.IsNullOrWhiteSpace(activeDeviceId))
            {
                url += "?device_id=" + Uri.EscapeDataString(activeDeviceId);
            }

            CPH.LogInfo(
                "Spotify TogglePlay request" +
                " | target=" + (targetIsPlay ? "PLAY" : "PAUSE") +
                " | url=" + url
            );

            HttpResponseMessage toggleResponse;
            string toggleJson;
            if (!TrySendWithAuthRetry(
                "TogglePlay",
                c => c.PutAsync(url, null).Result,
                out toggleResponse,
                out toggleJson))
            {
                return false;
            }

            if (!toggleResponse.IsSuccessStatusCode)
            {
                CPH.LogError(
                    "Spotify TogglePlay HTTP " +
                    ((int)toggleResponse.StatusCode) + " " + toggleResponse.StatusCode +
                    " | body: " + (string.IsNullOrWhiteSpace(toggleJson) ? "<empty>" : toggleJson)
                );
                return false;
            }

            GetStatus();

            CPH.LogInfo("Spotify TogglePlay executed");

            return true;
        }
        catch(Exception ex)
        {
            CPH.LogError(
                "Spotify TogglePlay Error: " + ex.Message
            );

            return false;
        }
    }

    // =========================================
    // NEXT SONG
    // =========================================

    public bool NextSong()
    {
        try
        {
            HttpResponseMessage response;
            string json;
            if (!TrySendWithAuthRetry(
                "NextSong",
                c => c.PostAsync("https://api.spotify.com/v1/me/player/next", null).Result,
                out response,
                out json))
            {
                return false;
            }

            if (!response.IsSuccessStatusCode)
            {
                CPH.LogError(
                    "Spotify NextSong HTTP " +
                    ((int)response.StatusCode) + " " + response.StatusCode +
                    " | body: " + (string.IsNullOrWhiteSpace(json) ? "<empty>" : json)
                );
                return false;
            }

            GetStatus();

            CPH.LogInfo("Spotify NextSong executed");

            return true;
        }
        catch(Exception ex)
        {
            CPH.LogError(
                "Spotify NextSong Error: " + ex.Message
            );

            return false;
        }
    }

    // =========================================
    // VOLUME
    // =========================================

    public bool Volume()
    {
        try
        {
            int lvolume = CPH.GetGlobalVar<int>(VOLUME_VAR, false);
            if (lvolume == 0)
            {
                // Backward compatibility if callers still write spotify.player.svolume.
                int fallbackVolume = CPH.GetGlobalVar<int>("spotify.player.svolume", false);
                if (fallbackVolume != 0)
                {
                    lvolume = fallbackVolume;
                }
            }
            lvolume = ClampVolume(lvolume);

            CPH.SetGlobalVar(VOLUME_VAR, lvolume, false);

            bool useLogarithmic = CPH.GetGlobalVar<bool>("spotify.plaer.logarithmic", true);

            int volumeToSend = lvolume;
            if (useLogarithmic)
            {
                volumeToSend = ToLinearVolumeFromLog(lvolume);
            }

            CPH.SetGlobalVar("spotify.player.svolume", volumeToSend, false);

            CPH.LogInfo(
                "Spotify Volume mode: " +
                (useLogarithmic ? "LOGARITHMIC" : "LINEAR") +
                " | linput=" + lvolume +
                " | output=" + volumeToSend
            );

            string url = "https://api.spotify.com/v1/me/player/volume?volume_percent=" + volumeToSend;

            HttpResponseMessage response;
            string json;
            if (!TrySendWithAuthRetry(
                "Volume",
                c => c.PutAsync(url, null).Result,
                out response,
                out json))
            {
                return false;
            }

            if (!response.IsSuccessStatusCode)
            {
                CPH.LogError(
                    "Spotify Volume HTTP " +
                    ((int)response.StatusCode) + " " + response.StatusCode +
                    " | body: " + (string.IsNullOrWhiteSpace(json) ? "<empty>" : json)
                );
                return false;
            }

            if (useLogarithmic)
            {
                CPH.LogInfo(
                    "Spotify Volume set: log=" + lvolume +
                    "% -> linear=" + volumeToSend + "%"
                );
            }
            else
            {
                CPH.LogInfo("Spotify Volume set to " + volumeToSend + "%");
            }

            return true;
        }
        catch (Exception ex)
        {
            CPH.LogError("Spotify Volume Error: " + ex.Message);
            return false;
        }
    }

    // =========================================
    // SEARCH (search.track -> spotify.search.query)
    // =========================================

    public bool SearchFirstTrack()
    {
        try
        {
            string query = CPH.GetGlobalVar<string>(SEARCH_TRACK_VAR, false) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(query))
            {
                CPH.LogError("Spotify SearchFirstTrack: search.track is empty");
                return false;
            }

            string searchUrl =
                "https://api.spotify.com/v1/search?type=track&limit=1&q=" +
                Uri.EscapeDataString(query);

            HttpResponseMessage searchResponse;
            string searchJson;
            if (!TrySendWithAuthRetry(
                "SearchFirstTrack",
                c => c.GetAsync(searchUrl).Result,
                out searchResponse,
                out searchJson))
            {
                return false;
            }

            if (!searchResponse.IsSuccessStatusCode)
            {
                CPH.LogError(
                    "Spotify search HTTP " +
                    ((int)searchResponse.StatusCode) + " " + searchResponse.StatusCode +
                    " | body: " + (string.IsNullOrWhiteSpace(searchJson) ? "<empty>" : searchJson)
                );
                return false;
            }

            JObject root = JObject.Parse(searchJson);
            JToken firstTrack = root["tracks"]?["items"]?[0];
            if (firstTrack == null)
            {
                CPH.LogInfo("Spotify SearchFirstTrack: no tracks found for query: " + query);
                return false;
            }

            string uri = firstTrack["uri"]?.ToString() ?? string.Empty;
            string trackName = firstTrack["name"]?.ToString() ?? "";
            string artistName = firstTrack["artists"]?[0]?["name"]?.ToString() ?? "";
            CPH.SetArgument("artist", artistName);
            CPH.SetArgument("track", trackName);

            if (string.IsNullOrWhiteSpace(uri))
            {
                CPH.LogError("Spotify SearchFirstTrack: track uri is empty");
                return false;
            }

            CPH.SetGlobalVar(SEARCH_QUERY_VAR, uri, false);
            CPH.LogInfo("Spotify found and saved to spotify.search.query: " + artistName + " - " + trackName);
            return true;
        }
        catch (Exception ex)
        {
            CPH.LogError("Spotify SearchFirstTrack Error: " + ex.Message);
            return false;
        }
    }

    // =========================================
    // ADD TO QUEUE (spotify.search.query)
    // =========================================

    public bool AddSearchTrackToQueue()
    {
        try
        {
            string trackUri = CPH.GetGlobalVar<string>(SEARCH_QUERY_VAR, false) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trackUri))
            {
                CPH.LogError("Spotify AddSearchTrackToQueue: spotify.search.query is empty");
                return false;
            }

            string queueUrl =
                "https://api.spotify.com/v1/me/player/queue?uri=" +
                Uri.EscapeDataString(trackUri);

            HttpResponseMessage queueResponse;
            string queueJson;
            if (!TrySendWithAuthRetry(
                "AddSearchTrackToQueue",
                c => c.PostAsync(queueUrl, null).Result,
                out queueResponse,
                out queueJson))
            {
                return false;
            }

            if (!queueResponse.IsSuccessStatusCode)
            {
                CPH.LogError(
                    "Spotify queue HTTP " +
                    ((int)queueResponse.StatusCode) + " " + queueResponse.StatusCode +
                    " | body: " + (string.IsNullOrWhiteSpace(queueJson) ? "<empty>" : queueJson)
                );
                return false;
            }

            CPH.LogInfo("Spotify queued from spotify.search.query: " + trackUri);
            return true;
        }
        catch (Exception ex)
        {
            CPH.LogError("Spotify AddSearchTrackToQueue Error: " + ex.Message);
            return false;
        }
    }

    // =========================================
    // ADD TO CURRENT PLAYLIST (spotify.search.query)
    // =========================================

    public bool AddSearchTrackToPlaylist()
    {
        try
        {
            HttpClient client = CreateClient();

            string trackUri = CPH.GetGlobalVar<string>(SEARCH_QUERY_VAR, false) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trackUri))
            {
                CPH.LogError("Spotify AddSearchTrackToPlaylist: spotify.search.query is empty");
                return false;
            }

            bool useForcedPlaylist = CPH.GetGlobalVar<bool>(REQUEST_ADD_TO_PLAYLIST_VAR, true);
            string configuredPlaylistId = CPH.GetGlobalVar<string>(REQUEST_TARGET_PLAYLIST_ID_VAR, true) ?? string.Empty;

            string playlistId = string.Empty;
            bool useConfiguredPlaylist =
                useForcedPlaylist &&
                !string.IsNullOrWhiteSpace(configuredPlaylistId) &&
                !string.Equals(configuredPlaylistId, CURRENT_PLAYLIST_ID, StringComparison.OrdinalIgnoreCase);

            if (useConfiguredPlaylist)
            {
                playlistId = configuredPlaylistId;
            }
            else if (!TryGetCurrentPlaylistId(client, out playlistId))
            {
                CPH.LogError("Spotify AddSearchTrackToPlaylist: current context is not a playlist");
                return false;
            }

            string playlistName = string.Empty;
            if (!TryGetPlaylistName(client, playlistId, out playlistName))
            {
                playlistName = CPH.GetGlobalVar<string>("spotify.playlist.name", false) ?? string.Empty;
            }

            CPH.LogInfo(
                "Spotify AddSearchTrackToPlaylist resolved playlist | playlistId: " + playlistId +
                " | playlistName: " + (string.IsNullOrWhiteSpace(playlistName) ? "<unknown>" : playlistName) +
                " | source: " + (useConfiguredPlaylist ? "FORCED" : "CURRENT") +
                " | trackUri: " + trackUri
            );

            if (!AddTrackToPlaylist(client, playlistId, trackUri))
            {
                return false;
            }

            CPH.LogInfo("Spotify added from spotify.search.query to playlist: " + trackUri);
            return true;
        }
        catch (Exception ex)
        {
            CPH.LogError("Spotify AddSearchTrackToPlaylist Error: " + ex.Message);
            return false;
        }
    }

    // =========================================
    // Refresh Access Token
    // =========================================

    public bool RefreshAccessToken()
    {
        try
        {
            string clientId = CPH.GetGlobalVar<string>("spotify.auth.client_id", true);
            string clientSecret = CPH.GetGlobalVar<string>("spotify.auth.client_secret", true);
            string refreshToken = CPH.GetGlobalVar<string>("spotify.auth.refresh_token", true);

            string auth = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($"{clientId}:{clientSecret}")
            );

            string postData =
                "grant_type=refresh_token" +
                "&refresh_token=" + Uri.EscapeDataString(refreshToken);

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

                string accessToken = obj["access_token"]?.ToString();

                if (string.IsNullOrEmpty(accessToken))
                {
                    CPH.LogError("No access token in response: " + json);
                    return false;
                }

                CPH.SetGlobalVar("spotify.auth.access_token", accessToken, false);

                CPH.LogInfo("Spotify token refreshed OK");
                return true;
            }
        }
        catch (Exception ex)
        {
            CPH.LogError("Refresh error: " + ex.ToString());
            return false;
        }
    }    

}