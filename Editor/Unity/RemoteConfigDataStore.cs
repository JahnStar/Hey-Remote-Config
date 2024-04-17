// https://docs.unity3d.com/Packages/com.unity.remote-config@4.0
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;

[assembly: InternalsVisibleTo("Unity.RemoteConfigEditor.Tests")]

namespace Unity.RemoteConfigEditor
{
    public class RemoteConfigDataStore : ScriptableObject, ISerializationCallbackReceiver
    {
        /// <summary>
        /// Declaration of event triggered after action of changed environment is performed
        /// </summary>
        public event Action EnvironmentChanged;

        /// <summary>
        /// Declaration of event triggered after action of changed RemoteSettingDataStore is performed
        /// </summary>
        public event Action RemoteSettingDataStoreChanged;

        const string k_CurrentEnvironment = "UnityRemoteConfigEditorEnvironment";

        /// <summary>
        /// A list of all supported types for keys
        /// </summary>
        public static readonly List<string> rsTypes = new List<string> { "string", "bool", "float", "int", "long", "json" };

        /// <summary>
        /// Enum for current status of the Data Store
        /// </summary>
        public enum m_DataStoreStatus {
            /// <summary>dataStore initialized</summary>
            Init = 0,
            /// <summary>dataStore unsynchronized</summary>
            UnSynchronized = 1,
            /// <summary>dataStore synchronized</summary>
            Synchronized = 2,
            /// <summary>dataStore pending</summary>
            Pending = 3,
            /// <summary>dataStore error</summary>
            Error = 4
        };

        /// <summary>
        /// Property with corresponding accessors depicting status of data store
        /// </summary>
        public m_DataStoreStatus dataStoreStatus { get; set; }

        private void OnEnable()
        {
            RestoreLastSelectedEnvironment(currentEnvironmentName);
            dataStoreStatus = m_DataStoreStatus.Init;
        }

        // Data stores for Remote Settings

        /// <summary>
        /// Property with corresponding accessors for list of keys
        /// </summary>
        public JArray rsKeyList { get; set; } = new JArray();

        /// <summary>
        /// Property with corresponding accessors for list of last cached keys
        /// </summary>
        public JArray rsLastCachedKeyList { get; set; } = new JArray();

        /// <summary>
        /// Property with corresponding accessors for configId
        /// </summary>
        public string configId { get; set; } = "";

        /// <summary>
        /// Property with corresponding accessors for current Environment Name
        /// </summary>
        public string currentEnvironmentName { get; set; } = "";

        /// <summary>
        /// Property with corresponding accessors for current Environment Id
        /// </summary>
        public string currentEnvironmentId { get; set; } = "";

        /// <summary>
        /// Property with corresponding accessors for current Environment being default
        /// </summary>
        public bool currentEnvironmentIsDefault { get; set; }

        /// <summary>
        /// Property with corresponding accessors for list of environments
        /// </summary>
        public JArray environments { get; set; } = new JArray();

        private JObject m_config;

        /// <summary>
        /// Property with corresponding accessors for the config
        /// </summary>
        public JObject config
        {
            get { return m_config;}
            set {
                rsKeyList = new JArray();
                if (value.HasValues)
                {
                    foreach(var val in value["value"])
                    {
                        var newSetting = new JObject();
                        newSetting["metadata"] = new JObject();
                        newSetting["metadata"]["entityId"] = Guid.NewGuid().ToString();
                        newSetting["rs"] = val;
                        rsKeyList.Add(newSetting);
                    }
                    configId = value["id"].Value<string>();
                }
                else
                {
                    configId = null;
                }

                m_config = value;
            }
        }

        // Values for Serialization

        /// <summary>
        /// Property for list of keys to be serialized
        /// </summary>
        public string _rsKeyList;

        /// <summary>
        /// Property for last cached list of keys to be serialized
        /// </summary>
        public string _rsLastCachedKeyList;

        /// <summary>
        /// Property for list of environments to be serialized
        /// </summary>
        public string _environments;

        /// <summary>
        /// Serializes corresponding JObjects into strings
        /// </summary>
        public void OnBeforeSerialize()
        {
            _rsKeyList = rsKeyList == null ? "" : rsKeyList.ToString();
            _rsLastCachedKeyList = rsLastCachedKeyList == null ? "" : rsLastCachedKeyList.ToString();
            _environments = environments == null ? "" : environments.ToString();
        }

        /// <summary>
        /// Deserializes corresponding strings into JObjects
        /// </summary>
        public void OnAfterDeserialize()
        {
            rsKeyList = string.IsNullOrEmpty(_rsKeyList) ? new JArray() : JArray.Parse(_rsKeyList);
            rsLastCachedKeyList = string.IsNullOrEmpty(_rsLastCachedKeyList) ? new JArray() : JArray.Parse(_rsLastCachedKeyList);
            environments = string.IsNullOrEmpty(_environments) ? new JArray() : JArray.Parse(_environments);
        }

        /// <summary>
        /// Property for count of settings (keys) in config
        /// </summary>
        public int settingsCount
        {
            get
            {
                return rsKeyList == null ? 0 : rsKeyList.Count;
            }
        }

