using HarmonyLib;
using UnityEngine;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UltimateMods.Patches;
using UltimateMods.Utilities;
using static UnityEngine.UI.Button;
using Object = UnityEngine.Object;

namespace UltimateMods.Modules
{
    [HarmonyPatch]
    public static class ClientOptionsPatch
    {
        private static SelectionBehaviour[] AllOptions = {
            new SelectionBehaviour("GhostsSeeTasksButton", () => MapOptions.ghostsSeeTasks = UltimateModsPlugin.GhostsSeeTasks.Value = !UltimateModsPlugin.GhostsSeeTasks.Value, UltimateModsPlugin.GhostsSeeTasks.Value),
            new SelectionBehaviour("GhostsSeeVotesButton", () => MapOptions.ghostsSeeVotes = UltimateModsPlugin.GhostsSeeVotes.Value = !UltimateModsPlugin.GhostsSeeVotes.Value, UltimateModsPlugin.GhostsSeeVotes.Value),
            new SelectionBehaviour("GhostsSeeRolesButton", () => MapOptions.ghostsSeeRoles = UltimateModsPlugin.GhostsSeeRoles.Value = !UltimateModsPlugin.GhostsSeeRoles.Value, UltimateModsPlugin.GhostsSeeRoles.Value),
            new SelectionBehaviour("ShowRoleSummaryButton", () => MapOptions.showRoleSummary = UltimateModsPlugin.ShowRoleSummary.Value = !UltimateModsPlugin.ShowRoleSummary.Value, UltimateModsPlugin.ShowRoleSummary.Value),
            new SelectionBehaviour("HideNameplates", () => {
                MapOptions.hideNameplates = UltimateModsPlugin.HideNameplates.Value = !UltimateModsPlugin.HideNameplates.Value;
                MeetingHudPatch.nameplatesChanged = true;
                return MapOptions.hideNameplates;
            }, UltimateModsPlugin.HideNameplates.Value),
            new SelectionBehaviour("HideTaskArrows", () => MapOptions.hideTaskArrows = UltimateModsPlugin.HideTaskArrows.Value = !UltimateModsPlugin.HideTaskArrows.Value, UltimateModsPlugin.HideTaskArrows.Value),
        };

        private static GameObject popUp;
        private static TextMeshPro titleText;

        private static ToggleButtonBehaviour moreOptions;
        private static List<ToggleButtonBehaviour> modButtons;
        private static TextMeshPro titleTextTitle;
        public static Vector3? _origin;

