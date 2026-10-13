#if UNITY_EDITOR
using System;
using System.Linq;
using DuelHero.Cards;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

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

            RectTransform titleArea = (RectTransform)NewUI("TitleArea", frontRoot.transform).transform;
            SetRect(titleArea, new Vector2(0.285f, 0.825f), new Vector2(0.715f, 0.925f), Vector2.zero, Vector2.zero);
            titleArea.gameObject.AddComponent<RectMask2D>();
            Text nameLabel = AddText("Name", titleArea, "베기", font, 12, TextAnchor.MiddleCenter);
            nameLabel.fontStyle = FontStyle.Bold;
            nameLabel.resizeTextForBestFit = true;
            nameLabel.resizeTextMinSize = 8;
            nameLabel.resizeTextMaxSize = 12;
            Stretch(nameLabel.rectTransform);

            RectTransform descriptionArea = (RectTransform)NewUI("DescriptionArea", frontRoot.transform).transform;
            SetRect(descriptionArea, new Vector2(0.105f, 0.18f), new Vector2(0.895f, 0.45f), Vector2.zero, new Vector2(0f, -2f));
            Text description = AddText("Description", descriptionArea, "범위 내 모든 적에게 피해 10을 줍니다.", font, 9, TextAnchor.MiddleCenter);
            description.resizeTextForBestFit = true;
            description.resizeTextMinSize = 8;
            description.resizeTextMaxSize = 10;
            description.horizontalOverflow = HorizontalWrapMode.Wrap;
            description.verticalOverflow = VerticalWrapMode.Truncate;
            SetRect(description.rectTransform, new Vector2(0f, 0.28f), Vector2.one, new Vector2(4f, 0f), new Vector2(-4f, -2f));
            RectTransform keywordLinksRoot = (RectTransform)NewUI("KeywordLinksRoot", descriptionArea).transform;
            SetRect(keywordLinksRoot, Vector2.zero, new Vector2(1f, 0.28f), new Vector2(4f, 0f), new Vector2(-4f, 0f));
            HorizontalLayoutGroup keywordLayout = keywordLinksRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            keywordLayout.childAlignment = TextAnchor.MiddleLeft;
            keywordLayout.spacing = 2f;
            keywordLayout.childControlWidth = true;
            keywordLayout.childControlHeight = true;
            keywordLayout.childForceExpandWidth = false;
            keywordLayout.childForceExpandHeight = false;
            Text keywordTemplate = AddText("KeywordLinkTemplate", keywordLinksRoot, "[방향지정]", font, 10, TextAnchor.MiddleLeft);
            keywordTemplate.color = new Color(1f, 0.73f, 0.28f, 1f);
            keywordTemplate.raycastTarget = true;
            keywordTemplate.gameObject.AddComponent<KeywordHoverLink>();
            LayoutElement keywordLayoutElement = keywordTemplate.gameObject.AddComponent<LayoutElement>();
            keywordLayoutElement.preferredWidth = 58f;
            keywordLayoutElement.preferredHeight = 16f;
            keywordTemplate.gameObject.SetActive(false);

            RectTransform rangeArea = (RectTransform)NewUI("RangeArea", frontRoot.transform).transform;
            SetRect(rangeArea, new Vector2(0.70f, 0.085f), new Vector2(0.895f, 0.18f), new Vector2(2f, 1f), new Vector2(-2f, -1f));
            CardRangeGraphic rangeGraphic = rangeArea.gameObject.AddComponent<CardRangeGraphic>();
            rangeGraphic.raycastTarget = false;

            Text state = AddText("State", frontRoot.transform, "사용 가능 · 미예약", font, 8, TextAnchor.MiddleCenter);
            state.color = new Color(0.72f, 0.75f, 0.78f, 1f);
            SetRect(state.rectTransform, new Vector2(0.14f, 0.025f), new Vector2(0.86f, 0.075f), Vector2.zero, Vector2.zero);

            Image energyBadge = AddImage("EnergyBadge", frontRoot.transform, energy);
            SetRect(energyBadge.rectTransform, new Vector2(0.01f, 0.81f), new Vector2(0.27f, 0.995f), new Vector2(-4f, 0f), new Vector2(1f, 2f));
            energyBadge.preserveAspect = true;
            Text energyLabel = AddText("EnergyCost", energyBadge.transform, "10", font, 17, TextAnchor.MiddleCenter);
            energyLabel.fontStyle = FontStyle.Bold;
            energyLabel.resizeTextForBestFit = true;
            energyLabel.resizeTextMinSize = 10;
            energyLabel.resizeTextMaxSize = 17;
            energyLabel.color = new Color(1f, 0.88f, 0.45f, 1f);
            Stretch(energyLabel.rectTransform);

            Image typeBadge = AddImage("TypeBadge", frontRoot.transform, attack);
            SetRect(typeBadge.rectTransform, new Vector2(0.73f, 0.81f), new Vector2(0.99f, 0.995f), new Vector2(-1f, 0f), new Vector2(4f, 2f));
            typeBadge.preserveAspect = true;
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
            Set(serialized, "keywordLinksRoot", keywordLinksRoot);
            Set(serialized, "keywordLinkTemplate", keywordTemplate);
            Set(serialized, "stateLabel", state);
            Set(serialized, "descriptionArea", descriptionArea);
            Set(serialized, "rangeArea", rangeArea);
            Set(serialized, "rangeGraphic", rangeGraphic);
            Set(serialized, "typeBadge", typeBadge);
            Set(serialized, "cooldownLabel", cooldown);
            Set(serialized, "backRoot", backImage.gameObject);
            Set(serialized, "grayscaleMaterial", grayscale);
            Set(serialized, "attackBadge", attack);
            Set(serialized, "defenseBadge", defense);
            Set(serialized, "recoveryBadge", recovery);
            Set(serialized, "specialBadge", special);
            Set(serialized, "moveBadge", move);
            SerializedProperty graphics = serialized.FindProperty("grayscaleGraphics");
            Graphic[] grayscaleGraphics = { illustration, frameImage, rangeGraphic, energyBadge, typeBadge };
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
