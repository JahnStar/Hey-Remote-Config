using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Unity.RemoteConfigEditor;

namespace Hey.Services.RemoteConfig.Editor
{
    internal class ConfigEditorWindowController
    {
        string envPath = "Assets/_JSG_DATA/Editor/";
        string envFilePath = "Assets/_JSG_DATA/Editor/dotenv";
        public string defaultConfigPath = "Assets/_JSG_DATA/Configs/production_config.json";
        string tempConfigPath = "Assets/_JSG_DATA/Configs/autosaved_config.json";
        public string defaultSecretKey = "none";
        
        public string GetConfigPath()
        {
            string configPath = defaultConfigPath;
            if (!Directory.Exists(envPath)) Directory.CreateDirectory(envPath);
            else if (!File.Exists(envFilePath)) File.Create(envFilePath).Dispose();
            else 
            {
                string content = File.ReadAllText(envFilePath);
                if (content.Contains("config_path")) configPath = content[content.IndexOf("config_path")..].Split('"')[1].Split('"')[0];
                else
                {
                    // add the config path
                    content += "\nconfig_path=\"" + defaultConfigPath + "\"";
                    File.WriteAllText(envFilePath, content);
                }
            }
            return configPath;
        }

        public string SetConfigPath(string newConfigPath)
        {
            string content = File.ReadAllText(envFilePath) + "\n";
            if (!Directory.Exists(envPath)) Directory.CreateDirectory(envPath);
            if (content.Contains("config_path"))
            {
                // change the config path
                string before_content = content[..content.IndexOf("config_path")];
                string after_content = content[content.IndexOf("config_path")..];
                after_content = after_content[after_content.IndexOf("\n")..];
                content = before_content + "config_path=\"" + newConfigPath + "\"" + after_content;
                File.WriteAllText(envFilePath, content[..(content.Length - 1)]);
            }
            else
            {
                // add the config path
                content += "\nconfig_path=\"" + newConfigPath + "\"";
                File.WriteAllText(envFilePath, content);
            }
            return newConfigPath;
        }
        
        public string GetSecretKey()
        {
            string config_secretkey = defaultSecretKey;
            if (!Directory.Exists(envPath)) Directory.CreateDirectory(envPath);
            else if (!File.Exists(envFilePath)) File.Create(envFilePath).Dispose();
            else {
                string content = File.ReadAllText(envFilePath);
                if (content.Contains("config_secretkey")) config_secretkey = content[content.IndexOf("config_secretkey")..].Split('"')[1].Split('"')[0];
                else
                {
                    content += "\nconfig_secretkey=\"" + defaultSecretKey + "\"";
                    File.WriteAllText(envFilePath, content);
                }
            }
            return config_secretkey;
        }

        public string SetSecretKey(string newSecretKey)
        {
            string content = File.ReadAllText(envFilePath) + "\n";
            if (content.Contains("config_secretkey"))
            {
                string before_content = content[..content.IndexOf("config_secretkey")];
                string after_content = content[content.IndexOf("config_secretkey")..];
                after_content = after_content[after_content.IndexOf("\n")..];
                content = before_content + "config_secretkey=\"" + newSecretKey + "\"" + after_content;
                File.WriteAllText(envFilePath, content[..(content.Length - 1)]);
            }
            else
            {
                content += "\nconfig_secretkey=\"" + newSecretKey + "\"";
                File.WriteAllText(envFilePath, content);
            }
            return newSecretKey;
        }

        public JObject LoadConfigFile(string configFilePath, bool debug = true)
        {
            if (string.IsNullOrEmpty(configFilePath)) configFilePath = tempConfigPath;
            if (!Directory.Exists(envPath)) Directory.CreateDirectory(envPath);
            if (File.Exists(configFilePath))
            {
                string configJson = File.ReadAllText(configFilePath);

                // decrypt the config file
                if (configFilePath != tempConfigPath)
                {
                    string secretKey = GetSecretKey();
                    if (secretKey != defaultSecretKey) configJson = EncryptionHelper.Decrypt(secretKey, configJson);
                }

                JObject config = new();

                try { config = JObject.Parse(configJson); } 
                catch 
                { 
                    if (debug) Debug.LogError("Error while parsing the config file. The encryption key is maybe invalid.");
                    return null;
                }

                if (debug) Debug.Log("Loaded config file.");
                return config;
            }
            else if (debug) Debug.LogError("Config file not found.");
            return null;
        }

        public void SaveAsConfigFile(string configFilePath, bool debug = true)
        {
            m_DataStore.config = CreateConfig(TimeSpan.MaxValue);
            string configJson = m_DataStore.config.ToString();

            // cyrpt the config file
            if (configFilePath != tempConfigPath)
            {
                string secretKey = GetSecretKey();
                if (secretKey != defaultSecretKey) configJson = EncryptionHelper.Encrypt(secretKey, configJson);
            }
            
            if (!Directory.Exists(Path.GetDirectoryName(configFilePath))) Directory.CreateDirectory(Path.GetDirectoryName(configFilePath));
            else if (!File.Exists(configFilePath)) File.Create(configFilePath).Dispose();
            File.WriteAllText(configFilePath, configJson);
            if (debug) Debug.Log("Saved config file.");
        }

