using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BungeoppangTycoon.Tests.EditMode
{
    public sealed class SettingsMainPrefabTests
    {
        private const string PrefabPath = "Assets/Resources/Prefabs/UI/UI_Settings.prefab";

        [Test]
        public void Prefab_HasV6MenuStructureAndRuntimeScript()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent("UI_Settings"), Is.Not.Null, "UI_Settings 스크립트 누락");

            string[] required =
            {
                "PaperPanel", "TitleText", "CloseButton", "SettingBtn", "DocumentsButton",
                "AchivementButton", "ResetButton", "QuitBtn"
            };
            foreach (string name in required)
                Assert.That(Find(prefab, name), Is.Not.Null, $"필수 오브젝트 누락: {name}");
        }

        [Test]
        public void Prefab_MenuCardsHaveButtonsAndGeneratedIcons()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            string[] cards = { "SettingBtn", "DocumentsButton", "AchivementButton", "ResetButton" };
            foreach (string name in cards)
            {
                GameObject card = Find(prefab, name);
                Assert.That(card.GetComponent<Button>(), Is.Not.Null, $"Button 누락: {name}");
                Assert.That(Find(card, "Icon")?.GetComponent<Image>()?.sprite, Is.Not.Null, $"아이콘 Sprite 누락: {name}");
            }
        }

        [Test]
        public void Prefab_TextUsesTmpFontAndMaterial()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            foreach (TextMeshProUGUI text in prefab.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                Assert.That(text.font, Is.Not.Null, $"TMP 폰트 누락: {text.name}");
                Assert.That(text.fontSharedMaterial, Is.Not.Null, $"TMP 재질 누락: {text.name}");
            }
        }

        private static GameObject Find(GameObject root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(transform => transform.name == name)?.gameObject;
        }
    }
}
