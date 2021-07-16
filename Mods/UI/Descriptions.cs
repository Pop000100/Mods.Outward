﻿using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using BepInEx.Configuration;
using HarmonyLib;
using Vheos.ModdingCore;
using Vheos.Extensions.Math;
using Vheos.Extensions.Collections;
using Vheos.Extensions.General;



/* TO DO:
 * - extend to more item types (rags, varnishes)
 */
namespace ModPack
{
    public class Descriptions : AMod, IDelayedInit
    {
        #region const
        static private readonly Vector2 BAR_MAX_SIZE = new Vector2(2.75f, 2.50f);
        static private readonly Vector2 BAR_PIVOT = new Vector2(0f, 1f);
        private const float DURABILITY_MAX_MAX = 777f;    // Duty (unique halberd)
        private const float DURABILITY_BAR_SCALING_CURVE = 0.75f;
        private const float FRESHNESS_LIFESPAN_MAX = 104f;   // Travel Ration
        private const float FRESHNESS_BAR_SCALING_CURVE = 2 / 3f;
        private const int DEFAULT_FONT_SIZE = 19;
        private const int WATER_ITEMS_FIRST_ID = 5600000;
        static private Color HEALTH_COLOR = new Color(0.765f, 0.522f, 0.525f, 1f);
        static private Color STAMINA_COLOR = new Color(0.827f, 0.757f, 0.584f, 1f);
        static private Color MANA_COLOR = new Color(0.529f, 0.702f, 0.816f, 1f);
        static private Color NEEDS_COLOR = new Color(0.584f, 0.761f, 0.522f, 1f);
        static private Color CORRUPTION_COLOR = new Color(0.655f, 0.647f, 0.282f, 1f);
        static private Color STATUSEFFECT_COLOR = new Color(0.780f, 1f, 0.702f, 1f);
        static private Color STATUSCURES_COLOR = new Color(1f, 0.702f, 0.706f);
        #endregion
        #region enum
        [Flags]
        private enum Details
        {
            None = 0,
            All = ~0,

            Vitals = 1 << 1,
            MaxVitals = 1 << 2,
            Needs = 1 << 3,
            Corruption = 1 << 4,
            RegenRates = 1 << 5,
            StatusEffects = 1 << 6,
            StatusCures = 1 << 7,
            Cooldown = 1 << 8,
            Costs = 1 << 9,
        }
        #endregion
        #region class
        private class Row
        {
            // Publics
            public string Label
            { get; }
            public string Content
            {
                get
                {
                    string formattedContent = "";
                    if (Prefix != null)
                        formattedContent += Prefix;
                    formattedContent += _content;

                    if (Size != DEFAULT_FONT_SIZE)
                        formattedContent = $"<size={Size}>{formattedContent}</size>";

                    return $"<color=#{ColorUtility.ToHtmlStringRGBA(_color)}>{formattedContent}</color>";
                }
            }
            public Details Detail;
            public int Order
            { get; }
            public int Size
            { get; }
            public string Prefix;

            // Private
            private string _content;
            private Color _color;

            // Constructors
            public Row(string label, string content, Details detail, int order = int.MaxValue, Color color = default)
            {
                Label = label;
                _content = content;
                Detail = detail;
                Order = order;
                _color = color;
                Prefix = "";

                Size = DEFAULT_FONT_SIZE;
                if (_content.Length >= 20)
                    Size--;
                if (_content.Length >= 25)
                    Size--;
                if (_content.Length >= 30)
                    Size--;
            }
        }
        private class RowsCache
        {
            // Publics
            public List<Row> GetRows(Item item)
            {
                if (!_rowsByItem.ContainsKey(item.ItemID))
                    CacheItemRows(item);
                return _rowsByItem[item.ItemID];
            }

            // Privates
            private Dictionary<int, List<Row>> _rowsByItem;
            private void CacheItemRows(Item item)
            {
                List<Row> rows = new List<Row>();

                if (item.TryAs(out Skill skill))
                    FormatSkillRows(skill, rows);
                else
                {
                    Effect[] effects = item is WaterItem ? GetWaterEffects(item.ItemID) : item.GetEffects();
                    foreach (var effect in effects)
                    {
                        Row newRow = GetFormattedItemRow(effect);
                        if (newRow != null)
                            rows.Add(newRow);
                    }
                }

                rows.Sort((a, b) => a.Order.CompareTo(b.Order));
                _rowsByItem.Add(item.ItemID, rows);
            }

