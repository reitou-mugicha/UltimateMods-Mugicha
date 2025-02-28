using HarmonyLib;
using UltimateMods.Roles;
using static UltimateMods.GameHistory;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using System.Text;
using UltimateMods.Modules;
using UltimateMods.Utilities;
using static UltimateMods.CustomRolesH;
using static UltimateMods.ColorDictionary;

namespace UltimateMods.EndGame
{
    public enum CustomGameOverReason
    {
        JesterExiled = 10,

        SabotageReactor,
        SabotageO2,
        ForceEnd,
        EveryoneLose
    }

    public enum FinalStatus
    {
        Alive, // 生存
        Sabotage, // サボ
        Reactor, // リアクターサボ
        Exiled, // 追放
        Misfire, // 誤爆
        Suicide, // 自殺
        Dead, // 死亡(キル)
        LackO2, // 酸素不足
        Disconnected // 切断
    }

    public enum WinCondition
    {
        Default,

        CrewmateWin,
        ImpostorWin,
        JesterWin,
        OpportunistWin,

        ForceEnd,
        EveryoneLose
    }

    static class AdditionalTempData
    {
        public static WinCondition winCondition = WinCondition.Default;
        public static List<WinCondition> additionalWinConditions = new ();
        public static List<PlayerRoleInfo> playerRoles = new ();
        public static GameOverReason gameOverReason;

        public static void clear()
        {
            playerRoles.Clear();
            additionalWinConditions.Clear();
            winCondition = WinCondition.Default;
        }
        internal class PlayerRoleInfo
        {
            public string WinOrLose { get; set; }
            public string PlayerName { get; set; }
            public List<RoleInfo> Roles { get; set; }
            public string RoleString { get; set; }
            public int TasksCompleted { get; set; }
            public int TasksTotal { get; set; }
            public FinalStatus Status { get; set; }
            public int PlayerId { get; set; }
        }
    }

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
    public static class OnGameEndPatch
    {
        public static void Prefix(AmongUsClient __instance, [HarmonyArgument(0)] ref EndGameResult endGameResult)
        {
            AdditionalTempData.gameOverReason = endGameResult.GameOverReason;
            if ((int)endGameResult.GameOverReason >= 10) endGameResult.GameOverReason = GameOverReason.ImpostorByKill;
        }

