﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HugsLib;
using WhatTheHack.Storage;
using HugsLib.Utils;
using HugsLib.Settings;
using Verse;
using RimWorld;
using Harmony;
using WhatTheHack.Comps;
using WhatTheHack.Recipes;

namespace WhatTheHack
{
    public class Base : ModBase
    {
        public static Base Instance { get; private set; }
        ExtendedDataStorage _extendedDataStorage;


        //settings
        internal static SettingHandle<String> tabsHandler;
        internal static SettingHandle<bool> maintenanceDecayEnabled;
        internal static SettingHandle<int> hackedMechChance;
        internal static SettingHandle<int> minHackedMechPoints;
        internal static SettingHandle<int> maxHackedMechPoints;

        internal static SettingHandle<int> failureChanceNothing;
        internal static SettingHandle<int> failureChanceCauseRaid;
        internal static SettingHandle<int> failureChanceShootRandomDirection;
        internal static SettingHandle<int> failureChanceHealToStanding;
        internal static SettingHandle<int> failureChanceHackPoorly;
        internal static SettingHandle<Dict2DRecordHandler> factionRestrictions;

        internal static List<ThingDef> allMechs;
        internal static List<String> allFactionNames;

        //temp accessible storage
        internal float daysOfFuel = 0;
        internal string daysOfFuelReason = "";

        //List<String> tabNames = new List<String>();

        public override string ModIdentifier
        {
            get { return "WhatTheHack"; }
        }
        public Base()
        {
            Instance = this;
        }
        public override void DefsLoaded()
        {
            base.DefsLoaded();
            Predicate<ThingDef> isMech = (ThingDef d) => d.race != null && d.race.IsMechanoid;
            Predicate<FactionDef> isHackingFaction = (FactionDef d) => !d.isPlayer && d != FactionDefOf.Mechanoid && d != FactionDefOf.Insect;
            allMechs = (from td in DefDatabase<ThingDef>.AllDefs where isMech(td) select td).ToList();
            allFactionNames = (from td  in DefDatabase<FactionDef>.AllDefs
                                            where isHackingFaction(td)
                                            select td.defName).ToList();
            tabsHandler = Settings.GetHandle<String>("tabs", "WTH_FactionRestrictions_Label".Translate(), "WTH_FactionRestrictions_Description".Translate(), allFactionNames.First());
            tabsHandler.CustomDrawer = rect => { return false; };
            factionRestrictions = Settings.GetHandle<Dict2DRecordHandler>("factionRestrictions", "", "", null);
            factionRestrictions.CustomDrawer = rect => { return GUIDrawUtility.CustomDrawer_MatchingPawns_active(rect, factionRestrictions, allMechs, allFactionNames, tabsHandler, "WTH_FactionRestrictions_OK".Translate(), "WTH_FactionRestrictions_NOK".Translate()); };


            hackedMechChance = Settings.GetHandle<int>("hackedMechChance", "WTH_HackedMechChance_Title".Translate(), "WTH_HackedMechChance_Description".Translate(), 60, Validators.IntRangeValidator(0,100));
            maxHackedMechPoints = Settings.GetHandle<int>("maxHackedMechPoints", "WTH_MaxHackedMechPoints_Title".Translate(), "WTH_MaxHackedMechPoints_Description".Translate(), 50, Validators.IntRangeValidator(0,500));
            minHackedMechPoints = Settings.GetHandle<int>("minHackedMechPoints", "WTH_MinHackedMechPoints_Title".Translate(), "WTH_MinHackedMechPoints_Description".Translate(), 0, Validators.IntRangeValidator(0, 500));

            failureChanceNothing = Settings.GetHandle<int>("failureChanceNothing", "WTH_FailureChance_Nothing_Title".Translate(), "WTH_FailureChance_Nothing_Description".Translate(), 70);
            failureChanceCauseRaid = Settings.GetHandle<int>("failureChanceCauseRaid", "WTH_FailureChance_CauseRaid_Title".Translate(), "WTH_FailureChance_CauseRaid_Description".Translate(), 5);
            failureChanceShootRandomDirection = Settings.GetHandle<int>("failureChanceShootRandomDirection", "WTH_FailureChance_ShootRandomDirection_Title".Translate(), "WTH_FailureChance_ShootRandomDirection_Description".Translate(), 10);
            failureChanceHealToStanding = Settings.GetHandle<int>("failureChanceHealToStanding", "WTH_FailureChance_HealToStanding_Title".Translate(), "WTH_FailureChance_HealToStanding_Description".Translate(), 5);
            failureChanceHackPoorly = Settings.GetHandle<int>("failureChanceHackPoorly", "WTH_FailureChance_HackPoorly_Title".Translate(), "WTH_FailureChance_HackPoorly_Description".Translate(), 10);
            maintenanceDecayEnabled = Settings.GetHandle<bool>("maintenanceDecayEnabled", "WTH_MaintenanceDedayEnabled_Title".Translate(), "WTH_MaintenanceDedayEnabled_Description".Translate(), true);

            factionRestrictions = GetDefaultForFactionRestrictions(factionRestrictions, allMechs, allFactionNames);
            GenerateImpliedRecipeDefs();
            DefDatabase<ThingDef>.ResolveAllReferences(true);
            SetMechMarketValue();
        }

