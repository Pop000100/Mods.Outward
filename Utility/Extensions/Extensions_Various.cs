﻿using System;
using UnityEngine;
using HarmonyLib;
using System.Collections.Generic;
using BepInEx.Configuration;
using System.Text.RegularExpressions;
using System.Text;
using System.Globalization;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ModPack
{
    static public class Extensions_Various
    {
        // Game
        static public bool IsPlayer(this Character character)
        => character.Faction == Character.Factions.Player;
        static public bool IsOwnerOf(this Character character, Item item)
        => character == item.OwnerCharacter;
        static public bool IsOwnerOf(this Character character, MeleeHitDetector hitDetector)
        => character == hitDetector.OwnerCharacter;
        static public bool HasStatusEffect(this Character character, StatusEffect statusEffect)
        => character.StatusEffectMngr.HasStatusEffect(statusEffect.IdentifierName);
        static public bool IsIngestible(this Item item)
        => Prefabs.IngestiblesByID.ContainsKey(item.ItemID);
        static public bool IsEatable(this Item item)
        => item.IsUsable && item.ActivateEffectAnimType == Character.SpellCastType.Eat;
        static public bool IsDrinkable(this Item item)
        => item.IsUsable
        && (item.ActivateEffectAnimType == Character.SpellCastType.DrinkWater || item.ActivateEffectAnimType == Character.SpellCastType.Potion);

        // Item effects
        static public T GetEffect<T>(this Item item) where T : Effect
        {
            foreach (Transform child in item.transform)
            {
                T effect = child.GetComponent<T>();
                if (effect != null)
                    return effect;
            }
            return null;
        }
        static public T[] GetEffects<T>(this Item item) where T : Effect
        => item.GetComponentsInChildren<T>();
        static public Effect[] GetEffects(this Item item)
        => item.GetEffects<Effect>();
        static public T AddEffect<T>(this Item item) where T : Effect
        {
            GameObject effectsHolder = item.FindChild("Effects");
            if (effectsHolder == null)
            {
                effectsHolder = new GameObject("Effects");
                effectsHolder.BecomeChildOf(item);
            }
            T effect = effectsHolder.AddComponent<T>();
            UnityEngine.Object.DontDestroyOnLoad(item);
            return effect;
        }
        static public float GetValue(this Effect effect)
        {
            switch (effect)
            {
                case AffectHealth t: return t.AffectQuantity;
                case AffectStamina t: return t.AffectQuantity;
                case AffectMana t: return t.Value;
                case AffectBurntHealth t: return t.AffectQuantity;
                case AffectBurntStamina t: return t.AffectQuantity;
                case AffectBurntMana t: return t.AffectQuantity;
                case AffectFood t: return t.m_affectQuantity;
                case AffectDrink t: return t.m_affectQuantity;
                case AffectFatigue t: return t.m_affectQuantity;
                case AffectCorruption t: return t.AffectQuantity;
                case AffectStat t: return t.Value;
            }
            return 0f;
        }
        static public void SetValue(this Effect effect, float value)
        {
            switch (effect)
            {
                case AffectHealth t: t.AffectQuantity = value; break;
                case AffectStamina t: t.AffectQuantity = value; break;
                case AffectMana t: t.Value = value; break;
                case AffectBurntHealth t: t.AffectQuantity = value; break;
                case AffectBurntStamina t: t.AffectQuantity = value; break;
                case AffectBurntMana t: t.AffectQuantity = value; break;
                case AffectFood t: t.m_affectQuantity = value; break;
                case AffectDrink t: t.m_affectQuantity = value; break;
                case AffectFatigue t: t.m_affectQuantity = value; break;
                case AffectCorruption t: t.AffectQuantity = value; break;
                case AffectStat t: t.Value = value; break;
            }
        }
        static public void RemoveAllEffects(this Item item)
        {
            foreach (Transform child in item.transform)
                child.GetComponents<Effect>().Destroy();
        }

        // StatusEffects effects
        static public (StatusEffect, string[]) GetStatusEffectAndValuesOfEffect<T>(this Item item) where T : Effect
        {
            foreach (var addStatusEffect in item.GetEffects<AddStatusEffect>())
            {
                StatusEffect statusEffect = addStatusEffect.Status;
                string[] values = statusEffect.GetValuesOfEffect<T>();
                if (values != null)
                    return (statusEffect, values);
            }
            return default;
        }
        static public string[] GetValuesOfEffect<T>(this StatusEffect statusEffect) where T : Effect
        {
            List<Effect> effects = statusEffect.StatusData.EffectSignature.Effects;
            for (int i = 0; i < effects.Count; i++)
                if (effects[i] is T)
                    return statusEffect.StatusData.EffectsData[i].Data;
            return null;
        }
        static public Dictionary<Effect, string[]> GetValuesByEffect(this StatusEffect statusEffect)
        {
            // Cache
            Dictionary<Effect, string[]> valuesByEffect = new Dictionary<Effect, string[]>();
            EffectSignature effectSignature = statusEffect.StatusData.EffectSignature;
            if (effectSignature == null)
                return valuesByEffect;

            List<Effect> effects = effectSignature.Effects;
            StatusData.EffectData[] effectDatas = statusEffect.StatusData.EffectsData;

            // Add        
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i] == null)
                    continue;

                string[] values = new string[0];
                if (effectDatas.IsIndexValid(i))
                    values = effectDatas[i].Data;

                valuesByEffect.Add(effects[i], values);
            }
            return valuesByEffect;
        }
        static public void RemoveAllEffects(this StatusEffect statusEffect)
        => statusEffect.StatusData.EffectSignature.GetComponentsInChildren<Effect>().Destroy();
        static public T AddEffect<T>(this StatusEffect statusEffect) where T : Effect
        {
            EffectSignature effectSignature = statusEffect.StatusData.EffectSignature;
            GameObject effectsHolder = effectSignature.FindChild("Effects");
            if (effectsHolder == null)
            {
                effectsHolder = new GameObject("Effects");
                effectsHolder.BecomeChildOf(effectSignature);
            }
            T effect = effectsHolder.AddComponent<T>();
            UnityEngine.Object.DontDestroyOnLoad(statusEffect);
            return effect;
        }
        static public bool HasEffectsAndDatas(this StatusEffect statusEffect)
        {
            EffectSignature effectSignature = statusEffect.StatusData.EffectSignature;
            if (effectSignature == null)
                return false;

            List<Effect> effects = effectSignature.Effects;
            if (effects.IsEmpty())
                return false;

            StatusData.EffectData[] effectDatas = statusEffect.StatusData.EffectsData;
            if (effectDatas.IsEmpty())
                return false;

            return true;
        }
        static public List<Effect> GetEffects(this StatusEffect statusEffect)
        => statusEffect.StatusData.EffectSignature.Effects;
        static public StatusData.EffectData[] GetDatas(this StatusEffect statusEffect)
        => statusEffect.StatusData.EffectsData;


        static public string Localized(this string text)
    => LocalizationManager.Instance.GetLoc(text);
        static public Item FirstItem(this GroupContainer stack)
        => stack.GetContainedItems()[0];
        static public KeyCode ToKeyCode(this string text)
        => GameInput.ToKeyCode(text);
        static public string ToName(this ControlsInput.GameplayActions action)
        => ControlsInput.GetGameplayActionName(action);
        static public string ToName(this ControlsInput.MenuActions action)
        => ControlsInput.GetMenuActionName(action);

        // Config
        static public ConfigurationManagerAttributes Attributes(this ConfigEntryBase t)
        => (t.Description.Tags[0] as ConfigurationManagerAttributes);
        static private void SetVisibility(this ConfigEntryBase t, bool a)
        => t.Attributes().Browsable = a;
        static public void Show(this ConfigEntryBase t)
        => t.SetVisibility(true);
        static public void Hide(this ConfigEntryBase t)
        => t.SetVisibility(false);

        // Various
        static public CodeMatcher CodeMatcher(this IEnumerable<CodeInstruction> t)
        => new CodeMatcher(t);
        static public string SplitCamelCase(this string t)
        => Regex.Replace(t, "([a-z](?=[A-Z])|[A-Z](?=[A-Z][a-z]))", "$1 ");
        static public int SetBitCount(this int t)
        {
            int count = 0;
            while (t != 0)
            {
                count++;
                t &= t - 1;
            }
            return count;
        }
        static public T As<T>(this object t)
        => (T)t;
        static public bool Is<T>(this object t)
        => t is T;
        static public bool IsNot<T>(this object t)
        => !t.Is<T>();
        static public bool IsAssignableTo<T>(this Type t)
        => typeof(T).IsAssignableFrom(t);
        static public bool IsNotAssignableTo<T>(this Type t)
        => !t.IsAssignableTo<T>();
        static public int ToInt(this string t)
        => Convert.ToInt32(t);
        static public T Random<T>(this IList<T> t)
        => t[UnityEngine.Random.Range(0, t.Count)];
        static public string FormatGameHours(this float t, bool showDays = true, bool showHours = true, bool showMinutes = true, bool showSeconds = true)
        {
            int days = t.Div(24).RoundDown();
            int hours = t.Mod(24).RoundDown();
            int minutes = t.Mod(1).Mul(60).RoundDown();
            int seconds = t.Mod(1f / 60).Mul(3600).RoundDown();
            return (showDays ? days.ToString("D2") + "d " : "") +
                   (showHours ? hours.ToString("D2") + "h " : "") +
                   (showMinutes ? minutes.ToString("D2") + "m " : "") +
                   (showSeconds ? seconds.ToString("D2") + "s " : "");
        }
        static public string FormatGameHours(this double t, bool showDays = true, bool showHours = true, bool showMinutes = true, bool showSeconds = true)
        => ((float)t).FormatGameHours(showDays, showHours, showMinutes, showSeconds);
        static public string FormatSeconds(this float t, bool showMinutes = true, bool showSeconds = true)
        {
            int minutes = t.Div(60).RoundDown();
            int seconds = t.Mod(60).RoundDown();
            return (showMinutes ? minutes.ToString("D2") + "m " : "") +
                   (showSeconds ? seconds.ToString("D2") + "s " : "");
        }
        static public bool HasFlagsAttribute(this Enum enumeration)
        => enumeration.GetType().IsDefined(typeof(FlagsAttribute), false);
        static public void Append(this StringBuilder builder, params string[] texts)
        {
            foreach (var text in texts)
                builder.Append(text);
        }
        static public string SubstringBefore(this string text, string find, bool caseSensitive = true)
        {
            if (!text.IsEmpty())
            {
                int length = text.IndexOf(find, caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
                if (length >= 1)
                    return text.Substring(0, length);
            }
            return string.Empty;
        }
        static public bool ContainsSubstring(this string text, string find)
        => !text.IsEmpty() && text.IndexOf(find) >= 0;
        static public List<T> GetAllComponentsInHierarchy<T>(this Transform root) where T : Component
        {
            List<T> components = new List<T>();
            components.Add(root.GetComponents<T>());
            Utility.AppendChildrenRecurisvely(root, components);
            return components;
        }
        static public List<RaycastResult> GetMouseHits(this GraphicRaycaster t)
        {
            PointerEventData eventData = new PointerEventData(null);
            eventData.position = Input.mousePosition;
            List<RaycastResult> hits = new List<RaycastResult>();
            t.Raycast(eventData, hits);
            return hits;
        }
        static public int ID(this string name)
        => Prefabs.ItemIDsByName[name];

        // GOName
        static public string GOName(this Component t)
        => t.gameObject.name;
        static public string SetGOName(this Component t, string name)
        => t.gameObject.name = name;
        static public bool GONameIs(this Component t, string name)
        => t.gameObject.name == name;

        static public bool IsEmpty(this string text)
            => string.IsNullOrEmpty(text);
        static public float ToFloat(this string text)
        {
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                return value;
            return float.NaN;
        }

        static public void SetX(ref this Vector2 t, float a)
        => t.x = a;
        static public void SetY(ref this Vector2 t, float a)
        => t.y = a;
        static public void SetX(ref this Vector3 t, float a)
        => t.x = a;
        static public void SetY(ref this Vector3 t, float a)
        => t.y = a;
        static public void SetZ(ref this Vector3 t, float a)
        => t.z = a;

        static public Coroutine ExecuteOnceAfterDelay(this MonoBehaviour monoBehaviour, float delay, Action action)
        => monoBehaviour.StartCoroutine(Utility.CoroutineWaitForSeconds(delay, action));
        static public Coroutine ExecuteOnceWhen(this MonoBehaviour monoBehaviour, Func<bool> test, Action action)
        => monoBehaviour.StartCoroutine(Utility.CoroutineWaitUntil(test, action));
        static public Coroutine ExecuteWhile(this MonoBehaviour monoBehaviour, Func<bool> test, Action action, Action finalAction = null)
        => monoBehaviour.StartCoroutine(Utility.CoroutineWhile(test, action, finalAction));
        static public Coroutine ExecuteUntil(this MonoBehaviour monoBehaviour, Func<bool> test, Action action, Action finalAction = null)
        => monoBehaviour.StartCoroutine(Utility.CoroutineDoUntil(test, action, finalAction));
    }
}