        public void DeleteConfigFile(string configPath, bool debug = true)
        {
            if (string.IsNullOrEmpty(configPath)) configPath = tempConfigPath;
            if (File.Exists(configPath))
            {
                File.Delete(configPath);
                if (debug) Debug.Log("Deleted config file.");
            }
            else if (debug) Debug.LogError("Config file not found.");
        }
        
        public event Action remoteSettingsStoreChanged;
        public RemoteConfigDataStore m_DataStore = new();
        bool m_IsLoading = false;

        // DialogBox variables
        public readonly string k_RCWindowName = "Remote Config";
        public readonly string k_RCDialogUnsavedChangesTitle = "Unsaved Changes";
        public readonly string k_RCDialogUnsavedChangesMessage = "You have unsaved changes. \n \n" + "If you want them saved, click 'Cancel' then 'Save'.\n" + "Otherwise, click 'OK' to discard the changes.";
        public readonly string k_RCDialogUnsavedChangesOK = "OK";
        public readonly string k_RCDialogUnsavedChangesCancel = "Cancel";

        public bool isLoading
        {
            get { return m_IsLoading; }
            set { m_IsLoading = value; }
        }

        public ConfigEditorWindowController()
        { 

        }

        public void SetDataStoreDirty()
        {
            if (!m_DataStore) EditorUtility.SetDirty(m_DataStore);
        }

        public JArray GetSettingsList()
        {
            var settingsList = m_DataStore.rsKeyList ?? new JArray();
            return (JArray)settingsList.DeepClone();
        }

        public void Load(bool debug = true)
        {
            m_IsLoading = true;
            var config = LoadConfigFile(GetConfigPath(), false);
            if (config == null || config["value"].ToList().Count == 0)
            {
                try 
                {
                    // restore the config from autosave
                    config = LoadConfigFile(tempConfigPath, false);
                    if (config != null && config["value"].ToList().Count > 0) Debug.Log("Restored config from autosaved config file.");
                    else
                    {
                        try 
                        {
                            SaveAsConfigFile(GetConfigPath(), false);
                        }
                        catch 
                        {
                            Debug.LogError("Error while creating the config file.");
                        }
                        config = LoadConfigFile(GetConfigPath(), false);
                    }
                }
                catch { }
            }
            else if (debug) Debug.Log("Loaded config file.");
            m_DataStore.config = config;
        }

        public void Save()
        {
            SaveAsConfigFile(GetConfigPath());
        }

        private JObject CreateConfig(TimeSpan expiration)
        {
            JObject config = new JObject();
            config["id"] = Guid.NewGuid().ToString();
            config["createdAt"] = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ");
            config["expiresAt"] = expiration == TimeSpan.MaxValue ? "never" : DateTime.Now.Add(expiration).ToString("yyyy-MM-ddTHH:mm:ssZ");
            config["type"] = "settings";
            config["value"] = ConvertList(GetSettingsList());
            JArray ConvertList(JArray list)
            {
                JArray newList = new JArray();
                foreach (var item in list)
                {
                    JObject newItem = new JObject();
                    newItem["key"] = item["rs"]["key"];
                    newItem["type"] = item["rs"]["type"];
                    newItem["value"] = item["rs"]["value"];
                    newList.Add(newItem);
                }
                return newList;
            }
            config["app_name"] = Application.productName;
            config["app_version"] = Application.version;
            return config;
        }

        public void AddSetting()
        {
            var jSetting = new JObject();
            jSetting["metadata"] = new JObject();
            jSetting["metadata"]["entityId"] = Guid.NewGuid().ToString();
            jSetting["rs"] = new JObject();
            jSetting["rs"]["key"] = "Setting" + m_DataStore.settingsCount.ToString();
            jSetting["rs"]["value"] = "";
            jSetting["rs"]["type"] = "";
            m_DataStore.AddSetting(jSetting);
        }

        public void OnRemoteSettingDataStoreChanged() => remoteSettingsStoreChanged?.Invoke();
        public void UpdateRemoteSetting(JObject oldItem, JObject newItem) => m_DataStore.UpdateSetting(oldItem, newItem);
        public void DeleteRemoteSetting(string entityId) => m_DataStore.DeleteSetting(entityId);

        public bool CompareSettings(JObject savedConfig)
        {
            bool isSame = false;
            SaveAsConfigFile(tempConfigPath, false);
            // compare the saved config with the temp config
            if (File.Exists(tempConfigPath))
            {
                string configJson = File.ReadAllText(tempConfigPath);
                
                JObject tempConfig = JObject.Parse(configJson);
                // compare values 
                JArray tempValues = tempConfig["value"] as JArray;
                JArray values = savedConfig["value"] as JArray;
                if (tempValues.Count == values.Count)
                {
                    isSame = true;
                    for (int i = 0; i < tempValues.Count; i++)
                    {
                        if (!JToken.DeepEquals(tempValues[i], values[i]))
                        {
                            isSame = false;
                            break;
                        }
                    }
                }
            }
            return isSame;
        }
    }
}