        private static void GenerateImpliedRecipeDefs()
        {
            IEnumerable<RecipeDef> extraRecipeDefs = ImpliedRecipeDefs();
            foreach (RecipeDef td in extraRecipeDefs)
            {
                DefGenerator.AddImpliedDef<RecipeDef>(td);
            }
        }

        private static IEnumerable<RecipeDef> ImpliedRecipeDefs()
        {
            foreach (ThingDef def in from d in DefDatabase<ThingDef>.AllDefs
                                     where d.HasComp(typeof(CompMountable))
                                     select d)
            {
                RecipeDef r = new RecipeDef();
                r.defName = "WTH_Mount_" + def.defName;
                r.label = "WTH_Mount".Translate(new object[] { def.label });
                r.jobString = "WTH_Mount_Jobstring".Translate(new object[] { def.label });
                r.workerClass = typeof(Recipe_MountTurret);
                r.appliedOnFixedBodyParts = new List<BodyPartDef>() { WTH_DefOf.Reactor };
                r.anesthetize = false;
                r.effectWorking = DefDatabase<EffecterDef>.AllDefs.FirstOrDefault((EffecterDef ed) => ed.defName == "Repair");
                r.surgerySuccessChanceFactor = 99999f;
                r.modContentPack = def.modContentPack;
                r.workAmount = 2000f;
                r.addsHediff = WTH_DefOf.WTH_MountedTurret;
                IngredientCount ic = new IngredientCount();
                ic.SetBaseCount(1f);
                ic.filter.SetAllow(def, true);
                r.ingredients.Add(ic);                
                r.fixedIngredientFilter.SetAllow(def, true);
                r.recipeUsers = new List<ThingDef>();
                r.modExtensions = new List<DefModExtension>()
                {
                    new DefModExtension_Recipe(){
                        requireBed = true,
                        requiredHediff = WTH_DefOf.WTH_TurretModule
                    }
                };
                foreach (ThingDef current in DefDatabase<ThingDef>.AllDefs.Where((ThingDef d) => d.category == ThingCategory.Pawn && d.race.IsMechanoid))
                {
                    r.recipeUsers.Add(current);
                }
                r.ResolveReferences();
                yield return r;
            }
        }
        private static void SetMechMarketValue()
        {
            foreach (PawnKindDef kind in (from kd in DefDatabase<PawnKindDef>.AllDefs where kd.RaceProps.IsMechanoid select kd))
            {
                if (kind.race.BaseMarketValue < 1.0f && kind.combatPower < 10000f)
                {
                    kind.race.BaseMarketValue = kind.combatPower * 3.0f;
                }
            }
        }

