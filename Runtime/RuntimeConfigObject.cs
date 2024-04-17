using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace Hey.Services.RemoteConfig
{
    /// <summary>
    /// This class represents a single runtime settings configuration. Access its methods and properties via the <c>ConfigManager.appConfig</c> wrapper.
    /// </summary>
    public class RuntimeConfigObject
    {
        private string url;
        private string secretKey;
        internal string cacheFile;
        internal string configType;
        internal ConfigResponse ConfigResponse;
        internal ConfigRequestStatus RequestStatus;

        JObject _config;
        JsonSerializerSettings rawDateSettings = new JsonSerializerSettings { DateParseHandling = DateParseHandling.None };

        /// <summary>
        /// Returns a copy of the entire config as a JObject.
        /// </summary>
        public JObject config => (JObject)_config.DeepClone();

        /// <summary>
        /// This event fires when the config is successfully returned from the Remote Config backend.
        /// </summary>
        /// <returns>
        /// A ConfigResponse struct representing the response.
        /// </returns>
        public event Action<ConfigResponse> FetchCompleted;
        public RuntimeConfigObject(string configType, string secretKey, string url)
        {
            this.configType = configType;
            this.secretKey = secretKey;
            this.url = url;
            RequestStatus = ConfigRequestStatus.None;
            _config = new JObject();
            ConfigResponse = new ConfigResponse();
        }

        public async Task FetchAsync(Action<ConfigResponse> callback)
        {
            cacheFile = url.Split('/')[url.Split('/').Length - 1].Split('.')[0] + ".cached";
            FetchCompleted += callback;
            try
            {
                using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
                {
                    var request = await webRequest.SendWebRequest().AsTask();
                    string text = request.downloadHandler.text;

                    if (!string.IsNullOrEmpty(secretKey) && secretKey != "none") 
                    {
                        try { text = EncryptionHelper.Decrypt(secretKey, text); }
                        catch { Debug.LogError("Invalid encryption key or data."); }
                    }

                    if (webRequest.result != UnityWebRequest.Result.Success)
                    {
                        string cachefilepath = Path.Combine(Application.persistentDataPath, cacheFile);
                        ConfigResponse configResponse = LoadCache(secretKey, cachefilepath);
                        HandleConfigResponse(configResponse);
                        //
                        if (configResponse.status == ConfigRequestStatus.Failed)
                        {
                            RequestStatus = ConfigRequestStatus.Failed;
                            ConfigResponse.status = RequestStatus;
                            Debug.LogError($"Error fetching remote config: {webRequest.error}");
                        }
                    }
                    else
                    {
                        SaveCache(cacheFile, text);
                        var configResponse = ParseResponse(ConfigOrigin.Remote, request.GetResponseHeaders(), text);
                        HandleConfigResponse(configResponse);
                    }
                }
            }
            catch (Exception e)
            {
                RequestStatus = ConfigRequestStatus.Failed;
                ConfigResponse.status = RequestStatus;
                HandleConfigResponse(ConfigResponse);
                Debug.LogException(e);
            }
        }
        
        internal void HandleConfigResponse(ConfigResponse configResponse)
        {
            ConfigResponse = configResponse;
            RequestStatus = ConfigResponse.status;
            var responseBody = ConfigResponse.body;

            if(configResponse.status == ConfigRequestStatus.Success)
            {
                if (responseBody["type"].ToString() != configType) 
                {
                    Debug.LogError("Error: Config type mismatch.");
                    return;
                }
                _config = responseBody;
            }

            FetchCompleted?.Invoke(ConfigResponse);
        }

        internal List<Func<Dictionary<string, string>, string, bool>> rawResponseValidators = new List<Func<Dictionary<string, string>, string, bool>>();
        internal ConfigResponse ParseResponse(ConfigOrigin origin, Dictionary<string, string> headers, string body)
        {
            var configResponse = new ConfigResponse
            {
                requestOrigin = origin,
                headers = headers
            };
            if (body == null || headers == null)
            {
                configResponse.status = ConfigRequestStatus.Failed;
                return configResponse;
            }
            foreach (var validationFunc in rawResponseValidators)
            {
                if (validationFunc(headers, body) == false)
                {
                    configResponse.status = ConfigRequestStatus.Failed;
                    return configResponse;
                }
            }
            try
            {
                var responseJObj = JObject.Parse(body);
                configResponse.body = responseJObj;
                configResponse.status = ConfigRequestStatus.Success;
            }
            catch (Exception e)
            {
                Debug.LogWarning("config response is not valid JSON:\n" + configResponse.body + "\n" + e);
                configResponse.status = ConfigRequestStatus.Failed;
            }

            return configResponse;
        }
        
        // Cache
        /// <summary>
        /// Caches all configs previously fetched, called whenever FetchConfigs completes.
        /// </summary>
        /// <param name="response">the ConfigResponse resulting from the FetchConfigs call</param>
        public void SaveCache(string cacheFile, string data)
        {
            try
            {
                using (var writer = File.CreateText(Path.Combine(Application.persistentDataPath, cacheFile)))
                {
                    writer.Write(data);
                }
            }
            catch (Exception e) { Debug.LogError(e); }
        }

        public ConfigResponse LoadCache(string secretKey, string cachefile_path)
        {
            if (!File.Exists(cachefile_path)) return ParseResponse(ConfigOrigin.Default, new(), null);
            string cached_data = File.ReadAllText(cachefile_path);

            if (!string.IsNullOrEmpty(secretKey) && secretKey != "none") 
            {
                try { cached_data = EncryptionHelper.Decrypt(secretKey, cached_data);}
                catch {  Debug.LogError("Invalid encryption key or data."); }
            }

            return ParseResponse(ConfigOrigin.Cached, new(), cached_data);
        }

        public JToken GetValue(string key) => _config["value"].Where(x => x["key"].Value<string>() == key).FirstOrDefault();
        /// <summary>
        /// Retrieves the boolean value of a corresponding key, if one exists.
        /// </summary>
        /// <param name="key">The key identifying the corresponding setting.</param>
        /// <param name="defaultValue">The default value to use if the specified key cannot be found or is unavailable.</param>
        /// <returns>A bool representation of the key from the remote service, if one exists. If one does not exist, the defaultValue is returned (false if none is supplied.)</returns>
        public bool GetBool(string key, bool defaultValue = false)
        {
            try
            {
                var value = GetValue(key);
                return value["value"].Value<bool>();
            }
            catch
            {
                Debug.LogError("Error: " + key + " type doesn't match or not found in remote config.");
                return defaultValue;
            }
        }

        /// <summary>
        /// Retrieves the float value of a corresponding key from the remote service, if one exists.
        /// </summary>
        /// <param name="key">The key identifying the corresponding setting.</param>
        /// <param name="defaultValue">The default value to use if the specified key cannot be found or is unavailable.</param>
        /// <returns>A float representation of the key from the remote service, if one exists. If one does not exist, the defaultValue is returned (0.0F if none is supplied.)</returns>
        public float GetFloat(string key, float defaultValue = 0.0F)
        {
            try
            {
                var value = GetValue(key);
                return value["value"].Value<float>();
            }
            catch
            {
                Debug.LogError("Error: " + key + " type doesn't match or not found in remote config.");
                return defaultValue;
            }
        }

        /// <summary>
        /// Retrieves the int value of a corresponding key, if one exists.
        /// </summary>
        /// <param name="key">The key identifying the corresponding setting.</param>
        /// <param name="defaultValue">The default value to use if the specified key cannot be found or is unavailable.</param>
        /// <returns>An int representation of the key from the remote service, if one exists. If one does not exist, the defaultValue is returned (0 if none is supplied.)</returns>
        public int GetInt(string key, int defaultValue = 0)
        {
            try
            {
                var value = GetValue(key);
                return value["value"].Value<int>();
            }
            catch
            {
                Debug.LogError("Error: " + key + " type doesn't match or not found in remote config.");
                return defaultValue;
            }
        }

        /// <summary>
        /// Retrieves the string value of a corresponding key from the remote service, if one exists.
        /// </summary>
        /// <param name="key">The key identifying the corresponding setting.</param>
        /// <param name="defaultValue">The default value to use if the specified key cannot be found or is unavailable.</param>
        /// <returns>A string representation of the key from the remote service, if one exists. If one does not exist, the defaultValue is returned ("" if none is supplied.)</returns>
        public string GetString(string key, string defaultValue = "")
        {
            try
            {
                var value = GetValue(key);
                return value["value"].Value<string>();
            }
            catch
            {
                Debug.LogError("Error: " + key + " type doesn't match or not found in remote config.");
                return defaultValue;
            }
        }

        /// <summary>
        /// Retrieves the long value of a corresponding key from the remote service, if one exists.
        /// </summary>
        /// <param name="key">The key identifying the corresponding setting.</param>
        /// <param name="defaultValue">The default value to use if the specified key cannot be found or is unavailable.</param>
        /// <returns>A long representation of the key from the remote service, if one exists. If one does not exist, the defaultValue is returned (0L if none is supplied.)</returns>
        public long GetLong(string key, long defaultValue = 0L)
        {
            try
            {
                var value = GetValue(key);
                return value["value"].Value<long>();
            }
            catch
            {
                Debug.LogError("Error: " + key + " type doesn't match or not found in remote config.");
                return defaultValue;
            }
        }

        /// <summary>
        /// Checks if a corresponding key exists in your remote settings.
        /// </summary>
        /// <returns><c>true</c>, if the key exists, or <c>false</c> if it doesn't.</returns>
        /// <param name="key">The key to search for.</param>
        public bool HasKey(string key)
        {
            if(_config == null)
            {
                return false;
            }
            return _config[key] == null ? false : true;
        }

        /// <summary>
        /// Returns all keys in your remote settings, as an array.
        /// </summary>
        /// <returns>An array of properties within config, if one exists. If one does not exist, empty string array is supplied.</returns>
        public string[] GetKeys()
        {
            try
            {
                return _config.Properties().Select(prop => prop.Name).ToArray<string>();
            }
            catch
            {
                Debug.LogError("Error: No keys found in remote config.");
                return new string[0];
            }
        }

        /// <summary>
        /// Retrieves the string representation of the JSON value of a corresponding key from the remote service, if one exists.
        /// </summary>
        /// <param name="key">The key identifying the corresponding setting.</param>
        /// <param name="defaultValue">The default value to use if the specified key cannot be found or is unavailable.</param>
        /// <returns>A string representation of the JSON value of a corresponding key from the remote service, if one exists. If one does not exist, the defaultValue is returned ("{}" if none is supplied.)</returns>
        public string GetJson(string key, string defaultValue = "{}")
        {
            try
            {
                var value = GetValue(key);
                return value["value"].ToString();
            }
            catch
            {
                Debug.LogError("Error: " + key + " type doesn't match or not found in remote config.");
                return defaultValue;
            }
        }
    }
    public static class UnityWebRequestExtensions
    {
        public static Task<UnityWebRequest> AsTask(this UnityWebRequestAsyncOperation asyncOperation)
        {
            var tcs = new TaskCompletionSource<UnityWebRequest>();
            asyncOperation.completed += _ => tcs.SetResult(asyncOperation.webRequest);
            return tcs.Task;
        }
    }
     /// <summary>
    /// An enum representing the status of the current Remote Config request.
    /// </summary>
    public enum ConfigRequestStatus
    {
        /// <summary>
        /// Indicates that no Remote Config request has been made.
        /// </summary>
        None,
        /// <summary>
        /// Indicates that the Remote Config request failed.
        /// </summary>
        Failed,
        /// <summary>
        /// Indicates that the Remote Config request succeeded.
        /// </summary>
        Success,
        /// <summary>
        /// Indicates that the Remote Config request is still processing.
        /// </summary>
        Pending
    }

    /// <summary>
    /// A struct representing the response of a Remote Config fetch.
    /// </summary>
    public struct ConfigResponse
    {
        /// <summary>
        /// The origin point of the last retrieved configuration settings.
        /// </summary>
        /// <returns>
        /// An enum describing the origin point of your most recently loaded configuration settings.
        /// </returns>
        public ConfigOrigin requestOrigin;
        /// <summary>
        /// The status of the current Remote Config request.
        /// </summary>
        /// <returns>
        /// An enum representing the status of the current Remote Config request.
        /// </returns>
        public ConfigRequestStatus status;
        /// <summary>
        /// The body of the Remote Config backend response.
        /// </summary>
        /// <returns>
        /// The full response body as a JObject.
        /// </returns>
        public JObject body;
        /// <summary>
        /// The headers from the Remote Config backend response.
        /// </summary>
        /// <returns>
        /// A Dictionary containing the headers..
        /// </returns>
        public Dictionary<string, string> headers;
    }
    /// <summary>
    /// An enum describing the origin point of your most recently loaded configuration settings.
    /// </summary>
    public enum ConfigOrigin
    {
        /// <summary>
        /// Indicates that no configuration settings loaded in the current session.
        /// </summary>
        Default,
        /// <summary>
        /// Indicates that the configuration settings loaded in the current session are cached from a previous session (in other words, no new configuration settings loaded).
        /// </summary>
        Cached,
        /// <summary>
        /// Indicates that new configuration settings loaded from the remote server in the current session.
        /// </summary>
        Remote
    }
}