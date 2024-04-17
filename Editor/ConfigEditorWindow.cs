using System.Collections;
// Copyright Jahn Star Games. All rights reserved.
// Developed by Halil Emre Yildiz 2024.4
// Github: @JahnStar
// ===============================================================================================
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unity.RemoteConfigEditor.UIComponents;
using Newtonsoft.Json.Linq;

namespace Hey.Services.RemoteConfig.Editor
{
    internal class ConfigEditorWindow : EditorWindow
    {
        public bool autosave = true;
        //Window state
        public bool shouldFetchOnInit;
        public bool windowOpenOnInit;
        [NonSerialized] bool m_Initialized;
        SettingsTreeview settingsTreeview;

        ConfigEditorWindowController m_Controller;
        
        //GUI Content
        GUIContent m_loadButtonContent = new GUIContent("Load");
        GUIContent m_saveButtonContent = new GUIContent("Save");
        GUIContent m_ConfigFilePathLabel = new GUIContent("File Path: ");
        GUIContent m_SecretKeyLabel = new GUIContent("Secret Key: ");
        GUIContent m_loadingMessage = new GUIContent("Loading, please wait.");

        //UI Style variables
        const float k_LineHeight = 24f;
        const string m_NoSettingsContent = "To get started, please add a setting";
        Rect toolbarRect => new Rect(0, 0, position.width, (k_LineHeight * 2));
        Rect detailsViewRect =>  new Rect(1f, 0, position.width, position.height - (k_LineHeight * 2.35f));

        // 
        string configPath;
        private string secretKey;

        [MenuItem("Window/Hey Remote Config/Config Editor")]
        public static void GetWindow()
        {
            var RCWindow = GetWindow<ConfigEditorWindow>();
            RCWindow.titleContent = new GUIContent("Hey Remote Config Editor");
            RCWindow.minSize = new Vector2(600, 300);
            RCWindow.windowOpenOnInit = true;
            RCWindow.Focus();
            RCWindow.Repaint();
        }
        
        private void OnEnable() => InitIfNeeded();

        private void OnDisable()
        {
            if (m_Controller != null)
            {
                m_Controller.SetDataStoreDirty();

                try
                {
                    if(settingsTreeview != null) settingsTreeview.OnSettingChanged -= SettingsTreeview_OnConfigSettingsChanged;
                    EditorApplication.quitting -= m_Controller.SetDataStoreDirty;
                    EditorApplication.playModeStateChanged -= EditorApplication_playModeStateChanged;
                }
                catch { }
            }
        }

        private void OnGUI()
        {
            OnEnable();

            // EditorGUI.BeginDisabledGroup(IsLoading());

            DrawToolbar(toolbarRect);
            DrawConfigsSettingsTreeView(new Rect(detailsViewRect.x, detailsViewRect.y + toolbarRect.height + 8, detailsViewRect.width, detailsViewRect.height - 3));

            // EditorGUI.EndDisabledGroup();
            // AddLoadingMessage();
        }

        private void DrawToolbar(Rect toolbarSize)
        {
            var currentY = toolbarSize.y + 4;
            DrawConfigFilePath(currentY);
            DrawSaveLoadButtons(currentY);
            currentY += k_LineHeight;

            DrawSecondLine(currentY);
        }

        private void InitIfNeeded()
        {
            if (!m_Initialized)
            {
                settingsTreeview = new SettingsTreeview();
                settingsTreeview.OnSettingChanged += SettingsTreeview_OnConfigSettingsChanged;
                m_Controller = new ConfigEditorWindowController();
                EditorApplication.quitting += m_Controller.SetDataStoreDirty;
                EditorApplication.playModeStateChanged += EditorApplication_playModeStateChanged;

                settingsTreeview.settingsList = m_Controller.GetSettingsList();
                settingsTreeview.activeSettingsList = m_Controller.GetSettingsList();

                m_Initialized = true;
                m_Controller.Load(false);
            }
        }

        private void SettingsTreeview_OnConfigSettingsChanged(JObject arg1, JObject arg2)
        {
            if(arg1 == null && arg2 != null)
            {
                //new setting added
                m_Controller.AddSetting();
            }
            else if(arg2 == null && arg1 != null)
            {
                //setting removed/deleted
                OnDeleteSetting(arg1["metadata"]["entityId"].Value<string>());
            }
            else if(arg1 != null && arg2 != null)
            {
                //update the setting
                OnUpdateSetting(arg1, arg2);
            }
        }