            // Constructors
            public RowsCache()
            {
                _rowsByItem = new Dictionary<int, List<Row>>();
            }
        }
        #endregion

        // Settings
        static private ModSetting<bool> _barsToggle, _equipmentToggle;
        static private ModSetting<bool> _addBackgrounds;
        static private ModSetting<Details> _details;
        static private ModSetting<bool> _displayRelativeAttackSpeed, _normalizeImpactDisplay, _moveBarrierBelowProtection, _hideNumericalDurability;
        static private ModSetting<int> _durabilityBarSize, _freshnessBarSize, _barThickness;
        static private ModSetting<bool> _durabilityTiedToMax, _freshnessTiedToLifespan;
        override protected void Initialize()
        {
            _details = CreateSetting(nameof(_details), Details.None);

            _equipmentToggle = CreateSetting(nameof(_equipmentToggle), false);
            _displayRelativeAttackSpeed = CreateSetting(nameof(_displayRelativeAttackSpeed), false);
            _normalizeImpactDisplay = CreateSetting(nameof(_normalizeImpactDisplay), false);
            _moveBarrierBelowProtection = CreateSetting(nameof(_moveBarrierBelowProtection), false);
            _hideNumericalDurability = CreateSetting(nameof(_hideNumericalDurability), false);

            _barsToggle = CreateSetting(nameof(_barsToggle), false);
            _durabilityTiedToMax = CreateSetting(nameof(_durabilityTiedToMax), false);
            _durabilityBarSize = CreateSetting(nameof(_durabilityBarSize), (100 / BAR_MAX_SIZE.x).Round(), IntRange(0, 100));
            _freshnessTiedToLifespan = CreateSetting(nameof(_freshnessTiedToLifespan), false);
            _freshnessBarSize = CreateSetting(nameof(_freshnessBarSize), (100 / BAR_MAX_SIZE.x).Round(), IntRange(0, 100));
            _barThickness = CreateSetting(nameof(_barThickness), (100 / BAR_MAX_SIZE.y).Round(), IntRange(0, 100));
            _addBackgrounds = CreateSetting(nameof(_addBackgrounds), false);

            AddEventOnConfigClosed(() => SetBackgrounds(_addBackgrounds));

            _rowsCache = new RowsCache();
        }
        override protected void SetFormatting()
        {

            _details.Format("Details to display");
            _equipmentToggle.Format("Equipment");
            Indent++;
            {
                _displayRelativeAttackSpeed.Format("Display relative attack speed", _equipmentToggle);
                _displayRelativeAttackSpeed.Description = "Attack speed will be displayedas +/- X%\n" +
                                                          "If the weapon has default attack speed (1), it won't be displayed";
                _normalizeImpactDisplay.Format("Normalize impact display", _equipmentToggle);
                _normalizeImpactDisplay.Description = "Impact damage/resistance will be displayed in the damages/resistances list and will have its own icon, just like all the other damage/resistance types";
                _moveBarrierBelowProtection.Format("Move barrier below protection", _equipmentToggle);
                _moveBarrierBelowProtection.Description = "Barrier will be displayed right under protection instead of between resistances and impact resistance";
                _hideNumericalDurability.Format("Hide numerical durability display", _equipmentToggle);
                _hideNumericalDurability.Description = "Hides the \"Durability: XXX/YYY\" row so the only indicator is the durability bar";
                Indent--;
            }
            _barsToggle.Format("Bars");
            _barsToggle.Description = "Change sizes of durability and freshness progress bars";
            Indent++;
            {
                _durabilityTiedToMax.Format("Durability proportional to max", _barsToggle);
                _durabilityTiedToMax.Description = "Items that are hard to break will have a longer bar\n" +
                                                   "Items that break easily will have a shorter bar";
                _durabilityBarSize.Format("Durability length", _durabilityTiedToMax, false);
                _durabilityBarSize.Description = "Displayed on weapon, armors, lanterns and tools";
                _freshnessTiedToLifespan.Format("Freshness proportional to lifespan", _barsToggle);
                _freshnessTiedToLifespan.Description = "Foods that stays fresh for a long time will have a longer bar\n" +
                                                       "Foods that decay quickly will have a shorter bar";
                _freshnessBarSize.Format("Freshness length", _freshnessTiedToLifespan, false);
                _freshnessBarSize.Description = "Displayed on food and drinks";
                _barThickness.Format("Thickness", _barsToggle);
                Indent--;
            }

            _addBackgrounds.Format("Add backgrounds to foods/drinks");
            _addBackgrounds.Description = "Display a big \"potions\" icon in the background of foods' and drinks' description box (by default, only Life Potion uses it)";
        }
        protected override string Description
        => "• Display extra item details in inventory\n" +
        "(restored health/stamina/mana, granted status effects)\n" +
        "• Override durability and freshness bars\n" +
        "(automatic scaling, thickness)";
        override protected string SectionOverride
        => ModSections.UI;
        override public void LoadPreset(int preset)
        {
            switch ((Presets.Preset)preset)
            {
                case Presets.Preset.Vheos_PreferredUI:
                    ForceApply();
                    _details.Value = Details.All;
                    _equipmentToggle.Value = true;
                    {
                        _displayRelativeAttackSpeed.Value = true;
                        _normalizeImpactDisplay.Value = true;
                        _moveBarrierBelowProtection.Value = true;
                        _hideNumericalDurability.Value = true;
                    }
                    _barsToggle.Value = true;
                    {
                        _durabilityTiedToMax.Value = true;
                        _freshnessTiedToLifespan.Value = true;
                        _barThickness.Value = 60;
                    }
                    _addBackgrounds.Value = true;
                    break;
            }
        }

