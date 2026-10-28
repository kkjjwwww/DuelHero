#if UNITY_EDITOR
using System;
using System.Linq;
using DuelHero.Cards;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DuelHero.Editor
{
    public static class CardPrefabBuilder
    {
        private const string PrefabPath = "Assets/02.Prefabs/CardView.prefab";
        private const string MaterialPath = "Assets/Art/UI/Cards/CardGrayscale.mat";
        private static readonly Color TextColor = new Color(0.94f, 0.89f, 0.77f, 1f);

        [MenuItem("Duel Hero/UI/Rebuild Card Prefab")]
        public static void Rebuild()
        {
            ConfigureTextures();
            Material grayscale = CreateGrayscaleMaterial();
            Sprite frame = LoadSprite("CardFrontFrame.png");
            Sprite back = LoadSprite("CardBack.png");
            Sprite energy = LoadSprite("EnergyBadge.png");
            Sprite attack = LoadSprite("TypeBadgeAttack.png");
            Sprite defense = LoadSprite("TypeBadgeDefense.png");
            Sprite recovery = LoadSprite("TypeBadgeRecovery.png");
            Sprite special = LoadSprite("TypeBadgeSpecial.png");
            Sprite move = LoadSprite("TypeBadgeMove.png");

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject root = NewUI("CardView");
            RectTransform rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = new Vector2(132f, 132f * 88f / 63f);
            LayoutElement layout = root.AddComponent<LayoutElement>();
            layout.preferredWidth = rootRect.sizeDelta.x;
            layout.preferredHeight = rootRect.sizeDelta.y;
            layout.flexibleWidth = 0f;
            layout.flexibleHeight = 0f;
            Image hitArea = root.AddComponent<Image>();
            hitArea.color = new Color(1f, 1f, 1f, 0.001f);
            Button button = root.AddComponent<Button>();
            button.targetGraphic = hitArea;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.78f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
            root.AddComponent<CardHoverInfo>();

            GameObject visual = NewUI("VisualRoot", root.transform);
            Stretch((RectTransform)visual.transform);
            GameObject frontRoot = NewUI("Front", visual.transform);
            Stretch((RectTransform)frontRoot.transform);

            Image illustration = AddImage("Illustration", frontRoot.transform, null);
            SetRect(illustration.rectTransform, new Vector2(0.09f, 0.475f), new Vector2(0.91f, 0.80f), Vector2.zero, Vector2.zero);
            illustration.color = new Color(0.12f, 0.14f, 0.18f, 1f);
            illustration.enabled = false;

            Image frameImage = AddImage("Frame", frontRoot.transform, frame);
            Stretch(frameImage.rectTransform);
            frameImage.preserveAspect = true;
            var reservationOutline = NewUI("ReservationOutline", frontRoot.transform).AddComponent<CardReservationOutline>();
            SetRect(reservationOutline.rectTransform, new Vector2(0.075f, 0.045f), new Vector2(0.925f, 0.96f), Vector2.zero, Vector2.zero);
            reservationOutline.color = Color.white;
            reservationOutline.raycastTarget = false;
            reservationOutline.enabled = false;
            Text reservationSlot = AddText("ReservationSlot", frontRoot.transform, string.Empty, font, 16, TextAnchor.MiddleCenter);
            reservationSlot.color = Color.white;
            reservationSlot.fontStyle = FontStyle.Bold;
            reservationSlot.horizontalOverflow = HorizontalWrapMode.Overflow;
            reservationSlot.verticalOverflow = VerticalWrapMode.Overflow;
            SetRect(reservationSlot.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-12f, 4f), new Vector2(12f, 24f));
            reservationSlot.gameObject.SetActive(false);

            RectTransform titleArea = (RectTransform)NewUI("TitleArea", frontRoot.transform).transform;
            SetRect(titleArea, new Vector2(0.285f, 0.825f), new Vector2(0.715f, 0.925f), Vector2.zero, Vector2.zero);
            Image titleMask = titleArea.gameObject.AddComponent<Image>();
            titleMask.raycastTarget = false;
            titleArea.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var badgeFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/UI/Fonts/BusanDandiM SDF.asset");
            var nameLabel = NewUI("Name", titleArea).AddComponent<TextMeshProUGUI>();
            nameLabel.font = badgeFont;
            nameLabel.text = "베기";
            nameLabel.color = TextColor;
            nameLabel.raycastTarget = false;
            nameLabel.fontStyle = FontStyles.Bold;
            nameLabel.alignment = TextAlignmentOptions.Center;
            nameLabel.textWrappingMode = TextWrappingModes.NoWrap;
            nameLabel.enableAutoSizing = true;
            nameLabel.fontSizeMin = 8;
            nameLabel.fontSizeMax = 12;
            Stretch(nameLabel.rectTransform);

            RectTransform descriptionArea = (RectTransform)NewUI("DescriptionArea", frontRoot.transform).transform;
            SetRect(descriptionArea, new Vector2(0.105f, 0.18f), new Vector2(0.895f, 0.45f), Vector2.zero, new Vector2(0f, -2f));
            var description = NewUI("Description", descriptionArea).AddComponent<TextMeshProUGUI>();
            description.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/UI/Fonts/NotoSansKR SDF.asset")
                ?? throw new InvalidOperationException("Card description TMP font asset is missing.");
            description.text = "범위 내 모든 적에게 피해 10을 줍니다. [방향지정]";
            description.fontSize = 9f;
            description.fontSizeMin = 8f; description.fontSizeMax = 10f;
            description.enableAutoSizing = true;
            description.alignment = TextAlignmentOptions.Center;
            description.color = TextColor;
            description.richText = true;
            description.textWrappingMode = TextWrappingModes.Normal;
            description.overflowMode = TextOverflowModes.Truncate;
            description.raycastTarget = true;
            SetRect(description.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 2f), new Vector2(-4f, -2f));
            description.gameObject.AddComponent<KeywordHoverLink>();

            RectTransform rangeArea = (RectTransform)NewUI("RangeArea", frontRoot.transform).transform;
            SetRect(rangeArea, new Vector2(0.70f, 0.085f), new Vector2(0.895f, 0.18f), new Vector2(-2f, 4f), new Vector2(-6f, 2f));
            Image rangeBackground = AddImage("RangeBackground", rangeArea, null);
            rangeBackground.color = new Color(0.035f, 0.045f, 0.06f, 0.95f);
            rangeBackground.raycastTarget = false;
            SetRect(rangeBackground.rectTransform, Vector2.zero, Vector2.one, new Vector2(-3f, -3f), new Vector2(3f, 3f));
            RectTransform rangeDrawing = (RectTransform)NewUI("RangeGraphic", rangeArea).transform;
            SetRect(rangeDrawing, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            CardRangeGraphic rangeGraphic = rangeDrawing.gameObject.AddComponent<CardRangeGraphic>();
            rangeGraphic.raycastTarget = false;


            Image energyBadge = AddImage("EnergyBadge", frontRoot.transform, energy);
            SetRect(energyBadge.rectTransform, new Vector2(0.01f, 0.81f), new Vector2(0.27f, 0.995f), new Vector2(-4f, 0f), new Vector2(1f, 2f));
            energyBadge.preserveAspect = true;
            var energyLabel = NewUI("EnergyCost", energyBadge.transform).AddComponent<TextMeshProUGUI>();
            energyLabel.font = badgeFont;
            energyLabel.text = "10";
            energyLabel.raycastTarget = false;
            energyLabel.fontStyle = FontStyles.Bold;
            energyLabel.alignment = TextAlignmentOptions.Center;
            energyLabel.textWrappingMode = TextWrappingModes.NoWrap;
            energyLabel.enableAutoSizing = true;
            energyLabel.fontSizeMin = 10;
            energyLabel.fontSizeMax = 17;
            energyLabel.color = new Color(1f, 0.88f, 0.45f, 1f);
            Stretch(energyLabel.rectTransform);

            Image typeBadge = AddImage("TypeBadge", frontRoot.transform, attack);
            SetRect(typeBadge.rectTransform, new Vector2(0.73f, 0.81f), new Vector2(0.99f, 0.995f), new Vector2(-1f, 0f), new Vector2(4f, 2f));
            typeBadge.preserveAspect = true;
            energyBadge.raycastTarget = true;
            typeBadge.raycastTarget = true;
            CardRaycastArea raycastArea = root.AddComponent<CardRaycastArea>();
            SerializedObject hitSettings = new SerializedObject(raycastArea);
            Set(hitSettings, "body", frameImage.rectTransform);
            Set(hitSettings, "energyBadge", energyBadge);
            Set(hitSettings, "typeBadge", typeBadge);
            hitSettings.ApplyModifiedPropertiesWithoutUndo();
            Text cooldown = AddText("Cooldown", typeBadge.transform, "1", font, 28, TextAnchor.MiddleCenter);
            cooldown.fontStyle = FontStyle.Bold;
            cooldown.color = new Color(1f, 0.12f, 0.08f, 1f);
            cooldown.gameObject.SetActive(false);
            Stretch(cooldown.rectTransform);

            Image backImage = AddImage("Back", visual.transform, back);
            Stretch(backImage.rectTransform);
            backImage.preserveAspect = true;
            backImage.gameObject.SetActive(false);

            CardView cardView = root.AddComponent<CardView>();
            SerializedObject serialized = new SerializedObject(cardView);
            Set(serialized, "button", button);
            Set(serialized, "visualRoot", visual.transform);
            Set(serialized, "frontRoot", frontRoot);
            Set(serialized, "illustration", illustration);
            Set(serialized, "nameLabel", nameLabel);
            Set(serialized, "energyLabel", energyLabel);
            Set(serialized, "descriptionLabel", description);
            Set(serialized, "descriptionArea", descriptionArea);
            Set(serialized, "rangeArea", rangeArea);
            Set(serialized, "rangeGraphic", rangeGraphic);
            Set(serialized, "typeBadge", typeBadge);
            Set(serialized, "cooldownLabel", cooldown);
            Set(serialized, "reservationOutline", reservationOutline);
            Set(serialized, "reservationSlotLabel", reservationSlot);
            Set(serialized, "backRoot", backImage.gameObject);
            Set(serialized, "grayscaleMaterial", grayscale);
            Set(serialized, "attackBadge", attack);
            Set(serialized, "defenseBadge", defense);
            Set(serialized, "recoveryBadge", recovery);
            Set(serialized, "specialBadge", special);
            Set(serialized, "moveBadge", move);
            SerializedProperty graphics = serialized.FindProperty("grayscaleGraphics");
            Graphic[] grayscaleGraphics = { illustration, frameImage, rangeBackground, rangeGraphic, energyBadge, typeBadge };
            graphics.arraySize = grayscaleGraphics.Length;
            for (int i = 0; i < grayscaleGraphics.Length; i++) graphics.GetArrayElementAtIndex(i).objectReferenceValue = grayscaleGraphics[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            ConnectScene(prefab.GetComponent<CardView>());
            AssetDatabase.SaveAssets();
            Debug.Log("[Duel Hero] Card prefab rebuilt: " + PrefabPath);
        }

        private static void ConfigureTextures()
        {
            string[] files = { "CardFrontFrame.png", "CardBack.png", "EnergyBadge.png", "TypeBadgeAttack.png", "TypeBadgeDefense.png", "TypeBadgeRecovery.png", "TypeBadgeSpecial.png", "TypeBadgeMove.png" };
            foreach (string file in files)
            {
                string path = "Assets/Art/UI/Cards/" + file;
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
        }

        private static Material CreateGrayscaleMaterial()
        {
            Shader shader = Shader.Find("DuelHero/UI/Grayscale");
            if (shader == null) throw new InvalidOperationException("DuelHero/UI/Grayscale shader is missing.");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "CardGrayscale" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else material.shader = shader;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConnectScene(CardView prefab)
        {
            foreach (var enemySlot in UnityEngine.Object.FindObjectsByType<EnemyActionSlotView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                SerializedObject enemySettings = new SerializedObject(enemySlot);
                Set(enemySettings, "cardPrefab", prefab);
                Set(enemySettings, "cardDatabase", AssetDatabase.LoadAssetAtPath<CardDatabase>("Assets/Data/CardDatabase.asset"));
                enemySettings.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(enemySlot);
            }
            ActionQueueUI actionQueue = UnityEngine.Object.FindFirstObjectByType<ActionQueueUI>(FindObjectsInactive.Include);
            if (actionQueue != null)
            {
                SerializedObject queueSettings = new SerializedObject(actionQueue);
                Set(queueSettings, "cardPrefab", prefab);
                Set(queueSettings, "cardDatabase", AssetDatabase.LoadAssetAtPath<CardDatabase>("Assets/Data/CardDatabase.asset"));
                queueSettings.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(actionQueue);
            }
            DeckListUI deckList = UnityEngine.Object.FindFirstObjectByType<DeckListUI>(FindObjectsInactive.Include);
            if (deckList == null) return;
            SerializedObject serialized = new SerializedObject(deckList);
            Set(serialized, "cardPrefab", prefab);
            serialized.FindProperty("minimumCardWidth").floatValue = 132f;
            serialized.FindProperty("cardHeight").floatValue = 132f * 88f / 63f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            RectTransform panel = deckList.GetComponent<RectTransform>();
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, Mathf.Max(panel.sizeDelta.y, 250f));
            EditorUtility.SetDirty(deckList);
            EditorUtility.SetDirty(panel);
            EditorSceneManager.MarkSceneDirty(deckList.gameObject.scene);
            EditorSceneManager.SaveScene(deckList.gameObject.scene);
        }

        private static Sprite LoadSprite(string file) => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Cards/" + file);
        private static GameObject NewUI(string name, Transform parent = null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        private static Image AddImage(string name, Transform parent, Sprite sprite)
        {
            Image image = NewUI(name, parent).AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static Text AddText(string name, Transform parent, string value, Font font, int size, TextAnchor alignment)
        {
            Text text = NewUI(name, parent).AddComponent<Text>();
            text.text = value;
            text.font = font;
            text.fontSize = size;
            text.color = TextColor;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void Set(SerializedObject serialized, string property, UnityEngine.Object value)
        {
            serialized.FindProperty(property).objectReferenceValue = value;
        }
    }
}
#endif