        private void OnDestroy()
        {
            if (m_Controller != null)
            {
                // Get config list from the data store
                var savedConfig = m_Controller.LoadConfigFile(m_Controller.GetConfigPath(), false);
                if (!(m_Controller.CompareSettings(savedConfig)))
                {
                    if (!EditorUtility.DisplayDialog(m_Controller.k_RCDialogUnsavedChangesTitle,
                        m_Controller.k_RCDialogUnsavedChangesMessage,
                        m_Controller.k_RCDialogUnsavedChangesOK,
                        m_Controller.k_RCDialogUnsavedChangesCancel))
                    {
                        CreateNewWindow(m_Controller.LoadConfigFile("", false));
                        m_Controller.DeleteConfigFile("", false);
                    }
                }
                if (!autosave) m_Controller.DeleteConfigFile("", false);
            }
        }
        private void CreateNewWindow(JObject newConfig)
        {
            ConfigEditorWindow newWindow = (ConfigEditorWindow)CreateInstance(typeof(ConfigEditorWindow));
            newWindow.titleContent.text = m_Controller.k_RCWindowName;
            newWindow.shouldFetchOnInit = true;
            newWindow.Show();
            //
            newWindow.InitIfNeeded();
            newWindow.m_Controller.m_DataStore.config = newConfig;
        }


        private void EditorApplication_playModeStateChanged(PlayModeStateChange obj)
        {
            if(obj == PlayModeStateChange.EnteredPlayMode) m_Controller.SetDataStoreDirty();
        }

        private void OnDeleteSetting(string entityId) => m_Controller.DeleteRemoteSetting(entityId);

        private void OnUpdateSetting(JObject oldItem, JObject newitem) => m_Controller.UpdateRemoteSetting(oldItem, newitem);

        private void AddLoadingMessage() { if (IsLoading()) GUI.Label(new Rect(0, position.height - k_LineHeight, position.width, k_LineHeight), m_loadingMessage); }

        private bool IsLoading()
        {
            bool isLoading = m_Controller.isLoading;
            settingsTreeview.isLoading = isLoading;
            return isLoading;
        }
        
        private void DrawConfigFilePath(float currentY)
        {
            var totalWidth = position.width / 2;
            // EditorGUI.BeginDisabledGroup(m_Controller.GetEnvironmentsCount() <= 1 || IsLoading());
            GUI.Label(new Rect(2, currentY, 120, 20), m_ConfigFilePathLabel);
            
            // add to env
            if (!string.IsNullOrEmpty(configPath))
            {
                Rect textFieldRect = new Rect(120 , currentY, totalWidth, 20);
                string newConfigPath = EditorGUI.TextField(textFieldRect, configPath);
                if (configPath != newConfigPath) m_Controller.SetConfigPath(string.IsNullOrEmpty(newConfigPath) ? m_Controller.defaultConfigPath : newConfigPath);
            }
            configPath = m_Controller.GetConfigPath();
            // EditorGUI.EndDisabledGroup();
        }

        private void DrawSaveLoadButtons(float currentY)
        {
            float boundingBoxPadding = 8;
            var paddedRect = new Rect((position.width / 2) + boundingBoxPadding, currentY,(position.width / 2) - (2 * boundingBoxPadding), 20);
            var buttonWidth = (paddedRect.width / 4);

            // EditorGUI.BeginDisabledGroup(m_Controller.GetEnvironmentsCount() == 0);

            if (GUI.Button(new Rect(-2 + paddedRect.x + 2 * (buttonWidth + (2 * boundingBoxPadding)), paddedRect.y, buttonWidth - (2 * boundingBoxPadding), 20), m_saveButtonContent))
            {
                m_Controller.Save();
            }

            if (GUI.Button(new Rect(paddedRect.x + 3 * buttonWidth + (2.2f*boundingBoxPadding), paddedRect.y, buttonWidth - (2 * boundingBoxPadding), 20), m_loadButtonContent))
            {
                m_Controller.Load();
            }

        }

        void DrawConfigsSettingsTreeView(Rect treeViewRect)
        {
            settingsTreeview.enableEditingSettingsKeys = true;
            settingsTreeview.settingsList = m_Controller.GetSettingsList();
            settingsTreeview.activeSettingsList = m_Controller.GetSettingsList();

            if (!m_Controller.GetSettingsList().Any())
            {
                settingsTreeview.settingsList = null;
                var messageRect = new Rect(treeViewRect.x + 1f, treeViewRect.y + k_LineHeight + 6f, treeViewRect.width - 3f, k_LineHeight);
                showMessage(messageRect, m_NoSettingsContent);
            }
            settingsTreeview.OnGUI(treeViewRect);
        }

        void DrawSecondLine(float currentY)
        {
            GUI.Label(new Rect(2, currentY, 120, 20), m_SecretKeyLabel);
            
            // add to env
            if (!string.IsNullOrEmpty(secretKey))
            {
                var totalWidth = position.width / 2;
                Rect textFieldRect = new Rect(120 , currentY, totalWidth, 20);
                string newSecretKey = EditorGUI.TextField(textFieldRect, secretKey);
                if (secretKey != newSecretKey) m_Controller.SetSecretKey(string.IsNullOrEmpty(secretKey) ? m_Controller.defaultSecretKey : newSecretKey);
            }
            secretKey = m_Controller.GetSecretKey();
            if (GUI.Button(new Rect(position.width - (position.width / 5), currentY + 2, (position.width / 5) - 5, 20), "@JahnStar")) Help.BrowseURL("https://github.com/JahnStar/Hey-Remote-Config");
        }

        private void showMessage(Rect messageRect, string messageText) => EditorGUI.HelpBox(messageRect, messageText, MessageType.Warning);
    }
}