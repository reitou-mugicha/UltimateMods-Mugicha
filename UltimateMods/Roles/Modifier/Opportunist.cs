using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using UltimateMods.Modules;

namespace UltimateMods.Roles
{
    [HarmonyPatch]
    public class Opportunist : ModifierBase<Opportunist>
    {
        public static string Postfix { get { return ModTranslation.getString("OpportunistPostfix"); } }

        public static List<PlayerControl> candidates
        {
            get
            {
                List<PlayerControl> validPlayers = new();

                foreach (var player in PlayerControl.AllPlayerControls)
                {
                    if (!player.hasModifier(ModifierType.Opportunist))
                        validPlayers.Add(player);
                }

                return validPlayers;
            }
        }

        public Opportunist()
        {
            ModType = modId = ModifierType.Opportunist;
        }

        public static void Clear()
        {
            players = new List<Opportunist>();
        }

        public override void OnMeetingStart() { }
        public override void OnMeetingEnd() { }
        public override void FixedUpdate() { }
        public override void OnKill(PlayerControl target) { }
        public override void OnDeath(PlayerControl killer = null) { }
        public override void HandleDisconnect(PlayerControl player, DisconnectReasons reason) { }
    }
}