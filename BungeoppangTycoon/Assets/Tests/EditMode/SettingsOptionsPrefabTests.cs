using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BungeoppangTycoon.Tests.EditMode
{
    public sealed class SettingsOptionsPrefabTests
    {
        private const string PrefabPath = "Assets/Resources/Prefabs/UI/UI_SettingsOptions.prefab";

        [Test]
        public void Prefab_HasRequiredFigmaV4Structure()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<Canvas>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<CanvasScaler>(), Is.Not.Null);
            string[] required =
            {
                "ContentRoot", "TitleText", "CloseButton", "VolumeCard",
                "VolumeSlider", "VolumeValueText", "VolumeMinusButton",
                "VolumePlusButton", "KeyboardCard", "KeyboardHintToggle",
                "KeyboardHintStateText", "KeyboardHelpButton", "ResetZone",
                "ResetGameButton", "FooterCloseButton"
            };

            foreach (string name in required)
                Assert.That(Find(prefab, name), Is.Not.Null, $"필수 오브젝트 누락: {name}");
        }

        [Test]
        public void Prefab_UsesInteractiveControlsAndExplicitNavigation()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(Find(prefab, "VolumeSlider").GetComponent<Slider>(), Is.Not.Null);

            string[] buttons =
            {
                "CloseButton", "VolumeMinusButton", "VolumePlusButton",
                "KeyboardHintToggle", "KeyboardHelpButton",
                "ResetGameButton", "FooterCloseButton"
            };

            foreach (string name in buttons)
            {
                Button button = Find(prefab, name).GetComponent<Button>();
                Assert.That(button, Is.Not.Null, $"Button 누락: {name}");
                Assert.That(button.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit), $"명시적 키보드 이동 누락: {name}");
            }
        }

        [Test]
        public void Prefab_TextRemainsDynamicTmpInsteadOfBakedIntoImages()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            string[] dynamicTexts =
            {
                "VolumeValueText", "VolumeStateText",
                "KeyboardHintStateText", "KeyboardHintDescriptionText"
            };

            foreach (string name in dynamicTexts)
                Assert.That(Find(prefab, name).GetComponent<TextMeshProUGUI>(), Is.Not.Null, $"TMP 누락: {name}");
        }

        [Test]
        public void Prefab_HasRuntimeScriptSpritesAndTmpMaterials()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab.GetComponent("UI_SettingsOptions"), Is.Not.Null, "UI_SettingsOptions 스크립트 누락");

            string[] spriteObjects =
            {
                "SettingsIcon", "CloseButton", "VolumeCard", "VolumeIconBadge",
                "VolumeIcon", "VolumeMinusButton", "Background", "Fill", "Handle",
                "VolumePlusButton", "KeyboardCard", "KeyboardIconBadge", "KeyboardIcon",
                "KeyboardHintToggle", "KeyboardHintToggleThumb", "Keycap_Space", "Keycap_1–8",
                "KeyboardHelpButton", "ResetZone", "WarningIcon", "ResetGameButton",
                "FooterEscKeycap", "FooterCloseButton", "AutoSaveStatusIcon"
            };
            foreach (string name in spriteObjects)
            {
                Image image = Find(prefab, name)?.GetComponent<Image>();
                Assert.That(image, Is.Not.Null, $"Image 누락: {name}");
                Assert.That(image.sprite, Is.Not.Null, $"Sprite 연결 누락: {name}");
            }

            foreach (TextMeshProUGUI text in prefab.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                Assert.That(text.font, Is.Not.Null, $"TMP 폰트 누락: {text.name}");
                Assert.That(text.fontSharedMaterial, Is.Not.Null, $"TMP 재질 누락: {text.name}");
            }
        }

        private static GameObject Find(GameObject root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(x => x.name == name)?.gameObject;
        }
    }
}