        public static SettingHandle<Dict2DRecordHandler> GetDefaultForFactionRestrictions(SettingHandle<Dict2DRecordHandler> factionRestrictions, List<ThingDef> allMechs, List<string> allFactionNames)
        {
            factionRestrictions.Value = GetDefaultForFactionRestrictions(factionRestrictions.Value, allMechs, allFactionNames);
            return factionRestrictions;
        }
        public static Dict2DRecordHandler GetDefaultForFactionRestrictions(Dict2DRecordHandler factionRestrictionsDict, List<ThingDef> allMechs, List<string> allFactionNames)
        {
            if (factionRestrictionsDict == null)
            {
                factionRestrictionsDict = new Dict2DRecordHandler();
            }

            if (factionRestrictionsDict.InnerList == null)
            {
                factionRestrictionsDict.InnerList = new Dictionary<String, Dictionary<String, Record>>();
            }
            foreach (FactionDef factionDef in from td in DefDatabase<FactionDef>.AllDefs
                                              where allFactionNames.Contains(td.defName)
                                              select td)
            {
                if (!factionRestrictionsDict.InnerList.ContainsKey(factionDef.defName))
                {
                    factionRestrictionsDict.InnerList.Add(factionDef.defName, new Dictionary<string, Record>());
                }
            }
            foreach (string name in allFactionNames)
            {
                Dictionary<string, Record> selection = factionRestrictionsDict.InnerList[name];
                GUIDrawUtility.FilterSelection(ref selection, allMechs, name);
                factionRestrictionsDict.InnerList[name] = selection;
            }
            return factionRestrictionsDict;
        }


        public override void WorldLoaded()
        {
            _extendedDataStorage = UtilityWorldObjectManager.GetUtilityWorldObject<ExtendedDataStorage>();
            base.WorldLoaded();
            /*
            foreach (Map map in Find.Maps)
            {
                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned.Where((Pawn p) => p.health != null && p.RaceProps.IsMechanoid && p.def.comps.Any<CompProperties>()))
                {
                    ThingWithComps twc = (ThingWithComps)pawn;
                    RemoveComps(ref twc);
                }
            }
            */

        }        
        //Removes comps if necessary
        //Explanation: Vanilla doesn't support conditional comps. Example: For the repair module, we only want mechs to have comp_refuelable when the mech has one installed. 
        //So to support conditional comps like this, we first allow all comps to be loaded. Then we remove the comps for which the condition doesn't hold. In this case, the refuelable comp for the repair module is
        //removed when a mechanoid doens't have one installed. 
        public static void RemoveComps(ref ThingWithComps __instance)
        {

                Pawn pawn = (Pawn)__instance;
                List<ThingComp> comps = Traverse.Create(__instance).Field("comps").GetValue<List<ThingComp>>();
                List<ThingComp> newComps = new List<ThingComp>();
                foreach (ThingComp thingComp in comps)
                {
                    CompProperties_Refuelable refuelableProps = thingComp.props as CompProperties_Refuelable;
                    if (refuelableProps == null || !refuelableProps.fuelFilter.Allows(WTH_DefOf.WTH_MechanoidParts))
                    {
                        newComps.Add(thingComp);
                    }
                    if (refuelableProps != null
                        && refuelableProps.fuelFilter.Allows(WTH_DefOf.WTH_MechanoidParts)
                        && pawn.IsHacked()
                        && (pawn.health.hediffSet.HasHediff(WTH_DefOf.WTH_RepairModule)))
                    {
                        newComps.Add(thingComp);
                    }
                }
                Traverse.Create(__instance).Field("comps").SetValue(newComps);    
        }

        public ExtendedDataStorage GetExtendedDataStorage()
        {
            return _extendedDataStorage;
        }

    }
}