        // Utility
        static private Sprite _impactIcon;
        static private RowsCache _rowsCache;
        static private void TryCacheImpactIcon(CharacterUI characterUI)
        {
            if (_impactIcon == null
            && characterUI.m_menus[(int)CharacterUI.MenuScreens.Equipment].TryAs(out EquipmentMenu equipmentMenu)
            && equipmentMenu.transform.GetFirstComponentsInHierarchy<EquipmentOverviewPanel>().TryNonNull(out var equipmentOverview)
            && equipmentOverview.m_lblImpactAtk.TryNonNull(out var impactDisplay)
            && impactDisplay.m_imgIcon.TryNonNull(out var impactImage))
                _impactIcon = impactImage.sprite;
        }
        static private void SetBackgrounds(bool state)
        {
            Item lifePotion = Prefabs.GetIngestibleByName("Life Potion");
            Sprite potionBackground = lifePotion.m_overrideSigil;
            foreach (var ingestibleByID in Prefabs.IngestiblesByID)
                if (ingestibleByID.Value != lifePotion)
                    ingestibleByID.Value.m_overrideSigil = state ? potionBackground : null;
        }
        static private Row GetFormattedItemRow(Effect effect)
        {
            switch (effect)
            {
                // Vitals
                case AffectHealth _:
                    return new Row("CharacterStat_Health".Localized(),
                                   FormatEffectValue(effect),
                                   Details.Vitals, 21, HEALTH_COLOR);
                case AffectStamina _:
                    return new Row("CharacterStat_Stamina".Localized(),
                                   FormatEffectValue(effect),
                                   Details.Vitals, 31, STAMINA_COLOR);
                case AffectMana _:
                    return new Row("CharacterStat_Mana".Localized(),
                                   FormatEffectValue(effect),
                                   Details.Vitals, 41, MANA_COLOR);
                // Max vitals
                case AffectBurntHealth _:
                    return new Row("General_Max".Localized() + ". " + "CharacterStat_Health".Localized(),
                                   FormatEffectValue(effect),
                                   Details.MaxVitals, 23, HEALTH_COLOR);
                case AffectBurntStamina _:
                    return new Row("General_Max".Localized() + ". " + "CharacterStat_Stamina".Localized(),
                                   FormatEffectValue(effect),
                                   Details.MaxVitals, 33, STAMINA_COLOR);
                case AffectBurntMana _:
                    return new Row("General_Max".Localized() + ". " + "CharacterStat_Mana".Localized(),
                                   FormatEffectValue(effect),
                                   Details.MaxVitals, 43, MANA_COLOR);
                // Needs
                case AffectFood _:
                    return new Row("CharacterStat_Food".Localized(),
                                   FormatEffectValue(effect, 10f, "%"),
                                   Details.Needs, 11, NEEDS_COLOR);
                case AffectDrink _:
                    return new Row("CharacterStat_Drink".Localized(),
                                   FormatEffectValue(effect, 10f, "%"),
                                   Details.Needs, 12, NEEDS_COLOR);
                case AffectFatigue _:
                    return new Row("CharacterStat_Sleep".Localized(),
                                   FormatEffectValue(effect, 10f, "%"),
                                   Details.Needs, 13, NEEDS_COLOR);
                // Corruption
                case AffectCorruption _:
                    return new Row("CharacterStat_Corruption".Localized(),
                                   FormatEffectValue(effect, 10f, "%"),
                                   Details.Corruption, 51, CORRUPTION_COLOR);
                // Cure
                case RemoveStatusEffect removeStatusEffect:
                    string text = "";
                    switch (removeStatusEffect.CleanseType)
                    {
                        case RemoveStatusEffect.RemoveTypes.StatusSpecific: text = removeStatusEffect.StatusEffect.StatusName; break;
                        case RemoveStatusEffect.RemoveTypes.StatusType: text = removeStatusEffect.StatusType.Tag.TagName; break;
                        case RemoveStatusEffect.RemoveTypes.StatusFamily: text = removeStatusEffect.StatusFamily.Get().Name; break;
                        case RemoveStatusEffect.RemoveTypes.NegativeStatuses: text = "All Negative Status Effects"; break;
                    }
                    return new Row("",
                                   $"- {text}",
                                   Details.StatusCures, 71, STATUSCURES_COLOR);
                // Status
                case AddStatusEffect addStatusEffect:
                    StatusEffect statusEffect = addStatusEffect.Status;
                    Row statusName = new Row("",
                                             $"+ {statusEffect.StatusName}",
                                             Details.StatusEffects, 61, STATUSEFFECT_COLOR);
                    if (addStatusEffect.ChancesToContract < 100)
                        statusName.Prefix = $"<color=silver>({addStatusEffect.ChancesToContract}%)</color> ";

                    if (!statusEffect.HasEffectsAndDatas())
                        return statusName;

                    StatusData.EffectData firstEffectData = statusEffect.GetDatas()[0];
                    if (firstEffectData.Data.IsNullOrEmpty())
                        return statusName;

                    string firstValue = firstEffectData.Data[0];
                    switch (statusEffect.GetEffects()[0])
                    {
                        case AffectHealth _:
                            return new Row("CharacterStat_Health".Localized() + " Regen",
                                           FormatStatusEffectValue(firstValue.ToFloat(), statusEffect.StartLifespan),
                                           Details.Vitals | Details.RegenRates, 22, HEALTH_COLOR);
                        case AffectStamina _:
                            return new Row("CharacterStat_Stamina".Localized() + " Regen",
                                           FormatStatusEffectValue(firstValue.ToFloat(), statusEffect.StartLifespan),
                                           Details.Vitals | Details.RegenRates, 32, STAMINA_COLOR);
                        case AffectMana _:
                            return new Row("CharacterStat_Mana".Localized() + " Regen",
                                           FormatStatusEffectValue(firstValue.ToFloat(), statusEffect.StartLifespan, 1f, "%"),
                                           Details.Vitals | Details.RegenRates, 42, MANA_COLOR);
                        case AffectCorruption _:
                            return new Row("CharacterStat_Corruption".Localized() + " Regen",
                                           FormatStatusEffectValue(firstValue.ToFloat(), statusEffect.StartLifespan, 10f, "%"),
                                           Details.Corruption | Details.RegenRates, 52, CORRUPTION_COLOR);
                        default: return statusName;
                    }
                default: return null;
            }
        }
        static private void FormatSkillRows(Skill skill, List<Row> rows)
        {
            if (skill.Cooldown > 0)
                rows.Add(new Row("ItemStat_Cooldown".Localized(),
                                  skill.Cooldown.FormatSeconds(skill.Cooldown >= 60),
                                  Details.Cooldown, 11, NEEDS_COLOR));
            if (skill.HealthCost > 0)
                rows.Add(new Row("CharacterStat_Health".Localized() + " " + "BuildingMenu_Supplier_Cost".Localized(),
                                  skill.HealthCost.ToString(),
                                  Details.Costs, 12, HEALTH_COLOR));
            if (skill.StaminaCost > 0)
                rows.Add(new Row("CharacterStat_Stamina".Localized() + " " + "BuildingMenu_Supplier_Cost".Localized(),
                                  skill.StaminaCost.ToString(),
                                  Details.Costs, 13, STAMINA_COLOR));
            if (skill.ManaCost > 0)
                rows.Add(new Row("CharacterStat_Mana".Localized() + " " + "BuildingMenu_Supplier_Cost".Localized(),
                                  skill.ManaCost.ToString(),
                                  Details.Costs, 14, MANA_COLOR));
            if (skill.DurabilityCost > 0 || skill.DurabilityCostPercent > 0)
            {
                bool isPercent = skill.DurabilityCostPercent > 0;
                rows.Add(new Row("ItemStat_Durability".Localized() + " " + "BuildingMenu_Supplier_Cost".Localized(),
                                  isPercent ? (skill.DurabilityCostPercent.ToString() + "%") : skill.DurabilityCost.ToString(),
                                  Details.Costs, 15, NEEDS_COLOR));
            }

        }
        static private string FormatEffectValue(Effect effect, float divisor = 1f, string postfix = "")
        {
            string content = "";
            if (effect != null)
            {
                float value = effect.GetValue();
                if (value != 0)
                    content = $"{value.Div(divisor).Round()}{postfix}";
                if (value > 0)
                    content = $"+{content}";
            }
            return content;
        }
        static private string FormatStatusEffectValue(float value, float duration, float divisor = 1f, string postfix = "")
        {
            string content = "";

            float totalValue = value * duration;
            string formattedDuration = duration < 60 ? $"{duration.Mod(60).RoundDown()}sec" : $"{duration.Div(60).RoundDown()}min";
            if (value != 0)
                content = $"{totalValue.Div(divisor).Round()}{postfix} / {formattedDuration}";
            if (value > 0)
                content = $"+{content}";

            return content;
        }
        static private Effect[] GetWaterEffects(WaterType waterType)
        {
            switch (waterType)
            {
                case WaterType.Clean: return Global.WaterDistributor.m_cleanWaterEffects;
                case WaterType.Fresh: return Global.WaterDistributor.m_freshWaterEffects;
                case WaterType.Salt: return Global.WaterDistributor.m_saltWaterEffects;
                case WaterType.Rancid: return Global.WaterDistributor.m_rancidWaterEffects;
                case WaterType.Magic: return Global.WaterDistributor.m_magicWaterEffects;
                case WaterType.Pure: return Global.WaterDistributor.m_pureWaterEffects;
                case WaterType.Healing: return Global.WaterDistributor.m_healingWaterEffects;
                default: return null;
            }
        }
        static private Effect[] GetWaterEffects(int waterID)
        => GetWaterEffects((WaterType)(waterID - WATER_ITEMS_FIRST_ID));
        static private void TrySwapProtectionWithResistances(Item item)
        {
            #region quit
            if (!_moveBarrierBelowProtection || !item.TryAs(out Equipment equipment) || equipment.BarrierProt <= 0)
                return;
            #endregion

            int resistancesIndex = item.m_displayedInfos.IndexOf(ItemDetailsDisplay.DisplayedInfos.DamageResistance);
            int barrierIndex = item.m_displayedInfos.IndexOf(ItemDetailsDisplay.DisplayedInfos.BarrierProtection);
            if (resistancesIndex < 0 || barrierIndex < 0 || barrierIndex < resistancesIndex)
                return;

            Utility.Swap(ref item.m_displayedInfos[resistancesIndex], ref item.m_displayedInfos[barrierIndex]);
        }