        /// <summary>
        /// Sets the the current environment ID name.
        /// </summary>
        /// <param name="currentEnvironment">Current Environment object containing the current environment name and ID</param>
        public void SetCurrentEnvironment(JObject currentEnvironment)
        {
            currentEnvironmentName = currentEnvironment["name"].Value<string>();
            currentEnvironmentId = currentEnvironment["id"].Value<string>();
            currentEnvironmentIsDefault = false;
            if (currentEnvironment["isDefault"] != null)
            {
                currentEnvironmentIsDefault = currentEnvironment["isDefault"].Value<bool>();
            }

            EnvironmentChanged?.Invoke();
        }

        /// <summary>
        /// Sets the default environment.
        /// </summary>
        /// <param name="defaultEnvironmentId">default Environment ID</param>
        public void SetDefaultEnvironment(string defaultEnvironmentId)
        {
            for (var i=0; i < environments.Count; i++)
            {
                ((JObject)environments[i])["isDefault"] = environments[i]["id"].Value<string>() == defaultEnvironmentId;
            }

            // if current environment became default, update the isDefault flag
            if (currentEnvironmentId == defaultEnvironmentId)
            {
                currentEnvironmentIsDefault = true;
            }

            CheckEnvironmentsValid();
        }

        /// <summary>
        /// Checks if set of environments is valid. There must be exactly one default environment.
        /// </summary>
        public void CheckEnvironmentsValid()
        {
            if (environments.Count < 1)
            {
                Debug.Log("Please create at least one environment");
            }

            var defaultEnvironmentsCount = environments.Count((e) => {
                if (((JObject)e)["isDefault"] != null)
                {
                    return e["isDefault"].Value<bool>();
                }
                return false;
            });
            if (defaultEnvironmentsCount < 1)
            {
                Debug.Log("Please set environment as default");
            }
            if (defaultEnvironmentsCount > 1)
            {
                Debug.LogWarning("Something went wrong. More than one default environment");
            }

            var environmentsWithNoName = environments.Where(e => e["name"].Value<string>() == "");
            for (var i = 0; i < environmentsWithNoName.Count(); i++)
            {
                Debug.LogWarning($"Environment with id: {environments.ElementAt(i)["id"].Value<string>()} has an empty name");
            }
        }

        /// <summary>
        /// Returns the name of the last selected environment that is stored in EditorPrefs.
        /// </summary>
        /// <param name="defaultEnvironment"> The default environment name to be returned if last selected environment is not found</param>
        /// <returns> Name of last selected environment or defaultEnvironment if last selected is not found</returns>
        public string RestoreLastSelectedEnvironment(string defaultEnvironment)
        {
            return EditorPrefs.GetString(k_CurrentEnvironment + Application.cloudProjectId, defaultEnvironment);
        }

        /// <summary>
        /// Sets the name of the last selected environment and stores it in EditorPrefs.
        /// </summary>
        /// <param name="environmentName"> Name of environment to be stored</param>
        public void SetLastSelectedEnvironment (string environmentName)
        {
            EditorPrefs.SetString(k_CurrentEnvironment + Application.cloudProjectId, environmentName);
        }

        /// <summary>
        /// Adds a setting to the Remote Settings data store. This will add the setting to the rsKeyList.
        /// </summary>
        /// <param name="newSetting">The setting to be added</param>
        public void AddSetting(JObject newSetting)
        {
            rsKeyList.Add(newSetting);
            RemoteSettingDataStoreChanged?.Invoke();
        }

        /// <summary>
        /// Deletes a setting from the Remote Settings data store. This will delete the setting from the rsKeyList.
        /// </summary>
        /// <param name="entityId">The EntityId of the setting to be deleted</param>
        public void DeleteSetting(string entityId)
        {
            for(int i = 0; i < rsKeyList.Count; i++)
            {
                if (rsKeyList[i]["metadata"]["entityId"].Value<string>() == entityId)
                {
                    rsKeyList.RemoveAt(i);
                    break;
                }
            }
            //m_DataStore.rsKeyList.Remove(m_DataStore.rsKeyList.Find(s => s.metadata.entityId == entityId));
            RemoteSettingDataStoreChanged?.Invoke();
        }

        /// <summary>
        /// Updates a setting in the Remote Settings data store. This will update the setting in the rsKeyList.
        /// </summary>
        /// <param name="oldSetting">The RsKvtData of the setting to be updated</param>
        /// <param name="newSetting">The new setting with the updated fields</param>
        public void UpdateSetting(JObject oldSetting, JObject newSetting)
        {
            var isError = false;
            if (newSetting["rs"]["key"].Value<string>().Length >= 255)
            {
                Debug.LogWarning(newSetting["rs"]["key"].Value<string>() + " is at the maximum length of 255 characters.");
                isError = true;
            }
            if (!isError)
            {
                for (int i = 0; i < rsKeyList.Count; i++)
                {
                    if (rsKeyList[i]["metadata"]["entityId"].Value<string>() == oldSetting["metadata"]["entityId"].Value<string>())
                    {
                        rsKeyList[i] = newSetting;
                    }
                }
                RemoteSettingDataStoreChanged?.Invoke();
            }
        }

    }
}