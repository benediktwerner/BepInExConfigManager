﻿using System;
using UnityEngine;
using UnityEngine.EventSystems;
using ConfigManager.Input;
using BF = System.Reflection.BindingFlags;
using HarmonyLib;
using ConfigManager.UI;

namespace ConfigManager.Input
{
    public class CursorUnlocker
    {
        public static bool Unlock
        {
            get => m_forceUnlock;
            set
            {
                m_forceUnlock = value;
                UpdateCursorControl();
            }
        }
        private static bool m_forceUnlock;

        public static bool ShouldActuallyUnlock => UIManager.ShowMenu && Unlock;

        private static CursorLockMode m_lastLockMode;
        private static bool m_lastVisibleState;

        private static bool m_currentlySettingCursor = false;

        private static Type CursorType
            => m_cursorType
            ?? (m_cursorType = ReflectionUtility.GetTypeByName("UnityEngine.Cursor"));
        private static Type m_cursorType;

        public static void Init()
        {
            SetupPatches();

            UpdateCursorControl();

            Unlock = true;
        }

        public static void UpdateCursorControl()
        {
            try
            {
                m_currentlySettingCursor = true;

                if (ShouldActuallyUnlock)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;

                    if (UIManager.EventSys)
                        SetEventSystem();
                }
                else
                {
                    Cursor.lockState = m_lastLockMode;
                    Cursor.visible = m_lastVisibleState;

                    if (UIManager.EventSys)
                        ReleaseEventSystem();
                }

                m_currentlySettingCursor = false;
            }
            catch (Exception e)
            {
                ConfigMngrPlugin.Logger.LogMessage($"Exception setting Cursor state: {e.GetType()}, {e.Message}");
            }
        }

        // Event system overrides

        private static bool m_settingEventSystem;
        private static EventSystem m_lastEventSystem;
        private static BaseInputModule m_lastInputModule;

        public static void SetEventSystem()
        {
            // temp disabled for new InputSystem
            if (InputManager.CurrentType == InputType.InputSystem)
                return;

            // Disable current event system object
            if (m_lastEventSystem || EventSystem.current)
            {
                if (!m_lastEventSystem)
                    m_lastEventSystem = EventSystem.current;

                m_lastEventSystem.enabled = false;
            }

            // Set to our current system
            m_settingEventSystem = true;
            EventSystem.current = UIManager.EventSys;
            UIManager.EventSys.enabled = true;
            InputManager.ActivateUIModule();
            m_settingEventSystem = false;
        }

        public static void ReleaseEventSystem()
        {
            if (InputManager.CurrentType == InputType.InputSystem)
                return;

            if (m_lastEventSystem)
            {
                m_lastEventSystem.enabled = true;

                m_settingEventSystem = true;
                EventSystem.current = m_lastEventSystem;
                m_lastInputModule?.ActivateModule();
                m_settingEventSystem = false;
            }
        }

        // Patches

        private static void SetupPatches()
        {
            try
            {
                if (CursorType == null)
                    throw new Exception("Could not load Type 'UnityEngine.Cursor'!");

                // Get current cursor state and enable cursor
                m_lastLockMode = (CursorLockMode?)CursorType.GetProperty("lockState", BF.Public | BF.Static)?.GetValue(null, null)
                                 ?? CursorLockMode.None;

                m_lastVisibleState = (bool?)CursorType.GetProperty("visible", BF.Public | BF.Static)?.GetValue(null, null)
                                     ?? false;

                PrefixProperty(typeof(Cursor),
                    "lockState",
                    new HarmonyMethod(typeof(CursorUnlocker).GetMethod(nameof(CursorUnlocker.Prefix_set_lockState))));

                PrefixProperty(typeof(Cursor),
                    "visible",
                    new HarmonyMethod(typeof(CursorUnlocker).GetMethod(nameof(CursorUnlocker.Prefix_set_visible))));

                PrefixProperty(typeof(EventSystem),
                    "current",
                    new HarmonyMethod(typeof(CursorUnlocker).GetMethod(nameof(CursorUnlocker.Prefix_EventSystem_set_current))));
            }
            catch (Exception e)
            {
                ConfigMngrPlugin.Logger.LogMessage($"Error on CursorUnlocker.Init! {e.GetType()}, {e.Message}");
            }
        }

        private static void PrefixProperty(Type type, string property, HarmonyMethod prefix)
        {
            try
            {
                var prop = type.GetProperty(property);
                ConfigMngrPlugin.Harmony.Patch(prop.GetSetMethod(), prefix: prefix);
            }
            catch //(Exception e)
            {
                //MPM.Log($"Unable to patch {type.Name}.set_{property}: {e.Message}");
            }
        }

        public static void Prefix_EventSystem_set_current(ref EventSystem value)
        {
            if (!m_settingEventSystem)
            {
                m_lastEventSystem = value;
                m_lastInputModule = value?.currentInputModule;

                if (ShouldActuallyUnlock)
                    value = UIManager.EventSys;
            }
        }

        // Force mouse to stay unlocked and visible while UnlockMouse and ShowMenu are true.
        // Also keep track of when anything else tries to set Cursor state, this will be the
        // value that we set back to when we close the menu or disable force-unlock.

        public static void Prefix_set_lockState(ref CursorLockMode value)
        {
            if (!m_currentlySettingCursor)
            {
                m_lastLockMode = value;

                if (ShouldActuallyUnlock)
                    value = CursorLockMode.None;
            }
        }

        public static void Prefix_set_visible(ref bool value)
        {
            if (!m_currentlySettingCursor)
            {
                m_lastVisibleState = value;

                if (ShouldActuallyUnlock)
                    value = true;
            }
        }
    }
}
