﻿using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using BepInEx.Configuration;
using HarmonyLib;
using BepInEx;


namespace ModPack
{
    [BepInPlugin(GUID, NAME, VERSION)]
    public class Main : BaseUnityPlugin
    {
        // Settings
        public const string GUID = "com.Vheos.ModPack";
        public const string NAME = "Vheos Mod Pack";
        public const string VERSION = "1.6.1";

        // Utility
        private List<Type> _awakeModTypes;
        private List<Type> _delayedModTypes;
        private List<IUpdatable> _updatableMods;
        private void CategorizeModsByInstantiationTime(params Type[] whitelist)
        {
            foreach (var modType in Utility.GetDerivedTypes<AMod>())
                if (modType.IsNotAssignableTo<IExcludeFromBuild>())
                    if (whitelist.Length == 0 || modType.IsContainedIn(whitelist))
                        if (modType.IsAssignableTo<IDelayedInit>())
                            _delayedModTypes.Add(modType);
                        else
                            _awakeModTypes.Add(modType);
        }
        private void TryDelayedInitialize()
        {
            if (!Prefabs.IsInitialized
            && ResourcesPrefabManager.Instance.Loaded
            && SplitScreenManager.Instance != null)
            {
                Prefabs.Initialize();
                InstantiateMods(_delayedModTypes);
            }
        }
        private void InstantiateMods(ICollection<Type> modTypes)
        {
            foreach (var modType in modTypes)
                InstantiateMod(modType);
        }
        private void InstantiateMod(Type modType)
        {
            AMod newMod = (AMod)Activator.CreateInstance(modType);
            if (modType.IsAssignableTo<IUpdatable>())
                _updatableMods.Add(newMod as IUpdatable);
        }
        private void UpdateMods(ICollection<IUpdatable> updatableMods)
        {
            foreach (var updatableMod in updatableMods)
                UpdateMod(updatableMod);
        }
        private void UpdateMod(IUpdatable updatableMod)
        {
            if (updatableMod.IsEnabled)
                updatableMod.OnUpdate();
        }

        // Mono
        private void Awake()
        {
            _awakeModTypes = new List<Type>();
            _delayedModTypes = new List<Type>();
            _updatableMods = new List<IUpdatable>();

            Tools.Initialize(this, Logger);
            GameInput.Initialize();
            Players.Initialize();

            CategorizeModsByInstantiationTime();
            InstantiateMods(_awakeModTypes);
        }
        private void Update()
        {
            TryDelayedInitialize();
            UpdateMods(_updatableMods);
            Tools.TryRedrawConfigWindow();
        }
    }
}