        public static ToggleButtonBehaviour buttonPrefab;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start))]
        public static void MainMenuManager_StartPostfix(MainMenuManager __instance)
        {
            // Prefab for the title
            var tmp = __instance.Announcement.transform.Find("Title_Text").gameObject.GetComponent<TextMeshPro>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.transform.localPosition += Vector3.left * 0.2f;
            titleText = Object.Instantiate(tmp);
            Object.Destroy(titleText.GetComponent<TextTranslatorTMP>());
            titleText.gameObject.SetActive(false);
            Object.DontDestroyOnLoad(titleText);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(OptionsMenuBehaviour), nameof(OptionsMenuBehaviour.Start))]
        public static void OptionsMenuBehaviour_StartPostfix(OptionsMenuBehaviour __instance)
        {
            if (!__instance.CensorChatButton) return;

            if (!popUp)
            {
                CreateCustom(__instance);
            }

            if (!buttonPrefab)
            {
                buttonPrefab = Object.Instantiate(__instance.CensorChatButton);
                Object.DontDestroyOnLoad(buttonPrefab);
                buttonPrefab.name = "CensorChatPrefab";
                buttonPrefab.gameObject.SetActive(false);
            }

            SetUpOptions();
            InitializeMoreButton(__instance);
        }

        private static void CreateCustom(OptionsMenuBehaviour prefab)
        {
            popUp = Object.Instantiate(prefab.gameObject);
            Object.DontDestroyOnLoad(popUp);
            var transform = popUp.transform;
            var pos = transform.localPosition;
            pos.z = -810f;
            transform.localPosition = pos;

            Object.Destroy(popUp.GetComponent<OptionsMenuBehaviour>());
            foreach (var gObj in popUp.gameObject.GetAllChilds())
            {
                if (gObj.name != "Background" && gObj.name != "CloseButton")
                    Object.Destroy(gObj);
            }
            popUp.SetActive(false);
        }

        private static void InitializeMoreButton(OptionsMenuBehaviour __instance)
        {
            moreOptions = Object.Instantiate(buttonPrefab, __instance.CensorChatButton.transform.parent);
            var transform = __instance.CensorChatButton.transform;
            _origin ??= transform.localPosition;
            transform.localPosition = _origin.Value + Vector3.left * 1.3f;
            moreOptions.transform.localPosition = _origin.Value + Vector3.right * 2.6f;
            var trans = moreOptions.transform.localPosition;
            moreOptions.gameObject.SetActive(true);
            trans = moreOptions.transform.position;
            moreOptions.Text.text = ModTranslation.getString("ModOptionsText");
            var moreOptionsButton = moreOptions.GetComponent<PassiveButton>();
            moreOptionsButton.OnClick = new ButtonClickedEvent();
            moreOptionsButton.OnClick.AddListener((Action)(() =>
            {
                if (!popUp) return;
                if (__instance.transform.parent && __instance.transform.parent == FastDestroyableSingleton<HudManager>.Instance.transform)
                {
                    popUp.transform.SetParent(FastDestroyableSingleton<HudManager>.Instance.transform);
                    popUp.transform.localPosition = new Vector3(0, 0, -800f);
                }
                else
                {
                    popUp.transform.SetParent(null);
                    Object.DontDestroyOnLoad(popUp);
                }

                CheckSetTitle();
                RefreshOpen();
            }));
        }

        private static void RefreshOpen()
        {
            popUp.gameObject.SetActive(false);
            popUp.gameObject.SetActive(true);
            SetUpOptions();
        }

        private static void CheckSetTitle()
        {
            if (!popUp || popUp.GetComponentInChildren<TextMeshPro>() || !titleText) return;

            var title = titleTextTitle = Object.Instantiate(titleText, popUp.transform);
            title.GetComponent<RectTransform>().localPosition = Vector3.up * 2.3f;
            title.gameObject.SetActive(true);
            title.text = ModTranslation.getString("ModOptionsText");
            title.name = "TitleText";
        }

        private static void SetUpOptions()
        {
            if (popUp.transform.GetComponentInChildren<ToggleButtonBehaviour>()) return;

            modButtons = new List<ToggleButtonBehaviour>();

            for (var i = 0; i < AllOptions.Length; i++)
            {
                var info = AllOptions[i];

                var button = Object.Instantiate(buttonPrefab, popUp.transform);
                var pos = new Vector3(i % 2 == 0 ? -1.17f : 1.17f, 1.3f - i / 2 * 0.8f, -.5f);

                var transform = button.transform;
                transform.localPosition = pos;

                button.onState = info.DefaultValue;
                button.Background.color = button.onState ? Color.green : Palette.ImpostorRed;

                button.Text.text = ModTranslation.getString(info.Title);
                button.Text.fontSizeMin = button.Text.fontSizeMax = 2.2f;
                button.Text.font = Object.Instantiate(titleText.font);
                button.Text.GetComponent<RectTransform>().sizeDelta = new Vector2(2, 2);

                button.name = info.Title.Replace(" ", "") + "Toggle";
                button.gameObject.SetActive(true);

                var passiveButton = button.GetComponent<PassiveButton>();
                var colliderButton = button.GetComponent<BoxCollider2D>();

                colliderButton.size = new Vector2(2.2f, .7f);

                passiveButton.OnClick = new ButtonClickedEvent();
                passiveButton.OnMouseOut = new UnityEvent();
                passiveButton.OnMouseOver = new UnityEvent();

                passiveButton.OnClick.AddListener((Action)(() =>
                {
                    button.onState = info.OnClick();
                    button.Background.color = button.onState ? Color.green : Palette.ImpostorRed;
                }));

                passiveButton.OnMouseOver.AddListener((Action)(() => button.Background.color = new Color32(34, 139, 34, byte.MaxValue)));
                passiveButton.OnMouseOut.AddListener((Action)(() => button.Background.color = button.onState ? Color.green : Palette.ImpostorRed));

                foreach (var spr in button.gameObject.GetComponentsInChildren<SpriteRenderer>())
                    spr.size = new Vector2(2.2f, .7f);

                modButtons.Add(button);
            }
        }
        private static IEnumerable<GameObject> GetAllChilds(this GameObject Go)
        {
            for (var i = 0; i < Go.transform.childCount; i++)
            {
                yield return Go.transform.GetChild(i).gameObject;
            }
        }

        public static void updateTranslations()
        {
            if (titleTextTitle)
                titleTextTitle.text = ModTranslation.getString("ModOptionsText");

            if (moreOptions)
                moreOptions.Text.text = ModTranslation.getString("ModOptionsText");

            for (int i = 0; i < AllOptions.Length; i++)
            {
                if (i >= modButtons.Count) break;
                modButtons[i].Text.text = ModTranslation.getString(AllOptions[i].Title);
            }
        }

        public class SelectionBehaviour
        {
            public string Title;
            public Func<bool> OnClick;
            public bool DefaultValue;

            public SelectionBehaviour(string title, Func<bool> onClick, bool defaultValue)
            {
                Title = title;
                OnClick = onClick;
                DefaultValue = defaultValue;
            }
        }
    }
}