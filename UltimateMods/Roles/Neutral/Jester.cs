using System.Linq;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UltimateMods.Roles
{
    [HarmonyPatch]
    public class Jester : RoleBase<Jester>
    {
        public static bool TriggerJesterWin = false;
        public static bool CanCallEmergency { get { return CustomRolesH.JesterCanEmergencyMeeting.getBool(); } }
        public static bool CanUseVents { get { return CustomRolesH.JesterCanUseVents.getBool(); } }
        public static bool CanSabotage { get { return CustomRolesH.JesterCanSabotage.getBool(); } }
        public static bool HasImpostorVision { get { return CustomRolesH.JesterHasImpostorVision.getBool(); } }
        public static bool HasTasks { get { return CustomRolesH.JesterMustFinishTasks.getBool(); } }
        public static int numCommonTasks { get { return CustomRolesH.JesterTasks.commonTasks; } }
        public static int numLongTasks { get { return CustomRolesH.JesterTasks.longTasks; } }
        public static int numShortTasks { get { return CustomRolesH.JesterTasks.shortTasks; } }

        public Jester()
        {
            TriggerJesterWin = false;
            RoleType = roleId = RoleType.Jester;
        }

        public void assignTasks()
        {
            player.GenerateAndAssignTasks(numCommonTasks, numShortTasks, numLongTasks);
        }

        [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.BeginCrewmate))]
        class BeginCrewmatePatch
        {
            public static void Postfix(ShipStatus __instance)
            {
                if (HasTasks)
                {
                    local.assignTasks();
                }
            }
        }

        public static bool TasksComplete(PlayerControl p)
        {
            int FinishedTasks = 0;
            int TasksCount = numCommonTasks + numLongTasks + numShortTasks;
            if (TasksCount == 0) return true;
            foreach (var task in p.Data.Tasks)
            {
                if (task.Complete)
                {
                    FinishedTasks++;
                }
            }
            return FinishedTasks >= TasksCount;
        }

        public override void OnMeetingStart() { }
        public override void OnMeetingEnd() { }
        public override void FixedUpdate() { }
        public override void OnKill(PlayerControl target) { }
        public override void OnDeath(PlayerControl killer = null) { }
        public override void HandleDisconnect(PlayerControl player, DisconnectReasons reason) { }

        public static void Clear()
        {
            players = new List<Jester>();
        }
    }
}