        // Hooks
#pragma warning disable IDE0051 // Remove unused private members
        [HarmonyPatch(typeof(ItemDetailsDisplay), "ShowDetails"), HarmonyPrefix]
        static bool ItemDetailsDisplay_ShowDetails_Pre(ItemDetailsDisplay __instance)
        {
            TrySwapProtectionWithResistances(__instance.m_lastItem);

            #region quit
            if (_details.Value == Details.None || !__instance.m_lastItem.TryNonNull(out var item) || !item.IsIngestible() && item.IsNot<Skill>())
                return true;
            #endregion

            if (item.TryAs(out WaterContainer waterskin)
            && waterskin.GetWaterItem().TryNonNull(out var waterItem))
                item = waterItem;

            int rowIndex = 0;
            foreach (var row in _rowsCache.GetRows(item))
                if (_details.Value.HasFlag(row.Detail))
                    __instance.GetRow(rowIndex++).SetInfo(row.Label, row.Content);

            List<ItemDetailRowDisplay> detailRows = __instance.m_detailRows;
            for (int i = rowIndex; i < detailRows.Count; i++)
                detailRows[i].Hide();

            return false;
        }

        [HarmonyPatch(typeof(ItemDetailsDisplay), "RefreshDetails"), HarmonyPostfix]
        static void ItemDetailsDisplay_RefreshDetails_Post(ItemDetailsDisplay __instance)
        {
            Item item = __instance.m_lastItem;
            GameObject durabilityHolder = __instance.m_durabilityHolder;
            #region quit
            if (!_barsToggle || item == null || durabilityHolder == null)
                return;
            #endregion

            // Cache
            RectTransform rectTransform = durabilityHolder.GetComponent<RectTransform>();
            bool isFood = item.m_perishScript != null && item.IsNot<Equipment>();
            ModSetting<int> barSize = isFood ? _freshnessBarSize : _durabilityBarSize;
            float curve = isFood ? FRESHNESS_BAR_SCALING_CURVE : DURABILITY_BAR_SCALING_CURVE;

            // Calculate automated values
            float rawSize = float.NaN;
            if (_freshnessTiedToLifespan && isFood)
            {
                float decayRate = item.PerishScript.m_baseDepletionRate;
                float decayTime = 100f / (decayRate * 24f);
                rawSize = decayTime / FRESHNESS_LIFESPAN_MAX;
            }
            else if (_durabilityTiedToMax && !isFood)
                rawSize = item.MaxDurability / DURABILITY_MAX_MAX;

            if (!rawSize.IsNaN())
                barSize.Value = rawSize.Pow(curve).MapFrom01(0, 100f).Round();

            // Assign
            float sizeOffset = barSize / 100f * BAR_MAX_SIZE.x - 1f;
            rectTransform.pivot = BAR_PIVOT;
            rectTransform.localScale = new Vector2(1f + sizeOffset, _barThickness / 100f * BAR_MAX_SIZE.y);
        }