        public static void Postfix(AmongUsClient __instance, [HarmonyArgument(0)] ref EndGameResult endGameResult)
        {
            var GameOverReason = AdditionalTempData.gameOverReason;
            AdditionalTempData.clear();

            var excludeRoles = new RoleType[] { };
            foreach (var p in GameData.Instance.AllPlayers.GetFastEnumerator())
            {
                var roles = RoleInfo.getRoleInfoForPlayer(p.Object, excludeRoles, includeHidden: true);
                var (tasksCompleted, tasksTotal) = TasksHandler.taskInfo(p);
                var finalStatus = finalStatuses[p.PlayerId] =
                    p.Disconnected == true ? FinalStatus.Disconnected :
                    finalStatuses.ContainsKey(p.PlayerId) ? finalStatuses[p.PlayerId] :
                    p.IsDead == true ? FinalStatus.Dead :
                    GameOverReason == (GameOverReason)CustomGameOverReason.SabotageReactor && !p.Role.IsImpostor ? FinalStatus.Reactor :
                    GameOverReason == (GameOverReason)CustomGameOverReason.SabotageO2 && !p.Role.IsImpostor ? FinalStatus.LackO2 :
                    FinalStatus.Alive;

                if (GameOverReason == GameOverReason.HumansByTask && p.Object.IsCrew()) tasksCompleted = tasksTotal;

                AdditionalTempData.playerRoles.Add(new AdditionalTempData.PlayerRoleInfo()
                {
                    PlayerName = p.PlayerName,
                    PlayerId = p.PlayerId,
                    // NameSuffix = Lovers.getIcon(p.Object),
                    Roles = roles,
                    RoleString = RoleInfo.GetRolesString(p.Object, true, excludeRoles, true),
                    TasksTotal = tasksTotal,
                    TasksCompleted = tasksCompleted,
                    Status = finalStatus,
                });
            }

            List<PlayerControl> notWinners = new();
            notWinners.AddRange(Jester.allPlayers);
            notWinners.AddRange(Opportunist.allPlayers);

            List<WinningPlayerData> winnersToRemove = new();
            foreach (WinningPlayerData winner in TempData.winners.GetFastEnumerator())
            {
                if (notWinners.Any(x => x.Data.PlayerName == winner.PlayerName)) winnersToRemove.Add(winner);
            }
            foreach (var winner in winnersToRemove) TempData.winners.Remove(winner);

            bool JesterWin = Jester.exists && GameOverReason == (GameOverReason)CustomGameOverReason.JesterExiled;

            bool ForceEnd = KeyboardClass.triggerForceEnd;
            bool SaboWin = GameOverReason == GameOverReason.ImpostorBySabotage;
            bool EveryoneLose = AdditionalTempData.playerRoles.All(x => x.Status != FinalStatus.Alive);

            if (JesterWin)
            {
                TempData.winners = new Il2CppSystem.Collections.Generic.List<WinningPlayerData>();
                foreach (var jester in Jester.players)
                {
                    WinningPlayerData wpd = new WinningPlayerData(jester.player.Data);
                    TempData.winners.Add(wpd);
                    jester.player.Data.IsDead = true;
                }
                AdditionalTempData.winCondition = WinCondition.JesterWin;
            }

            else if (ForceEnd)
            {
                TempData.winners = new Il2CppSystem.Collections.Generic.List<WinningPlayerData>();
                foreach (PlayerControl p in PlayerControl.AllPlayerControls)
                {
                    if (p)
                    {
                        WinningPlayerData wpd = new(p.Data);
                        TempData.winners.Add(wpd);
                    }
                }
                AdditionalTempData.winCondition = WinCondition.ForceEnd;
            }

            else if (EveryoneLose)
            {
                TempData.winners = new Il2CppSystem.Collections.Generic.List<WinningPlayerData>();
                AdditionalTempData.winCondition = WinCondition.EveryoneLose;
            }

            if (!SaboWin)
            {
                bool oppWin = false;
                foreach (var p in Opportunist.livingPlayers)
                {
                    if (!TempData.winners.ToArray().Any(x => x.PlayerName == p.Data.PlayerName))
                        TempData.winners.Add(new WinningPlayerData(p.Data));
                    oppWin = true;
                }
                if (oppWin)
                    AdditionalTempData.additionalWinConditions.Add(WinCondition.OpportunistWin);
            }

            foreach (WinningPlayerData wpd in TempData.winners)
            {
                wpd.IsDead = wpd.IsDead || AdditionalTempData.playerRoles.Any(x => x.PlayerName == wpd.PlayerName && x.Status != FinalStatus.Alive);
            }

            // Reset Settings
            RPCProcedure.ResetVariables();
        }
    }

