﻿using BepInEx;
using BepInEx.Configuration;
using ConfigManager.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
#if CPP
using BepInEx.IL2CPP;
#endif

namespace ConfigManager.UI
{
    public class ConfigurationEditor
    {
        internal class ConfigFileInfo
        {
            public ConfigFile RefConfigFile;

            internal List<EntryInfo> Entries = new List<EntryInfo>();

            internal bool isCompletelyHidden;
            internal Button listButton;
            internal GameObject contentObj;

            internal IEnumerable<GameObject> HiddenEntries 
                => Entries.Where(it => it.IsHidden).Select(it => it.content);
        }

        internal class EntryInfo
        {
            public EntryInfo(CachedConfigEntry cached) { Cached = cached; }
            public CachedConfigEntry Cached { get; }
            public ConfigEntryBase RefEntry;
            public bool IsHidden { get; internal set; }

            internal GameObject content;
        }

        public static ConfigurationEditor Instance { get; internal set; }

        public static bool ShowHiddenConfigs { get; internal set; }

        internal static GameObject MainPanel;
        internal static GameObject CategoryListViewport;
        internal static GameObject ConfigEditorViewport;

        internal static string Filter => currentFilter ?? "";
        private static string currentFilter;

        private static readonly HashSet<CachedConfigEntry> editingEntries = new HashSet<CachedConfigEntry>();
        internal static Button saveButton;

        private static readonly Dictionary<string, ConfigFileInfo> _categoryInfos = new Dictionary<string, ConfigFileInfo>();
        private static ConfigFileInfo _currentCategory;

        private static Color _normalInactiveColor = new Color(0.38f, 0.34f, 0.34f);
        private static Color _normalActiveColor = UnityHelpers.ToColor("c2b895");

        // called by UIManager.Init
        internal static void Create()
        {
            if (Instance != null)
            {
                ConfigManager.Log.LogWarning("An instance of PreferencesEditor already exists, cannot create another!");
                return;
            }

            Instance = new ConfigurationEditor();
            Instance.ConstructMenu();
        }

        public static void OnEntryEdit(CachedConfigEntry entry)
        {
            if (!editingEntries.Contains(entry))
                editingEntries.Add(entry);

            if (!saveButton.interactable)
                saveButton.interactable = true;
        }

        public static void OnEntrySaveOrUndo(CachedConfigEntry entry)
        {
            if (editingEntries.Contains(entry))
                editingEntries.Remove(entry);

            if (!editingEntries.Any())
                saveButton.interactable = false;
        }

        public static void SavePreferences()
        {
            foreach (var ctg in _categoryInfos.Values)
            {
                foreach (var entry in ctg.Entries)
                    entry.RefEntry.BoxedValue = entry.Cached.EditedValue;
                
                var file = ctg.RefConfigFile;
                if (!file.SaveOnConfigSet)
                    file.Save();
            }

            foreach (var entry in editingEntries)
                entry.OnSaveOrUndo();

            editingEntries.Clear();
            saveButton.interactable = false;
        }

        public static void SetHiddenConfigVisibility(bool show)
        {
            if (ShowHiddenConfigs == show)
                return;

            ShowHiddenConfigs = show;

            foreach (var entry in _categoryInfos)
            {
                var info = entry.Value;

                if (info.isCompletelyHidden)
                    info.listButton.gameObject.SetActive(ShowHiddenConfigs);
            }

            if (_currentCategory != null && !ShowHiddenConfigs && _currentCategory.isCompletelyHidden)
                UnsetActiveCategory();

            RefreshFilter();
        }

        public static void FilterConfigs(string search)
        {
            currentFilter = search.ToLower();
            RefreshFilter();
        }

        internal static void RefreshFilter()
        {
            if (_currentCategory == null)
                return;

            foreach (var entry in _currentCategory.Entries)
            {
                bool val = (string.IsNullOrEmpty(currentFilter) 
                                || entry.RefEntry.Definition.Key.ToLower().Contains(currentFilter)
                                || (entry.RefEntry.Description?.Description?.Contains(currentFilter) ?? false))
                           && (!entry.IsHidden || ShowHiddenConfigs);

                entry.content.SetActive(val);
            }
        }