        [HarmonyPatch(typeof(ItemDetailsDisplay), "RefreshDetail"), HarmonyPrefix]
        static bool ItemDetailsDisplay_RefreshDetail_Post(ItemDetailsDisplay __instance, ref bool __result, int _rowIndex, ItemDetailsDisplay.DisplayedInfos _infoType)
        {
            if (_infoType == ItemDetailsDisplay.DisplayedInfos.AttackSpeed
            && _displayRelativeAttackSpeed)
            {
                float attackSpeedOffset = __instance.cachedWeapon.BaseAttackSpeed - 1f;
                Weapon.WeaponType weaponType = __instance.cachedWeapon.Type;
                if (attackSpeedOffset == 0 || weaponType == Weapon.WeaponType.Shield || weaponType == Weapon.WeaponType.Bow)
                    return false;

                string text = (attackSpeedOffset > 0 ? "+" : "") + attackSpeedOffset.ToString("P0");
                Color color = attackSpeedOffset > 0 ? Global.LIGHT_GREEN : Global.LIGHT_RED;
                __instance.GetRow(_rowIndex).SetInfo(LocalizationManager.Instance.GetLoc("ItemStat_AttackSpeed"), Global.SetTextColor(text, color));
                __result = true;
                return false;
            }
            else if ((_infoType == ItemDetailsDisplay.DisplayedInfos.Impact || _infoType == ItemDetailsDisplay.DisplayedInfos.ImpactResistance) && _normalizeImpactDisplay)
            {
                float value = _infoType == ItemDetailsDisplay.DisplayedInfos.Impact
                                         ? __instance.cachedWeapon.Impact
                                         : __instance.cachedEquipment.ImpactResistance;
                if (value <= 0)
                    return false;

                TryCacheImpactIcon(__instance.CharacterUI);
                __instance.GetRow(_rowIndex).SetInfo("", value.Round(), _impactIcon);
                __result = true;
                return false;
            }
            else if (_infoType == ItemDetailsDisplay.DisplayedInfos.Durability && _hideNumericalDurability)
                return false;

            return true;
        }
    }
}