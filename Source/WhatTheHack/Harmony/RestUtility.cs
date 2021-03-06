﻿using Harmony;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using Verse.AI;
using WhatTheHack.Buildings;
using WhatTheHack.Storage;

namespace WhatTheHack.Harmony
{
    [HarmonyPatch(typeof(RestUtility), "FindBedFor")]
    [HarmonyPatch(new Type[] { typeof(Pawn), typeof(Pawn), typeof(bool), typeof(bool), typeof(bool) })]
    class RestUtility_FindBedFor
    {
        static bool Prefix(Pawn sleeper, Pawn traveler, ref Building_Bed __result)
        {
            /*
            if (sleeper.RaceProps.Animal && (__result is Building_MechanoidPlatform || __result is Building_HackingTable))
            {
                return false;
            }
            */

            if (!sleeper.RaceProps.IsMechanoid || !sleeper.IsHacked())
            {
                return true;
            }

            if (HealthAIUtility.ShouldSeekMedicalRest(sleeper))
            {
                if (!sleeper.IsHacked())
                {
                    if (sleeper.OnHackingTable())
                    {
                        __result = sleeper.CurrentBed();
                        return false;
                    }
                    else
                    {
                        __result = Utilities.GetAvailableHackingTable(traveler, sleeper);
                        return false;
                    }
                }
                else
                {
                    if (sleeper.OnBaseMechanoidPlatform())
                    {
                        __result = sleeper.CurrentBed();
                        return false;
                    }
                    else
                    {
                        __result = Utilities.GetAvailableMechanoidPlatform(traveler, sleeper);
                        return false;
                    }
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(RestUtility), "GetBedSleepingSlotPosFor")]
    class RestUtility_GetBedSleepingSlotPosFor
    {
        static bool Prefix(Pawn pawn, Building_Bed bed, ref IntVec3 __result)
        {
            if(bed is Building_BaseMechanoidPlatform)
            {
                __result = bed.GetSleepingSlotPos(Building_BaseMechanoidPlatform.SLOTINDEX);
                return false;
            }
            if (bed is Building_HackingTable)
            {
                __result = bed.GetSleepingSlotPos(Building_HackingTable.SLOTINDEX);
                return false;
            }
            return true;
        }
    }
    //Make sure only mechanoids can use hacking table and mechanoid platforms a bed
    [HarmonyPatch(typeof(RestUtility), "CanUseBedEver")]
    class RestUtility_CanUseBedEver
    {
        static bool Prefix(ref bool __result, Pawn p, ThingDef bedDef)
        {
            if(!p.RaceProps.IsMechanoid && (bedDef == WTH_DefOf.WTH_HackingTable || bedDef == WTH_DefOf.WTH_MechanoidPlatform || bedDef == WTH_DefOf.WTH_PortableChargingPlatform)){
                __result = false;
                return false;
            }
            return true;
        }
    }
    //"Wake up" mechanoids when forming a caravan
    [HarmonyPatch(typeof(RestUtility), "WakeUp")]
    class RestUtility_WakeUp
    {
        static void Postfix(ref Pawn p)
        {
            Pawn targetPawn = null;

            if (p.CurJob != null && p.CurJob.targetA != null && p.CurJob.targetA.HasThing && p.CurJob.targetA.Thing is Pawn)
            {
                targetPawn = p.CurJob.targetA.Thing as Pawn;
            }
            else
            {
                return;
            }

            if(targetPawn.jobs.curJob.def == WTH_DefOf.WTH_Mechanoid_Rest)
            {
                targetPawn.jobs.EndCurrentJob(JobCondition.InterruptForced, false);
                ExtendedPawnData pawnData = Base.Instance.GetExtendedDataStorage().GetExtendedDataFor(targetPawn);
                pawnData.isActive = true;
            }
        }
    }
    [HarmonyPatch(typeof(RestUtility), "CurrentBed")]
    class RestUtility_CurrentBed
    {
        static bool Prefix(Pawn p, ref Building_Bed __result)
        {
            if (!p.RaceProps.IsMechanoid || p.Map == null)
            {
                return true;
            }
            if (p.jobs.curDriver == null || ((p.CurJob.def != WTH_DefOf.WTH_Mechanoid_Rest) && p.jobs.posture != PawnPosture.LayingInBed))
            {
                return true;              
            }
            List<Thing> thingList = p.Position.GetThingList(p.Map);



            if (!p.IsHacked())
            {
                Building_HackingTable hackingTable = null;
                for (int i = 0; i < thingList.Count; i++)
                {
                    hackingTable = (thingList[i] as Building_HackingTable);
                    if (hackingTable != null)
                    {
                        break;
                    }
                }
                if (hackingTable == null)
                {
                    return true;
                }

                if (hackingTable.GetCurOccupant(Building_HackingTable.SLOTINDEX) == p)
                {
                    __result = hackingTable;
                    return false;
                }
            }
            else
            {
                Building_BaseMechanoidPlatform mechanoidPlatform = null;
                for (int i = 0; i < thingList.Count; i++)
                {
                    mechanoidPlatform = (thingList[i] as Building_BaseMechanoidPlatform);
                    if (mechanoidPlatform != null)
                    {
                        break;
                    }
                }
                if (mechanoidPlatform == null)
                {
                    return true;
                }

                if (mechanoidPlatform.GetCurOccupant(Building_BaseMechanoidPlatform.SLOTINDEX) == p)
                {
                    __result = mechanoidPlatform;
                    return false;
                }
            }

            return true;
        }
    }


}
