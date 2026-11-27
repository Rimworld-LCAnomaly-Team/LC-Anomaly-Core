using System;
using System.Collections.Generic;
using System.Linq;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Defs;
using LCAnomalyCore.ModExtensions;
using LCAnomalyCore.Settings;
using LCAnomalyCore.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Damage
{
    /// <summary>四色配置来源。</summary>
    public enum FourColorProfileSource
    {
        None,
        Authored,
        DefaultTable,
        AutoInferred,
        UserOverride
    }

    /// <summary>用户自定义的护甲四色覆写记录。</summary>
    public class FourColorApparelOverride : IExposable
    {
        public string defName;
        public bool enabled = true;
        public LCRiskLevel riskLevel = LCRiskLevel.TETH;
        public float red = 1f;
        public float white = 1f;
        public float black = 1f;
        public float pale = 1f;

        public void ExposeData()
        {
            Scribe_Values.Look(ref defName, "defName");
            Scribe_Values.Look(ref enabled, "enabled", true);
            Scribe_Values.Look(ref riskLevel, "riskLevel", LCRiskLevel.TETH);
            Scribe_Values.Look(ref red, "red", 1f);
            Scribe_Values.Look(ref white, "white", 1f);
            Scribe_Values.Look(ref black, "black", 1f);
            Scribe_Values.Look(ref pale, "pale", 1f);
        }
    }

    /// <summary>用户自定义的武器四色覆写记录。</summary>
    public class FourColorWeaponOverride : IExposable
    {
        public string defName;
        public FourColorDamageType damageType = FourColorDamageType.None;
        public LCRiskLevel riskLevel = LCRiskLevel.ZAYIN;
        public float damageMultiplier = 1f;

        public void ExposeData()
        {
            Scribe_Values.Look(ref defName, "defName");
            Scribe_Values.Look(ref damageType, "damageType", FourColorDamageType.None);
            Scribe_Values.Look(ref riskLevel, "riskLevel", LCRiskLevel.ZAYIN);
            Scribe_Values.Look(ref damageMultiplier, "damageMultiplier", 1f);
        }
    }

    /// <summary>用户自定义的种族四色覆写记录。</summary>
    public class FourColorRaceOverride : IExposable
    {
        public string defName;
        public FourColorTargetKind targetKind = FourColorTargetKind.Default;
        public float red = 1f;
        public float white = 1f;
        public float black = 1f;
        public float pale = 1f;

        public void ExposeData()
        {
            Scribe_Values.Look(ref defName, "defName");
            Scribe_Values.Look(ref targetKind, "targetKind", FourColorTargetKind.Default);
            Scribe_Values.Look(ref red, "red", 1f);
            Scribe_Values.Look(ref white, "white", 1f);
            Scribe_Values.Look(ref black, "black", 1f);
            Scribe_Values.Look(ref pale, "pale", 1f);
        }
    }

    /// <summary>
    /// 四色默认配置与运行时覆写缓存中枢（非破坏式解析，不修改 ThingDef.modExtensions）。
    /// </summary>
    public static class FourColorOverrides
    {
        /// <summary>解析后的护甲四色配置。</summary>
        public struct ResolvedApparelProfile
        {
            public LCRiskLevel riskLevel;
            public float red;
            public float white;
            public float black;
            public float pale;
            public FourColorProfileSource source;
            public bool isAuthoredEgo;

            public float ResistanceFor(FourColorDamageType damageType)
            {
                switch (damageType)
                {
                    case FourColorDamageType.Red: return red;
                    case FourColorDamageType.White: return white;
                    case FourColorDamageType.Black: return black;
                    case FourColorDamageType.Pale: return pale;
                    default: return 1f;
                }
            }
        }

        /// <summary>解析后的武器四色配置。</summary>
        public struct ResolvedWeaponProfile
        {
            public FourColorDamageType damageType;
            public FourColorDamageType secondaryDamageType;
            public LCRiskLevel riskLevel;
            public float damageMultiplier;
            public FourColorProfileSource source;
            public bool isAuthoredEgo;
            public bool guaranteedHit;
        }

        /// <summary>解析后的种族四色配置。</summary>
        public struct ResolvedRaceProfile
        {
            public FourColorTargetKind targetKind;
            public float red;
            public float white;
            public float black;
            public float pale;
            public FourColorProfileSource source;

            public float ResistanceFor(FourColorDamageType damageType)
            {
                switch (damageType)
                {
                    case FourColorDamageType.Red: return red;
                    case FourColorDamageType.White: return white;
                    case FourColorDamageType.Black: return black;
                    case FourColorDamageType.Pale: return pale;
                    default: return 1f;
                }
            }
        }

        private static bool initialized;
        private static readonly Dictionary<ThingDef, ResolvedApparelProfile> apparelCache = new Dictionary<ThingDef, ResolvedApparelProfile>();
        private static readonly Dictionary<ThingDef, ResolvedWeaponProfile> weaponCache = new Dictionary<ThingDef, ResolvedWeaponProfile>();
        private static readonly Dictionary<ThingDef, ResolvedRaceProfile> raceCache = new Dictionary<ThingDef, ResolvedRaceProfile>();

        private static readonly Dictionary<ThingDef, ResolvedApparelProfile> baselineApparelCache = new Dictionary<ThingDef, ResolvedApparelProfile>();
        private static readonly Dictionary<ThingDef, ResolvedWeaponProfile> baselineWeaponCache = new Dictionary<ThingDef, ResolvedWeaponProfile>();
        private static readonly Dictionary<ThingDef, ResolvedRaceProfile> baselineRaceCache = new Dictionary<ThingDef, ResolvedRaceProfile>();

        public static bool IsInitialized => initialized;

        public static void Initialize()
        {
            initialized = true;
            RebuildCache();
        }

        public static void RebuildCache()
        {
            if (!initialized)
            {
                return;
            }

            apparelCache.Clear();
            weaponCache.Clear();
            raceCache.Clear();
            baselineApparelCache.Clear();
            baselineWeaponCache.Clear();
            baselineRaceCache.Clear();

            Setting_LCAnomalyCore settings = Setting_LCAnomalyCore_Main.Settings;
            bool useDefaults = settings?.If_EnableFourColorDefaults ?? true;
            bool autoInfer = settings?.If_AutoInferUnlistedApparel ?? true;

            var apparelDefaultsMap = new Dictionary<string, FourColorApparelDefault>(StringComparer.Ordinal);
            var raceCategoryMap = new Dictionary<FourColorRaceCategory, FourColorRaceCategoryDefault>();

            if (useDefaults)
            {
                foreach (FourColorDefaultsDef def in DefDatabase<FourColorDefaultsDef>.AllDefsListForReading)
                {
                    if (def == null || !def.enabled)
                    {
                        continue;
                    }

                    if (def.apparelEntries != null)
                    {
                        for (int i = 0; i < def.apparelEntries.Count; i++)
                        {
                            FourColorApparelDefault entry = def.apparelEntries[i];
                            if (entry != null && !entry.thingDef.NullOrEmpty() && !apparelDefaultsMap.ContainsKey(entry.thingDef))
                            {
                                apparelDefaultsMap[entry.thingDef] = entry;
                            }
                        }
                    }

                    if (def.raceCategories != null)
                    {
                        for (int i = 0; i < def.raceCategories.Count; i++)
                        {
                            FourColorRaceCategoryDefault rc = def.raceCategories[i];
                            if (rc != null && !raceCategoryMap.ContainsKey(rc.category))
                            {
                                raceCategoryMap[rc.category] = rc;
                            }
                        }
                    }
                }
            }

            var apparelOverrideMap = BuildOverrideMap(settings?.apparelOverrides, e => e.defName);
            var weaponOverrideMap = BuildOverrideMap(settings?.weaponOverrides, e => e.defName);
            var raceOverrideMap = BuildOverrideMap(settings?.raceOverrides, e => e.defName);

            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def == null)
                {
                    continue;
                }

                if (IsEligibleApparel(def))
                {
                    BuildApparelEntry(def, apparelDefaultsMap, apparelOverrideMap, useDefaults, autoInfer);
                }

                if (IsEligibleWeapon(def) || def.GetModExtension<FourColorWeaponExtension>() != null)
                {
                    BuildWeaponEntry(def, weaponOverrideMap);
                }

                if (IsEligibleRace(def))
                {
                    BuildRaceEntry(def, raceCategoryMap, raceOverrideMap);
                }
            }
        }

        private static Dictionary<string, T> BuildOverrideMap<T>(List<T> list, Func<T, string> keySelector) where T : class
        {
            var dict = new Dictionary<string, T>(StringComparer.Ordinal);
            if (list == null)
            {
                return dict;
            }

            for (int i = 0; i < list.Count; i++)
            {
                T item = list[i];
                if (item == null)
                {
                    continue;
                }

                string key = keySelector(item);
                if (!key.NullOrEmpty())
                {
                    dict[key] = item;
                }
            }

            return dict;
        }

        private static void BuildApparelEntry(
            ThingDef def,
            Dictionary<string, FourColorApparelDefault> defaultsMap,
            Dictionary<string, FourColorApparelOverride> overrideMap,
            bool useDefaults,
            bool autoInfer)
        {
            bool hasBaseline = TryBuildBaselineApparel(def, defaultsMap, useDefaults, autoInfer, out ResolvedApparelProfile baseline);
            if (hasBaseline)
            {
                baselineApparelCache[def] = baseline;
            }

            if (overrideMap.TryGetValue(def.defName, out FourColorApparelOverride ov))
            {
                if (ov.enabled)
                {
                    apparelCache[def] = new ResolvedApparelProfile
                    {
                        riskLevel = ov.riskLevel,
                        red = Mathf.Max(0f, ov.red),
                        white = Mathf.Max(0f, ov.white),
                        black = Mathf.Max(0f, ov.black),
                        pale = Mathf.Max(0f, ov.pale),
                        source = FourColorProfileSource.UserOverride,
                        isAuthoredEgo = hasBaseline && baseline.isAuthoredEgo
                    };
                }
                return;
            }

            if (hasBaseline)
            {
                apparelCache[def] = baseline;
            }
        }

        private static bool TryBuildBaselineApparel(
            ThingDef def,
            Dictionary<string, FourColorApparelDefault> defaultsMap,
            bool useDefaults,
            bool autoInfer,
            out ResolvedApparelProfile profile)
        {
            FourColorApparelExtension ext = def.GetModExtension<FourColorApparelExtension>();
            if (ext != null)
            {
                profile = new ResolvedApparelProfile
                {
                    riskLevel = ext.riskLevel,
                    red = Mathf.Max(0f, ext.redResistance),
                    white = Mathf.Max(0f, ext.whiteResistance),
                    black = Mathf.Max(0f, ext.blackResistance),
                    pale = Mathf.Max(0f, ext.paleResistance),
                    source = FourColorProfileSource.Authored,
                    isAuthoredEgo = true
                };
                return true;
            }

            CompPoperties_EgoSuit egoComp = def.comps?.OfType<CompPoperties_EgoSuit>().FirstOrDefault();
            if (egoComp != null)
            {
                profile = new ResolvedApparelProfile
                {
                    riskLevel = (LCRiskLevel)DamageUtils.LevelTag2Int(egoComp.level),
                    red = Mathf.Max(0f, egoComp.redResist),
                    white = Mathf.Max(0f, egoComp.whiteResist),
                    black = Mathf.Max(0f, egoComp.blackResist),
                    pale = Mathf.Max(0f, egoComp.paleResist),
                    source = FourColorProfileSource.Authored,
                    isAuthoredEgo = true
                };
                return true;
            }

            if (useDefaults && defaultsMap.TryGetValue(def.defName, out FourColorApparelDefault entry))
            {
                profile = new ResolvedApparelProfile
                {
                    riskLevel = entry.riskLevel,
                    red = Mathf.Max(0f, entry.red),
                    white = Mathf.Max(0f, entry.white),
                    black = Mathf.Max(0f, entry.black),
                    pale = Mathf.Max(0f, entry.pale),
                    source = FourColorProfileSource.DefaultTable,
                    isAuthoredEgo = false
                };
                return true;
            }

            if (autoInfer && TryInferApparelProfile(def, out profile))
            {
                return true;
            }

            profile = default;
            return false;
        }

        private static bool TryInferApparelProfile(ThingDef def, out ResolvedApparelProfile profile)
        {
            ThingDef stuff = def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null;
            float sharp = def.GetStatValueAbstract(RimWorld.StatDefOf.ArmorRating_Sharp, stuff);
            float blunt = def.GetStatValueAbstract(RimWorld.StatDefOf.ArmorRating_Blunt, stuff);
            float heat = def.GetStatValueAbstract(RimWorld.StatDefOf.ArmorRating_Heat, stuff);
            float maxArmor = Mathf.Max(sharp, Mathf.Max(blunt, heat));

            if (maxArmor < 0.30f)
            {
                profile = default;
                return false;
            }

            bool spacerPlus = def.techLevel >= TechLevel.Spacer;
            if (maxArmor >= 1.05f || (maxArmor >= 0.90f && spacerPlus))
            {
                profile = new ResolvedApparelProfile
                {
                    riskLevel = LCRiskLevel.WAW,
                    red = 0.8f,
                    white = 1.05f,
                    black = 1.05f,
                    pale = 1.6f,
                    source = FourColorProfileSource.AutoInferred,
                    isAuthoredEgo = false
                };
                return true;
            }

            if (maxArmor >= 0.70f || (maxArmor >= 0.55f && spacerPlus))
            {
                profile = new ResolvedApparelProfile
                {
                    riskLevel = LCRiskLevel.HE,
                    red = 0.85f,
                    white = 1.1f,
                    black = 1.1f,
                    pale = 1.8f,
                    source = FourColorProfileSource.AutoInferred,
                    isAuthoredEgo = false
                };
                return true;
            }

            profile = new ResolvedApparelProfile
            {
                riskLevel = LCRiskLevel.TETH,
                red = 0.95f,
                white = 1.2f,
                black = 1.2f,
                pale = 2.0f,
                source = FourColorProfileSource.AutoInferred,
                isAuthoredEgo = false
            };
            return true;
        }

        private static void BuildWeaponEntry(ThingDef def, Dictionary<string, FourColorWeaponOverride> overrideMap)
        {
            bool hasBaseline = TryBuildBaselineWeapon(def, out ResolvedWeaponProfile baseline);
            if (hasBaseline)
            {
                baselineWeaponCache[def] = baseline;
            }

            if (overrideMap.TryGetValue(def.defName, out FourColorWeaponOverride ov))
            {
                if (ov.damageType != FourColorDamageType.None)
                {
                    weaponCache[def] = new ResolvedWeaponProfile
                    {
                        damageType = ov.damageType,
                        secondaryDamageType = hasBaseline ? baseline.secondaryDamageType : FourColorDamageType.None,
                        riskLevel = ov.riskLevel,
                        damageMultiplier = Mathf.Max(0f, ov.damageMultiplier),
                        source = FourColorProfileSource.UserOverride,
                        isAuthoredEgo = hasBaseline && baseline.isAuthoredEgo,
                        guaranteedHit = hasBaseline && baseline.guaranteedHit
                    };
                }
                return;
            }

            if (hasBaseline)
            {
                weaponCache[def] = baseline;
            }
        }

        private static bool TryBuildBaselineWeapon(ThingDef def, out ResolvedWeaponProfile profile)
        {
            FourColorWeaponExtension ext = def.GetModExtension<FourColorWeaponExtension>();
            if (ext != null && ext.damageType != FourColorDamageType.None)
            {
                profile = new ResolvedWeaponProfile
                {
                    damageType = ext.damageType,
                    secondaryDamageType = ext.secondaryDamageType,
                    riskLevel = ext.riskLevel,
                    damageMultiplier = Mathf.Max(0f, ext.damageMultiplier),
                    source = FourColorProfileSource.Authored,
                    isAuthoredEgo = true,
                    guaranteedHit = ext.guaranteedHit
                };
                return true;
            }

            CompProperties_EgoWeapon comp = def.comps?.OfType<CompProperties_EgoWeapon>().FirstOrDefault();
            if (comp != null && comp.damageType != FourColorDamageType.None)
            {
                profile = new ResolvedWeaponProfile
                {
                    damageType = comp.damageType,
                    secondaryDamageType = FourColorDamageType.None,
                    riskLevel = (LCRiskLevel)DamageUtils.LevelTag2Int(comp.level),
                    damageMultiplier = Mathf.Max(0f, comp.damageMultiplier),
                    source = FourColorProfileSource.Authored,
                    isAuthoredEgo = true,
                    guaranteedHit = false
                };
                return true;
            }

            profile = default;
            return false;
        }

        private static void BuildRaceEntry(
            ThingDef def,
            Dictionary<FourColorRaceCategory, FourColorRaceCategoryDefault> categoryMap,
            Dictionary<string, FourColorRaceOverride> overrideMap)
        {
            ResolvedRaceProfile baseline = BuildBaselineRace(def, categoryMap);
            baselineRaceCache[def] = baseline;

            if (overrideMap.TryGetValue(def.defName, out FourColorRaceOverride ov))
            {
                raceCache[def] = new ResolvedRaceProfile
                {
                    targetKind = ov.targetKind,
                    red = Mathf.Max(0f, ov.red),
                    white = Mathf.Max(0f, ov.white),
                    black = Mathf.Max(0f, ov.black),
                    pale = Mathf.Max(0f, ov.pale),
                    source = FourColorProfileSource.UserOverride
                };
                return;
            }

            raceCache[def] = baseline;
        }

        private static ResolvedRaceProfile BuildBaselineRace(
            ThingDef def,
            Dictionary<FourColorRaceCategory, FourColorRaceCategoryDefault> categoryMap)
        {
            FourColorTargetExtension ext = def.GetModExtension<FourColorTargetExtension>();
            if (ext != null)
            {
                return new ResolvedRaceProfile
                {
                    targetKind = ext.targetKind,
                    red = Mathf.Max(0f, ext.redResistance),
                    white = Mathf.Max(0f, ext.whiteResistance),
                    black = Mathf.Max(0f, ext.blackResistance),
                    pale = Mathf.Max(0f, ext.paleResistance),
                    source = FourColorProfileSource.Authored
                };
            }

            FourColorRaceCategory cat = GetRaceCategory(def);
            if (categoryMap.TryGetValue(cat, out FourColorRaceCategoryDefault entry))
            {
                return new ResolvedRaceProfile
                {
                    targetKind = entry.targetKind,
                    red = Mathf.Max(0f, entry.red),
                    white = Mathf.Max(0f, entry.white),
                    black = Mathf.Max(0f, entry.black),
                    pale = Mathf.Max(0f, entry.pale),
                    source = FourColorProfileSource.DefaultTable
                };
            }

            return new ResolvedRaceProfile
            {
                targetKind = FourColorTargetKind.Default,
                red = 1f,
                white = 1f,
                black = 1f,
                pale = 1f,
                source = FourColorProfileSource.None
            };
        }

        public static bool IsEligibleApparel(ThingDef def) => def != null && def.IsApparel && def.apparel != null;

        public static bool IsEligibleWeapon(ThingDef def) => def != null && def.IsWeapon && def.equipmentType == EquipmentType.Primary;

        public static bool IsEligibleRace(ThingDef def) => def != null && def.race != null;

        public static FourColorRaceCategory GetRaceCategory(ThingDef def)
        {
            RaceProperties race = def?.race;
            if (race == null)
            {
                return FourColorRaceCategory.Humanlike;
            }

            if (race.IsMechanoid)
            {
                return FourColorRaceCategory.Mechanoid;
            }

            if (race.Insect)
            {
                return FourColorRaceCategory.Insect;
            }

            if (race.Animal)
            {
                return FourColorRaceCategory.Animal;
            }

            return FourColorRaceCategory.Humanlike;
        }

        public static bool TryGetApparelProfile(ThingDef def, out ResolvedApparelProfile profile)
        {
            if (!initialized)
            {
                Initialize();
            }

            if (def != null && apparelCache.TryGetValue(def, out profile))
            {
                return true;
            }

            profile = default;
            return false;
        }

        public static bool TryGetBaselineApparelProfile(ThingDef def, out ResolvedApparelProfile profile)
        {
            if (!initialized)
            {
                Initialize();
            }

            if (def != null && baselineApparelCache.TryGetValue(def, out profile))
            {
                return true;
            }

            profile = default;
            return false;
        }

        public static bool TryGetWeaponProfile(ThingDef def, out ResolvedWeaponProfile profile)
        {
            if (!initialized)
            {
                Initialize();
            }

            if (def != null && weaponCache.TryGetValue(def, out profile))
            {
                return profile.damageType != FourColorDamageType.None;
            }

            profile = default;
            return false;
        }

        public static bool TryGetBaselineWeaponProfile(ThingDef def, out ResolvedWeaponProfile profile)
        {
            if (!initialized)
            {
                Initialize();
            }

            if (def != null && baselineWeaponCache.TryGetValue(def, out profile))
            {
                return profile.damageType != FourColorDamageType.None;
            }

            profile = default;
            return false;
        }

        public static ResolvedRaceProfile GetRaceProfile(ThingDef def)
        {
            if (!initialized)
            {
                Initialize();
            }

            if (def != null && raceCache.TryGetValue(def, out ResolvedRaceProfile profile))
            {
                return profile;
            }

            return new ResolvedRaceProfile
            {
                targetKind = FourColorTargetKind.Default,
                red = 1f,
                white = 1f,
                black = 1f,
                pale = 1f,
                source = FourColorProfileSource.None
            };
        }

        public static ResolvedRaceProfile GetBaselineRaceProfile(ThingDef def)
        {
            if (!initialized)
            {
                Initialize();
            }

            if (def != null && baselineRaceCache.TryGetValue(def, out ResolvedRaceProfile profile))
            {
                return profile;
            }

            return new ResolvedRaceProfile
            {
                targetKind = FourColorTargetKind.Default,
                red = 1f,
                white = 1f,
                black = 1f,
                pale = 1f,
                source = FourColorProfileSource.None
            };
        }

        public static FourColorTargetKind GetRaceTargetKind(ThingDef def) => GetRaceProfile(def).targetKind;

        public static float GetRaceResistance(ThingDef def, FourColorDamageType damageType) => GetRaceProfile(def).ResistanceFor(damageType);

        public static float GetApparelQualityFactor(Thing apparel, bool isAuthoredEgo)
        {
            Setting_LCAnomalyCore settings = Setting_LCAnomalyCore_Main.Settings;
            if (settings == null || !settings.If_EnableQualityScaling)
            {
                return 1f;
            }

            if (isAuthoredEgo && !settings.If_ApplyQualityScalingToEGO)
            {
                return 1f;
            }

            if (apparel == null || !apparel.TryGetQuality(out QualityCategory qc))
            {
                return 1f;
            }

            switch (qc)
            {
                case QualityCategory.Awful: return 1.15f;
                case QualityCategory.Poor: return 1.07f;
                case QualityCategory.Normal: return 1.0f;
                case QualityCategory.Good: return 0.95f;
                case QualityCategory.Excellent: return 0.90f;
                case QualityCategory.Masterwork: return 0.85f;
                case QualityCategory.Legendary: return 0.75f;
                default: return 1.0f;
            }
        }

        public static float ApplyApparelQuality(float baseResistance, Thing apparel, bool isAuthoredEgo)
        {
            if (baseResistance <= 0f)
            {
                return 0f;
            }

            float factor = GetApparelQualityFactor(apparel, isAuthoredEgo);
            if (Mathf.Approximately(factor, 1f))
            {
                return baseResistance;
            }

            return Mathf.Max(0.1f, baseResistance * factor);
        }

        public static float GetWeaponQualityFactor(Thing weapon, bool isAuthoredEgo)
        {
            Setting_LCAnomalyCore settings = Setting_LCAnomalyCore_Main.Settings;
            if (settings == null || !settings.If_EnableQualityScaling)
            {
                return 1f;
            }

            if (isAuthoredEgo && !settings.If_ApplyQualityScalingToEGO)
            {
                return 1f;
            }

            if (weapon == null || !weapon.TryGetQuality(out QualityCategory qc))
            {
                return 1f;
            }

            switch (qc)
            {
                case QualityCategory.Awful: return 0.85f;
                case QualityCategory.Poor: return 0.93f;
                case QualityCategory.Normal: return 1.0f;
                case QualityCategory.Good: return 1.05f;
                case QualityCategory.Excellent: return 1.10f;
                case QualityCategory.Masterwork: return 1.18f;
                case QualityCategory.Legendary: return 1.28f;
                default: return 1.0f;
            }
        }

        public static bool HasApparelOverride(string defName)
        {
            var list = Setting_LCAnomalyCore_Main.Settings?.apparelOverrides;
            return list != null && list.Any(e => e != null && e.defName == defName);
        }

        public static bool HasWeaponOverride(string defName)
        {
            var list = Setting_LCAnomalyCore_Main.Settings?.weaponOverrides;
            return list != null && list.Any(e => e != null && e.defName == defName);
        }

        public static bool HasRaceOverride(string defName)
        {
            var list = Setting_LCAnomalyCore_Main.Settings?.raceOverrides;
            return list != null && list.Any(e => e != null && e.defName == defName);
        }

        public static void SetApparelOverride(ThingDef def, bool enabled, LCRiskLevel riskLevel, float red, float white, float black, float pale)
        {
            Setting_LCAnomalyCore settings = Setting_LCAnomalyCore_Main.Settings;
            if (def == null || settings == null)
            {
                return;
            }

            if (!enabled && !TryGetBaselineApparelProfile(def, out _))
            {
                settings.RemoveApparelOverride(def.defName);
            }
            else
            {
                FourColorApparelOverride entry = settings.GetOrCreateApparelOverride(def.defName);
                entry.enabled = enabled;
                entry.riskLevel = riskLevel;
                entry.red = Mathf.Clamp(red, 0f, 5f);
                entry.white = Mathf.Clamp(white, 0f, 5f);
                entry.black = Mathf.Clamp(black, 0f, 5f);
                entry.pale = Mathf.Clamp(pale, 0f, 5f);
            }

            RebuildCache();
        }

        public static void SetWeaponOverride(ThingDef def, FourColorDamageType damageType, LCRiskLevel riskLevel, float damageMultiplier)
        {
            Setting_LCAnomalyCore settings = Setting_LCAnomalyCore_Main.Settings;
            if (def == null || settings == null)
            {
                return;
            }

            if (damageType == FourColorDamageType.None && !TryGetBaselineWeaponProfile(def, out _))
            {
                settings.RemoveWeaponOverride(def.defName);
            }
            else
            {
                FourColorWeaponOverride entry = settings.GetOrCreateWeaponOverride(def.defName);
                entry.damageType = damageType;
                entry.riskLevel = riskLevel;
                entry.damageMultiplier = Mathf.Clamp(damageMultiplier, 0.1f, 10f);
            }

            RebuildCache();
        }

        public static void SetRaceOverride(ThingDef def, FourColorTargetKind targetKind, float red, float white, float black, float pale)
        {
            Setting_LCAnomalyCore settings = Setting_LCAnomalyCore_Main.Settings;
            if (def == null || settings == null)
            {
                return;
            }

            FourColorRaceOverride entry = settings.GetOrCreateRaceOverride(def.defName);
            entry.targetKind = targetKind;
            entry.red = Mathf.Clamp(red, 0f, 5f);
            entry.white = Mathf.Clamp(white, 0f, 5f);
            entry.black = Mathf.Clamp(black, 0f, 5f);
            entry.pale = Mathf.Clamp(pale, 0f, 5f);

            RebuildCache();
        }

        public static void RevertApparel(string defName)
        {
            Setting_LCAnomalyCore_Main.Settings?.RemoveApparelOverride(defName);
            RebuildCache();
        }

        public static void RevertWeapon(string defName)
        {
            Setting_LCAnomalyCore_Main.Settings?.RemoveWeaponOverride(defName);
            RebuildCache();
        }

        public static void RevertRace(string defName)
        {
            Setting_LCAnomalyCore_Main.Settings?.RemoveRaceOverride(defName);
            RebuildCache();
        }

        public static void RevertAllApparel()
        {
            Setting_LCAnomalyCore_Main.Settings?.apparelOverrides?.Clear();
            RebuildCache();
        }

        public static void RevertAllWeapons()
        {
            Setting_LCAnomalyCore_Main.Settings?.weaponOverrides?.Clear();
            RebuildCache();
        }

        public static void RevertAllRaces()
        {
            Setting_LCAnomalyCore_Main.Settings?.raceOverrides?.Clear();
            RebuildCache();
        }
    }
}
