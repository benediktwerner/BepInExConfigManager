﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ConfigManager.Runtime
{
    public abstract class RuntimeProvider
    {
        public static RuntimeProvider Instance;

        public ReflectionProvider Reflection;
        public TextureUtilProvider TextureUtil;

        public RuntimeProvider()
        {
            Initialize();
        }

        public static void Init() =>
#if CPP
            Instance = new Il2Cpp.Il2CppProvider();
#else
            Instance = new Mono.MonoProvider();
#endif


        public abstract void Initialize();

        // Unity API handlers

        public abstract ColorBlock SetColorBlock(ColorBlock colors, Color? normal = null, Color? highlighted = null, Color? pressed = null,
            Color? disabled = null);
    }
}