        public static void SetActiveCategory(string categoryIdentifier)
        {
            if (!_categoryInfos.ContainsKey(categoryIdentifier))
                return;

            UnsetActiveCategory();

            var info = _categoryInfos[categoryIdentifier];

            _currentCategory = info;

            var obj = info.contentObj;
            obj.SetActive(true);

            var btn = info.listButton;
            btn.colors = RuntimeProvider.Instance.SetColorBlock(btn.colors, _normalActiveColor);

            RefreshFilter();
        }

        internal static void UnsetActiveCategory()
        {
            if (_currentCategory == null)
                return;

            var colors = _currentCategory.listButton.colors;
            colors = RuntimeProvider.Instance.SetColorBlock(colors, _normalInactiveColor);
            _currentCategory.listButton.colors = colors;
            _currentCategory.contentObj.SetActive(false);

            _currentCategory = null;
        }

        private void ConstructMenu()
        {
            MainPanel = UIFactory.CreatePanel("MainMenu", out GameObject mainContent);

            var rect = MainPanel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.2f, 0.02f);
            rect.anchorMax = new Vector2(0.8f, 0.98f);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 1000);

            ConstructTitleBar(mainContent);

            ConstructSaveButton(mainContent);

            ConstructToolbar(mainContent);

            ConstructEditorViewport(mainContent);
        }

        private void ConstructTitleBar(GameObject content)
        {
            // Core title bar holder

            GameObject titleBar = UIFactory.CreateHorizontalGroup(content, "MainTitleBar", true, true, true, true, 0, new Vector4(3, 3, 15, 3));
            UIFactory.SetLayoutElement(titleBar, minWidth: 25, minHeight: 30, flexibleHeight: 0);

            // Main title label

            var text = UIFactory.CreateLabel(titleBar, "TitleLabel", $"<b><color=#8b736b>BepInEx Config Manager</color></b> " +
                $"<i><color=#ffe690>v{ConfigManagerPlugin.VERSION}</color></i>", 
                TextAnchor.MiddleLeft, default, true, 15);
            UIFactory.SetLayoutElement(text.gameObject, flexibleWidth: 5000);

            // Hide button

            ColorBlock colorBlock = new ColorBlock();
            colorBlock = RuntimeProvider.Instance.SetColorBlock(colorBlock, new Color(1, 0.2f, 0.2f),
                new Color(1, 0.6f, 0.6f), new Color(0.3f, 0.1f, 0.1f));

            var hideButton = UIFactory.CreateButton(titleBar,
                "HideButton",
                $"X",
                () => { UIManager.ShowMenu = false; },
                colorBlock);
            UIFactory.SetLayoutElement(hideButton.gameObject, minWidth: 25, flexibleWidth: 0);

            Text hideText = hideButton.GetComponentInChildren<Text>();
            hideText.color = Color.white;
            hideText.resizeTextForBestFit = true;
            hideText.resizeTextMinSize = 8;
            hideText.resizeTextMaxSize = 14;
        }

        private void ConstructSaveButton(GameObject mainContent)
        {
            saveButton = UIFactory.CreateButton(mainContent, "SaveButton", "Save Preferences", SavePreferences);
            UIFactory.SetLayoutElement(saveButton.gameObject, minHeight: 35, flexibleWidth: 9999);
            var colors = new ColorBlock() { colorMultiplier = 1 };
            saveButton.colors = RuntimeProvider.Instance.SetColorBlock(colors, new Color(0.1f, 0.3f, 0.1f),
                new Color(0.2f, 0.5f, 0.2f), new Color(0.1f, 0.2f, 0.1f), new Color(0.2f, 0.2f, 0.2f));

            saveButton.interactable = false;

            saveButton.gameObject.SetActive(!ConfigManager.Auto_Save_Configs.Value);
        }

        private void ConstructToolbar(GameObject parent)
        {
            var toolbarGroup = UIFactory.CreateHorizontalGroup(parent, "Toolbar", false, true, true, true, 4, new Vector4(3, 3, 3, 3),
                new Color(0.1f, 0.1f, 0.1f));

            var toggleObj = UIFactory.CreateToggle(toolbarGroup, "HiddenConfigsToggle", out Toggle toggle, out Text toggleText);
            toggle.isOn = false;
            toggle.onValueChanged.AddListener((bool val) =>
            {
                SetHiddenConfigVisibility(val);
            });
            toggleText.text = "Show Advanced Settings";
            UIFactory.SetLayoutElement(toggleObj, minWidth: 280, minHeight: 25, flexibleHeight: 0, flexibleWidth: 0);

            var inputField = UIFactory.CreateInputField(toolbarGroup, "FilterInput", "Search...", 14);
            UIFactory.SetLayoutElement(inputField, flexibleWidth: 9999);
            var input = inputField.GetComponent<InputField>();
            input.onValueChanged.AddListener(FilterConfigs);
        }

        private void ConstructEditorViewport(GameObject mainContent)
        {
            var horiGroup = UIFactory.CreateHorizontalGroup(mainContent, "Main", true, true, true, true, 2, default, new Color(0.08f, 0.08f, 0.08f));

            var ctgList = UIFactory.CreateScrollView(horiGroup, "CategoryList", out GameObject ctgViewport, out _, new Color(0.1f, 0.1f, 0.1f));
            UIFactory.SetLayoutElement(ctgList, minWidth: 300, flexibleWidth: 0);
            CategoryListViewport = ctgViewport;

            var editor = UIFactory.CreateScrollView(horiGroup, "ConfigEditor", out GameObject editorViewport, out _, new Color(0.05f, 0.05f, 0.05f));
            UIFactory.SetLayoutElement(editor, flexibleWidth: 9999);
            ConfigEditorViewport = editorViewport;
        }

        // wait for end of chainloader setup. mods that set up preferences after this aren't compatible atm.
        // Also, stray ConfigFiles defined manually will not be found either.
        internal static void SetupCategories()
        {
            ColorBlock btnColors = new ColorBlock();
            btnColors = RuntimeProvider.Instance.SetColorBlock(btnColors, _normalInactiveColor, new Color(0.6f, 0.55f, 0.45f),
                new Color(0.20f, 0.18f, 0.15f));

#if CPP
            ConfigFile coreConfig = ConfigFile.CoreConfig;
#else
            ConfigFile coreConfig = (ConfigFile)ReflectionUtility.GetPropertyInfo(typeof(ConfigFile), "CoreConfig").GetValue(null, null);
#endif
            if (coreConfig != null)
                SetupCategory(coreConfig, null, new BepInPlugin("bepinex.core.config", "BepInEx", "1.0"), btnColors, true);

#if CPP
            foreach (var plugin in IL2CPPChainloader.Instance.Plugins.Values)
            {
                var configFile = (plugin.Instance as BasePlugin)?.Config;
                if (configFile != null && configFile.Keys.Any())
                    SetupCategory(configFile, plugin.Instance, plugin.Metadata, btnColors);
            }
#else
            if (BepInEx.Bootstrap.Chainloader.PluginInfos == null)
            {
                ConfigManager.Log.LogWarning("Chainload pluginInfos is null!");
                return;
            }
            foreach (var plugin in BepInEx.Bootstrap.Chainloader.PluginInfos.Values)
            {
                if (plugin.Instance?.Info?.Metadata == null)
                    continue;

                var configFile = plugin.Instance.Config;
                if (configFile != null && configFile.Keys.Any())
                    SetupCategory(configFile, plugin.Instance, plugin.Instance.Info.Metadata, btnColors);
            }
#endif
        }

        internal static void SetupCategory(ConfigFile configFile, object plugin, BepInPlugin meta, ColorBlock btnColors, bool forceAdvanced = false)
        {
            try
            {
#pragma warning disable IDE0019 // Use pattern matching
#if CPP
                var basePlugin = plugin as BasePlugin;
#else
                var basePlugin = plugin as BaseUnityPlugin;
#endif
#pragma warning restore IDE0019

                if (basePlugin != null)
                {
                    var type = basePlugin.GetType();
                    if (!forceAdvanced && type.GetCustomAttributes(typeof(BrowsableAttribute), false)
                                              .Cast<BrowsableAttribute>()
                                              .Any(it => !it.Browsable))
                    {
                        forceAdvanced = true;
                    }
                }

                var info = new ConfigFileInfo()
                {
                    RefConfigFile = configFile,
                };

                // List button

                var btn = UIFactory.CreateButton(CategoryListViewport,
                    "BUTTON_" + meta.GUID,
                    meta.Name,
                    () => { SetActiveCategory(meta.GUID); },
                    btnColors);
                UIFactory.SetLayoutElement(btn.gameObject, flexibleWidth: 9999, minHeight: 30, flexibleHeight: 0);

                info.listButton = btn;

                // Editor content

                var content = UIFactory.CreateVerticalGroup(ConfigEditorViewport, "CATEGORY_" + meta.GUID,
                    true, false, true, true, 4, default, new Color(0.05f, 0.05f, 0.05f));

                var dict = new Dictionary<string, List<ConfigEntryBase>>
                {
                    { "", new List<ConfigEntryBase>() } // make sure the null category is first.
                };

                // Iterate and prepare categories
                foreach (var entry in configFile.Keys)
                {
                    string sec = entry.Section;
                    if (sec == null)
                        sec = "";

                    if (!dict.ContainsKey(sec))
                        dict.Add(sec, new List<ConfigEntryBase>());

                    dict[sec].Add(configFile[entry]);
                }

                // Create actual entry editors
                foreach (var ctg in dict)
                {
                    if (!string.IsNullOrEmpty(ctg.Key))
                    {
                        var bg = UIFactory.CreateHorizontalGroup(content, "TitleBG", true, true, true, true, 0, default,
                            new Color(0.07f, 0.07f, 0.07f));
                        var title = UIFactory.CreateLabel(bg, $"Title_{ctg.Key}", ctg.Key, TextAnchor.MiddleCenter, default, true, 17);
                        UIFactory.SetLayoutElement(title.gameObject, minHeight: 30, minWidth: 200, flexibleWidth: 9999);
                    }

                    foreach (var configEntry in ctg.Value)
                    {
                        var cache = new CachedConfigEntry(configEntry, content);
                        cache.Enable();

                        var obj = cache.m_UIroot;

                        bool advanced = forceAdvanced;

                        if (!advanced)
                        {
                            var tags = configEntry.Description?.Tags;
                            if (tags != null && tags.Any())
                            {
                                if (tags.Any(it => it is string s && s == "Advanced"))
                                {
                                    advanced = true;
                                }
                                else if (tags.FirstOrDefault(it => it.GetType().Name == "ConfigurationManagerAttributes") is object attributes)
                                {
                                    advanced = (bool?)attributes.GetType().GetField("IsAdvanced")?.GetValue(attributes) == true;
                                }
                            }
                        }

                        info.Entries.Add(new EntryInfo(cache)
                        {
                            RefEntry = configEntry,
                            content = obj,
                            IsHidden = advanced
                        });
                    }
                }

                // hide buttons for completely-hidden categories.
                if (!info.Entries.Any(it => !it.IsHidden))
                {
                    btn.gameObject.SetActive(false);
                    info.isCompletelyHidden = true;
                }

                content.SetActive(false);

                info.contentObj = content;

                _categoryInfos.Add(meta.GUID, info);
            }
            catch (Exception ex)
            {
                ConfigManager.Log.LogWarning($"Exception setting up category '{meta.GUID}'!\r\n{ex}");
            }
        }
    }
}