    [HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.SetEverythingUp))]
    public class EndGameManagerSetUpPatch
    {
        public static void Postfix(EndGameManager __instance)
        {
            // Delete and readd PoolablePlayers always showing the name and role of the player
            foreach (PoolablePlayer pb in __instance.transform.GetComponentsInChildren<PoolablePlayer>())
            {
                UnityEngine.Object.Destroy(pb.gameObject);
            }
            int num = Mathf.CeilToInt(7.5f);
            List<WinningPlayerData> list = TempData.winners.ToArray().ToList().OrderBy(delegate (WinningPlayerData b)
            {
                if (!b.IsYou)
                {
                    return 0;
                }
                return -1;
            }).ToList<WinningPlayerData>();
            for (int i = 0; i < list.Count; i++)
            {
                WinningPlayerData winningPlayerData2 = list[i];
                int num2 = (i % 2 == 0) ? -1 : 1;
                int num3 = (i + 1) / 2;
                float num4 = (float)num3 / (float)num;
                float num5 = Mathf.Lerp(1f, 0.75f, num4);
                float num6 = (float)((i == 0) ? -8 : -1);
                PoolablePlayer poolablePlayer = UnityEngine.Object.Instantiate<PoolablePlayer>(__instance.PlayerPrefab, __instance.transform);
                poolablePlayer.transform.localPosition = new Vector3(1f * (float)num2 * (float)num3 * num5, FloatRange.SpreadToEdges(-1.125f, 0f, num3, num), num6 + (float)num3 * 0.01f) * 0.9f;
                float num7 = Mathf.Lerp(1f, 0.65f, num4) * 0.9f;
                Vector3 vector = new Vector3(num7, num7, 1f);
                poolablePlayer.transform.localScale = vector;
                poolablePlayer.UpdateFromPlayerOutfit((GameData.PlayerOutfit)winningPlayerData2, PlayerMaterial.MaskType.ComplexUI, winningPlayerData2.IsDead, true);
                if (winningPlayerData2.IsDead)
                {
                    poolablePlayer.cosmetics.currentBodySprite.BodySprite.sprite = poolablePlayer.cosmetics.currentBodySprite.GhostSprite;
                    poolablePlayer.SetDeadFlipX(i % 2 == 0);
                }
                else
                {
                    poolablePlayer.SetFlipX(i % 2 == 0);
                }

                poolablePlayer.cosmetics.nameText.color = Color.white;
                poolablePlayer.cosmetics.nameText.transform.localScale = new Vector3(1f / vector.x, 1f / vector.y, 1f / vector.z);
                poolablePlayer.cosmetics.nameText.transform.localPosition = new Vector3(poolablePlayer.cosmetics.nameText.transform.localPosition.x, poolablePlayer.cosmetics.nameText.transform.localPosition.y, -15f);
                poolablePlayer.cosmetics.nameText.text = winningPlayerData2.PlayerName;

                foreach (var data in AdditionalTempData.playerRoles)
                {
                    if (data.PlayerName != winningPlayerData2.PlayerName) continue;
                    var roles =
                    poolablePlayer.cosmetics.nameText.text += $"\n{string.Join("\n", data.Roles.Select(x => Helpers.cs(x.color, x.Name)))}";
                }
            }

            // Additional code
            GameObject bonusTextObject = UnityEngine.Object.Instantiate(__instance.WinText.gameObject);
            bonusTextObject.transform.position = new Vector3(__instance.WinText.transform.position.x, __instance.WinText.transform.position.y - 0.5f, __instance.WinText.transform.position.z);
            bonusTextObject.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
            TMPro.TMP_Text textRenderer = bonusTextObject.GetComponent<TMPro.TMP_Text>();
            textRenderer.text = "";

            string bonusText = "";

            if (AdditionalTempData.winCondition == WinCondition.JesterWin)
            {
                bonusText = ModTranslation.getString("Jester");
                textRenderer.color = JesterPink;
                __instance.BackgroundBar.material.SetColor("_Color", JesterPink);
            }
            else if (AdditionalTempData.gameOverReason == GameOverReason.HumansByTask || AdditionalTempData.gameOverReason == GameOverReason.HumansByVote)
            {
                bonusText = ModTranslation.getString("Crewmate");
                textRenderer.color = CrewmateBlue;
            }
            else if (AdditionalTempData.gameOverReason == GameOverReason.ImpostorByKill || AdditionalTempData.gameOverReason == GameOverReason.ImpostorByVote || AdditionalTempData.gameOverReason == GameOverReason.ImpostorBySabotage)
            {
                bonusText = ModTranslation.getString("Impostor");
                textRenderer.color = ImpostorRed;
            }
            else if (AdditionalTempData.winCondition == WinCondition.EveryoneLose)
            {
                bonusText = ModTranslation.getString("Everyone");
                textRenderer.color = Palette.DisabledGrey;
                __instance.BackgroundBar.material.SetColor("_Color", Palette.DisabledGrey);
            }
            else if (AdditionalTempData.winCondition == WinCondition.ForceEnd)
            {
                bonusText = ModTranslation.getString("ForceEnd");
                textRenderer.color = Palette.DisabledGrey;
                __instance.BackgroundBar.material.SetColor("_Color", Palette.DisabledGrey);
            }

            string extraText = "";
            foreach (WinCondition w in AdditionalTempData.additionalWinConditions)
            {
                switch (w)
                {
                    case WinCondition.OpportunistWin:
                        extraText += ModTranslation.getString("OpportunistExtra");
                        break;
                    default:
                        break;
                }
            }

            if (extraText.Length > 0)
            {
                textRenderer.text = string.Format(ModTranslation.getString(bonusText + "Extra"), extraText);
            }
            else
            {
                textRenderer.text = ModTranslation.getString(bonusText);
            }

            if (AdditionalTempData.gameOverReason == (GameOverReason)CustomGameOverReason.SabotageO2)
            {
                textRenderer.text += ($"\n" + ModTranslation.getString("O2Win"));
            }
            else if (AdditionalTempData.gameOverReason == (GameOverReason)CustomGameOverReason.SabotageReactor)
            {
                textRenderer.text += ($"\n" + ModTranslation.getString("ReactorWin"));
            }

            if (MapOptions.showRoleSummary)
            {
                var position = Camera.main.ViewportToWorldPoint(new Vector3(0f, 1f, Camera.main.nearClipPlane));
                GameObject roleSummary = UnityEngine.Object.Instantiate(__instance.WinText.gameObject);
                roleSummary.transform.position = new Vector3(__instance.Navigation.ExitButton.transform.position.x + 0.1f, position.y - 0.1f, -14f);
                roleSummary.transform.localScale = new Vector3(1f, 1f, 1f);

                var roleSummaryText = new StringBuilder();
                roleSummaryText.AppendLine(ModTranslation.getString("roleSummaryText"));
                AdditionalTempData.playerRoles.Sort((x, y) =>
                {
                    RoleInfo roleX = x.Roles.FirstOrDefault();
                    RoleInfo roleY = y.Roles.FirstOrDefault();
                    RoleType idX = roleX == null ? RoleType.NoRole : roleX.roleType;
                    RoleType idY = roleY == null ? RoleType.NoRole : roleY.roleType;

                    if (x.Status == y.Status)
                    {
                        if (idX == idY)
                        {
                            return x.PlayerName.CompareTo(y.PlayerName);
                        }
                        return idX.CompareTo(idY);
                    }
                    return x.Status.CompareTo(y.Status);
                });

                TMPro.TMP_Text roleSummaryTextMesh = roleSummary.GetComponent<TMPro.TMP_Text>();
                roleSummaryTextMesh.alignment = TMPro.TextAlignmentOptions.TopLeft;
                roleSummaryTextMesh.color = Color.white;
                roleSummaryTextMesh.outlineWidth *= 1.2f;
                roleSummaryTextMesh.fontSizeMin = 1.25f;
                roleSummaryTextMesh.fontSizeMax = 1.25f;
                roleSummaryTextMesh.fontSize = 1.25f;

                var roleSummaryTextMeshRectTransform = roleSummaryTextMesh.GetComponent<RectTransform>();
                roleSummaryTextMeshRectTransform.anchoredPosition = new Vector2(position.x + 3.5f, position.y - 0.1f);
                roleSummaryTextMesh.text = roleSummaryText.ToString();
            }
            AdditionalTempData.clear();
        }
    }

    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.CheckEndCriteria))]
    class CheckEndCriteriaPatch
    {
        public static bool Prefix(ShipStatus __instance)
        {
            if (!GameData.Instance) return false;
            if (DestroyableSingleton<TutorialManager>.InstanceExists) // InstanceExists | Don't check Custom Criteria when in Tutorial
                return true;
            var statistics = new PlayerStatistics(__instance);
            if (CheckAndEndGameForJesterWin(__instance)) return false;
            if (CheckAndEndGameForSabotageWin(__instance)) return false;
            if (CheckAndEndGameForTaskWin(__instance)) return false;
            if (CheckAndEndGameForForceEnd(__instance)) return false;
            if (CheckAndEndGameForImpostorWin(__instance, statistics)) return false;
            if (CheckAndEndGameForCrewmateWin(__instance, statistics)) return false;
            return false;
        }

        private static bool CheckAndEndGameForJesterWin(ShipStatus __instance)
        {
            if (Jester.TriggerJesterWin)
            {
                __instance.enabled = false;
                ShipStatus.RpcEndGame((GameOverReason)CustomGameOverReason.JesterExiled, false);
                return true;
            }
            return false;
        }

        private static bool CheckAndEndGameForForceEnd(ShipStatus __instance)
        {
            if (KeyboardClass.triggerForceEnd)
            {
                __instance.enabled = false;
                ShipStatus.RpcEndGame((GameOverReason)CustomGameOverReason.ForceEnd, false);
                KeyboardClass.triggerForceEnd = false;
                return true;
            }
            return false;
        }

        private static bool CheckAndEndGameForSabotageWin(ShipStatus __instance)
        {
            if (MapUtilities.Systems == null) return false;
            var systemType = MapUtilities.Systems.ContainsKey(SystemTypes.LifeSupp) ? MapUtilities.Systems[SystemTypes.LifeSupp] : null;
            if (systemType != null)
            {
                LifeSuppSystemType lifeSuppSystemType = systemType.TryCast<LifeSuppSystemType>();
                if (lifeSuppSystemType != null && lifeSuppSystemType.Countdown < 0f)
                {
                    EndGameForO2(__instance);
                    lifeSuppSystemType.Countdown = 10000f;
                    return true;
                }
            }
            var systemType2 = MapUtilities.Systems.ContainsKey(SystemTypes.Reactor) ? MapUtilities.Systems[SystemTypes.Reactor] : null;
            if (systemType2 == null)
            {
                systemType2 = MapUtilities.Systems.ContainsKey(SystemTypes.Laboratory) ? MapUtilities.Systems[SystemTypes.Laboratory] : null;
            }
            if (systemType2 != null)
            {
                ICriticalSabotage criticalSystem = systemType2.TryCast<ICriticalSabotage>();
                if (criticalSystem != null && criticalSystem.Countdown < 0f)
                {
                    EndGameForReactor(__instance);
                    criticalSystem.ClearSabotage();
                    return true;
                }
            }
            return false;
        }

        private static bool CheckAndEndGameForTaskWin(ShipStatus __instance)
        {
            if (GameData.Instance.TotalTasks > 0 && GameData.Instance.TotalTasks <= GameData.Instance.CompletedTasks)
            {
                __instance.enabled = false;
                ShipStatus.RpcEndGame(GameOverReason.HumansByTask, false);
                return true;
            }
            return false;
        }

        private static bool CheckAndEndGameForImpostorWin(ShipStatus __instance, PlayerStatistics statistics)
        {
            if (statistics.TeamImpostorsAlive >= statistics.TotalAlive - statistics.TeamImpostorsAlive)
            {
                __instance.enabled = false;
                GameOverReason endReason;
                switch (TempData.LastDeathReason)
                {
                    case DeathReason.Exile:
                        endReason = GameOverReason.ImpostorByVote;
                        break;
                    case DeathReason.Kill:
                        endReason = GameOverReason.ImpostorByKill;
                        break;
                    default:
                        endReason = GameOverReason.ImpostorByVote;
                        break;
                }
                ShipStatus.RpcEndGame(endReason, false);
                return true;
            }
            return false;
        }

        private static bool CheckAndEndGameForCrewmateWin(ShipStatus __instance, PlayerStatistics statistics)
        {
            if (statistics.TeamImpostorsAlive == 0)
            {
                __instance.enabled = false;
                ShipStatus.RpcEndGame(GameOverReason.HumansByVote, false);
                return true;
            }
            return false;
        }

        private static void EndGameForO2(ShipStatus __instance)
        {
            __instance.enabled = false;
            ShipStatus.RpcEndGame((GameOverReason)CustomGameOverReason.SabotageO2, false);
            return;
        }

        private static void EndGameForReactor(ShipStatus __instance)
        {
            __instance.enabled = false;
            ShipStatus.RpcEndGame((GameOverReason)CustomGameOverReason.SabotageReactor, false);
            return;
        }
    }

    internal class PlayerStatistics
    {
        public int TeamImpostorsAlive { get; set; }
        public int TeamCrew { get; set; }
        public int NeutralAlive { get; set; }
        public int TotalAlive { get; set; }

        public PlayerStatistics(ShipStatus __instance)
        {
            GetPlayerCounts();
        }

        private void GetPlayerCounts()
        {
            int numImpostorsAlive = 0;
            int numTotalAlive = 0;
            int numNeutralAlive = 0;
            int numCrew = 0;

            for (int i = 0; i < GameData.Instance.PlayerCount; i++)
            {
                GameData.PlayerInfo playerInfo = GameData.Instance.AllPlayers[i];
                if (!playerInfo.Disconnected)
                {
                    if (playerInfo.Object.IsCrew()) numCrew++;
                    if (!playerInfo.IsDead)
                    {
                        numTotalAlive++;
                        if (playerInfo.Role.IsImpostor) numImpostorsAlive++;
                        if (playerInfo.Object.IsNeutral()) numNeutralAlive++;
                    }
                }
            }

            TeamCrew = numCrew;
            TeamImpostorsAlive = numImpostorsAlive;
            NeutralAlive = numNeutralAlive;
            TotalAlive = numTotalAlive;
        }
    }
}