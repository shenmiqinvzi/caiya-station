using System;
using System.Collections.Generic;
using System.Reflection;
using CaiYaStation.Domain;
using UnityEngine;

namespace CaiYaStation
{
    internal sealed class StationVisuals : IDisposable
    {
        private readonly Action<string> _log;
        private Material? _frame;
        private Material? _dark;
        private Material? _metal;
        private Material? _sign;
        private Material? _used;
        private Material? _beak;
        private Material? _glow;
        private Material? _flag;
        private Material? _wood;
        private Material? _cream;
        private Material? _roof;
        private Material? _paper;
        private Material? _rope;
        private Material? _red;
        private Material? _burlap;
        private Font? _signFont;
        private bool _materialsReady;
        private Texture2D? _iconTexture;
        private Sprite? _icon;

        internal StationVisuals(Action<string> log)
        {
            _log = log;
        }

        internal Sprite Icon
        {
            get
            {
                if (_icon == null) _icon = CreateIcon();
                return _icon;
            }
        }

        private void EnsureMaterials()
        {
            if (_materialsReady) return;
            _materialsReady = true;
            try
            {
                _signFont = Font.CreateDynamicFontFromOSFont(
                    new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial" }, 64);
            }
            catch (Exception exception)
            {
                _log("Station sign font unavailable: " + exception.Message);
            }
            var template = FindTemplateMaterial();
            if (template != null)
            {
                _frame = Tint(template, new Color(0.16f, 0.20f, 0.23f));
                _dark = Tint(template, new Color(0.08f, 0.10f, 0.12f));
                _metal = Tint(template, new Color(0.58f, 0.64f, 0.68f));
                _sign = Tint(template, new Color(0.24f, 0.86f, 0.52f), true);
                _used = Tint(template, new Color(0.38f, 0.40f, 0.42f));
                _beak = Tint(template, new Color(0.97f, 0.66f, 0.18f), true);
                _glow = Tint(template, new Color(1f, 0.82f, 0.42f), true);
                _flag = Tint(template, new Color(0.20f, 0.72f, 0.46f));
                _wood = Tint(template, new Color(0.33f, 0.21f, 0.13f));
                _cream = Tint(template, new Color(0.88f, 0.82f, 0.66f));
                _roof = Tint(template, new Color(0.09f, 0.15f, 0.14f));
                _paper = Tint(template, new Color(0.94f, 0.90f, 0.78f));
                _rope = Tint(template, new Color(0.42f, 0.30f, 0.16f));
                _red = Tint(template, new Color(0.72f, 0.18f, 0.14f), true);
                _burlap = Tint(template, new Color(0.58f, 0.46f, 0.31f));
                return;
            }
            var shader = FindShader();
            if (shader == null)
            {
                _log("Station visuals unavailable: no compatible shader");
                return;
            }
            _frame = Make(shader, new Color(0.16f, 0.20f, 0.23f));
            _dark = Make(shader, new Color(0.08f, 0.10f, 0.12f));
            _metal = Make(shader, new Color(0.58f, 0.64f, 0.68f));
            _sign = Make(shader, new Color(0.24f, 0.86f, 0.52f), true);
            _used = Make(shader, new Color(0.38f, 0.40f, 0.42f));
            _beak = Make(shader, new Color(0.97f, 0.66f, 0.18f), true);
            _glow = Make(shader, new Color(1f, 0.82f, 0.42f), true);
            _flag = Make(shader, new Color(0.20f, 0.72f, 0.46f));
            _wood = Make(shader, new Color(0.33f, 0.21f, 0.13f));
            _cream = Make(shader, new Color(0.88f, 0.82f, 0.66f));
            _roof = Make(shader, new Color(0.09f, 0.15f, 0.14f));
            _paper = Make(shader, new Color(0.94f, 0.90f, 0.78f));
            _rope = Make(shader, new Color(0.42f, 0.30f, 0.16f));
            _red = Make(shader, new Color(0.72f, 0.18f, 0.14f), true);
            _burlap = Make(shader, new Color(0.58f, 0.46f, 0.31f));
        }

        internal GameObject CreateStation(Vector3 position, float yaw, bool used)
        {
            EnsureMaterials();
            var root = new GameObject("CaiYaStation.Kiosk");
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));

            // Foundation and a small step; the kiosk should look placed on the ground rather
            // than dropped on it.
            Shape(root, PrimitiveType.Cube, new Vector3(0, 0.06f, 0), new Vector3(3.8f, 0.12f, 2.9f), _dark);
            Shape(root, PrimitiveType.Cube, new Vector3(0, 0.11f, 1.55f), new Vector3(2.2f, 0.10f, 0.55f), _metal);
            Shape(root, PrimitiveType.Cube, new Vector3(0, 0.13f, 0), new Vector3(3.35f, 0.06f, 2.45f), _wood);

            // Four timber posts and a panelled back wall give the booth a real silhouette.
            foreach (var x in new[] { -1.60f, 1.60f })
            {
                foreach (var z in new[] { -1.10f, 1.10f })
                {
                    Shape(root, PrimitiveType.Cylinder, new Vector3(x, 1.60f, z), new Vector3(0.09f, 1.60f, 0.09f), _wood);
                    Shape(root, PrimitiveType.Cube, new Vector3(x, 3.16f, z), new Vector3(0.18f, 0.08f, 0.18f), _metal);
                }
            }
            Shape(root, PrimitiveType.Cube, new Vector3(0, 1.58f, -1.03f), new Vector3(3.10f, 2.50f, 0.14f), _frame);
            Shape(root, PrimitiveType.Cube, new Vector3(0, 0.36f, -0.96f), new Vector3(3.00f, 0.24f, 0.10f), _dark);
            for (var index = 0; index < 9; index++)
            {
                var x = -1.36f + index * 0.34f;
                Shape(root, PrimitiveType.Cube, new Vector3(x, 1.58f, -0.94f), new Vector3(0.055f, 2.34f, 0.035f), _wood);
            }

            // Counter: body, stone top, front panel and a lower shelf for parcels.
            Shape(root, PrimitiveType.Cube, new Vector3(0, 0.58f, 0.58f), new Vector3(2.50f, 1.00f, 0.80f), _wood);
            Shape(root, PrimitiveType.Cube, new Vector3(0, 1.10f, 0.58f), new Vector3(2.70f, 0.12f, 0.95f), _metal);
            Shape(root, PrimitiveType.Cube, new Vector3(0, 0.62f, 0.995f), new Vector3(2.35f, 0.86f, 0.04f), _frame);
            Shape(root, PrimitiveType.Cube, new Vector3(0, 0.24f, 1.02f), new Vector3(2.35f, 0.05f, 0.03f), _metal);
            Shape(root, PrimitiveType.Cube, new Vector3(0, 1.00f, 1.02f), new Vector3(2.35f, 0.05f, 0.03f), _metal);
            Shape(root, PrimitiveType.Cube, new Vector3(0, 0.18f, 0.58f), new Vector3(2.40f, 0.06f, 0.75f), _dark);
            Parcel(root, new Vector3(-0.78f, 1.22f, 0.55f), 0.40f, 0.27f, 0.32f, 8f);
            Parcel(root, new Vector3(-0.30f, 1.20f, 0.50f), 0.30f, 0.20f, 0.26f, -12f);
            Shape(root, PrimitiveType.Sphere, new Vector3(0.86f, 1.18f, 0.55f), new Vector3(0.09f, 0.07f, 0.09f), _glow);

            // Back shelf with a few sorted parcels and a route notice.
            Shape(root, PrimitiveType.Cube, new Vector3(0, 1.86f, -0.82f), new Vector3(2.25f, 0.08f, 0.36f), _wood);
            Parcel(root, new Vector3(-0.72f, 2.02f, -0.82f), 0.36f, 0.24f, 0.28f, 4f);
            Parcel(root, new Vector3(-0.28f, 2.00f, -0.82f), 0.28f, 0.18f, 0.24f, -8f);
            Shape(root, PrimitiveType.Cube, new Vector3(0.55f, 2.02f, -0.80f), new Vector3(0.50f, 0.36f, 0.04f), _paper);
            Shape(root, PrimitiveType.Cube, new Vector3(0.55f, 2.08f, -0.77f), new Vector3(0.38f, 0.03f, 0.02f), _red);
            Shape(root, PrimitiveType.Cube, new Vector3(0.55f, 2.00f, -0.77f), new Vector3(0.38f, 0.03f, 0.02f), _dark);

            // Striped awning above the counter.
            for (var index = 0; index < 8; index++)
            {
                var x = -1.49f + index * 0.425f;
                var material = index % 2 == 0 ? _sign : _cream;
                Shape(root, PrimitiveType.Cube, new Vector3(x, 2.60f, 0.84f), new Vector3(0.425f, 0.055f, 1.42f),
                    material, Quaternion.Euler(-8f, 0f, 0f));
            }
            Shape(root, PrimitiveType.Cube, new Vector3(0, 2.50f, 1.54f), new Vector3(3.42f, 0.22f, 0.06f), _frame);
            Shape(root, PrimitiveType.Cube, new Vector3(-1.72f, 2.50f, 0.84f), new Vector3(0.06f, 0.22f, 1.42f), _frame);
            Shape(root, PrimitiveType.Cube, new Vector3(1.72f, 2.50f, 0.84f), new Vector3(0.06f, 0.22f, 1.42f), _frame);

            // Peaked roof, ridge cap and a little vent.
            Shape(root, PrimitiveType.Cube, new Vector3(0, 3.08f, 0.72f), new Vector3(3.90f, 0.10f, 1.55f),
                _roof, Quaternion.Euler(-12f, 0f, 0f));
            Shape(root, PrimitiveType.Cube, new Vector3(0, 3.08f, -0.72f), new Vector3(3.90f, 0.10f, 1.55f),
                _roof, Quaternion.Euler(12f, 0f, 0f));
            Shape(root, PrimitiveType.Cylinder, new Vector3(0, 3.25f, 0), new Vector3(0.08f, 1.95f, 0.08f),
                _roof, Quaternion.Euler(0f, 0f, 90f));
            Shape(root, PrimitiveType.Cube, new Vector3(0, 2.99f, 1.46f), new Vector3(3.90f, 0.06f, 0.08f), _metal);
            Shape(root, PrimitiveType.Cube, new Vector3(0, 2.99f, -1.46f), new Vector3(3.90f, 0.06f, 0.08f), _metal);
            Shape(root, PrimitiveType.Cube, new Vector3(0, 2.93f, 1.47f), new Vector3(3.90f, 0.035f, 0.03f), _sign);
            Shape(root, PrimitiveType.Cube, new Vector3(0.95f, 3.28f, -0.55f), new Vector3(0.28f, 0.34f, 0.28f), _dark);
            Shape(root, PrimitiveType.Cube, new Vector3(0.95f, 3.47f, -0.55f), new Vector3(0.36f, 0.06f, 0.36f), _metal);

            // Front fascia sign: mounted above the awning so the player actually sees it on
            // approach (the old back-wall sign was hidden by the roof from most angles).
            Shape(root, PrimitiveType.Cube, new Vector3(0, 2.98f, 1.46f), new Vector3(2.50f, 0.78f, 0.10f), _dark);
            var sign = Shape(root, PrimitiveType.Cube, new Vector3(0, 2.98f, 1.51f), new Vector3(2.30f, 0.66f, 0.06f), used ? _used : _sign);
            Shape(root, PrimitiveType.Cube, new Vector3(0, 3.32f, 1.55f), new Vector3(2.30f, 0.035f, 0.03f), _metal);
            Shape(root, PrimitiveType.Cube, new Vector3(0, 2.64f, 1.55f), new Vector3(2.30f, 0.035f, 0.03f), _metal);
            Shape(root, PrimitiveType.Cube, new Vector3(-1.15f, 2.98f, 1.55f), new Vector3(0.035f, 0.66f, 0.03f), _metal);
            Shape(root, PrimitiveType.Cube, new Vector3(1.15f, 2.98f, 1.55f), new Vector3(0.035f, 0.66f, 0.03f), _metal);
            Shape(root, PrimitiveType.Cube, new Vector3(-1.08f, 3.24f, 1.48f), new Vector3(0.05f, 0.28f, 0.08f), _metal);
            Shape(root, PrimitiveType.Cube, new Vector3(1.08f, 3.24f, 1.48f), new Vector3(0.05f, 0.28f, 0.08f), _metal);
            var headPivot = Anchor(root, "DuckHeadPivot", new Vector3(-0.80f, 3.00f, 1.555f));
            Shape(headPivot.gameObject, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.36f, 0.32f, 0.08f), used ? _used : _cream);
            Shape(headPivot.gameObject, PrimitiveType.Cube, new Vector3(0.18f, -0.04f, 0.03f), new Vector3(0.22f, 0.08f, 0.08f), _beak);
            Shape(headPivot.gameObject, PrimitiveType.Sphere, new Vector3(0.08f, 0.08f, 0.045f), new Vector3(0.048f, 0.048f, 0.025f), _dark);
            AddText(root, "SignText", "菜鸭驿站", new Vector3(0.30f, 2.98f, 1.565f),
                Quaternion.Euler(0f, 180f, 0f), 0.18f, new Color(0.96f, 0.92f, 0.76f, 1f), 1.55f, 0.42f);

            // The back wall gets a small notice board instead of a duplicate title sign.
            Shape(root, PrimitiveType.Cube, new Vector3(0, 2.30f, -0.90f), new Vector3(1.30f, 0.70f, 0.08f), _dark);
            Shape(root, PrimitiveType.Cube, new Vector3(0, 2.30f, -0.84f), new Vector3(1.12f, 0.52f, 0.04f), _paper);
            Shape(root, PrimitiveType.Cube, new Vector3(-0.12f, 2.40f, -0.81f), new Vector3(0.66f, 0.025f, 0.02f), _dark);
            Shape(root, PrimitiveType.Cube, new Vector3(-0.02f, 2.28f, -0.81f), new Vector3(0.82f, 0.025f, 0.02f), _dark);
            Shape(root, PrimitiveType.Cube, new Vector3(-0.16f, 2.16f, -0.81f), new Vector3(0.58f, 0.025f, 0.02f), _red);

            // Hanging "寄" sign under the awning.
            Shape(root, PrimitiveType.Cylinder, new Vector3(-0.38f, 2.38f, 1.28f), new Vector3(0.018f, 0.20f, 0.018f), _rope);
            Shape(root, PrimitiveType.Cylinder, new Vector3(0.38f, 2.38f, 1.28f), new Vector3(0.018f, 0.20f, 0.018f), _rope);
            Shape(root, PrimitiveType.Cube, new Vector3(0, 2.04f, 1.30f), new Vector3(1.05f, 0.68f, 0.08f), _dark);
            var hangingFace = Shape(root, PrimitiveType.Cube, new Vector3(0, 2.04f, 1.35f), new Vector3(0.92f, 0.56f, 0.04f), used ? _used : _sign);
            AddText(root, "HangingText", "寄", new Vector3(0f, 2.04f, 1.375f),
                Quaternion.Euler(0f, 180f, 0f), 0.30f, new Color(0.96f, 0.92f, 0.76f, 1f), 0.70f, 0.42f);

            // Waving flag with a small duck silhouette.
            Shape(root, PrimitiveType.Cylinder, new Vector3(-1.78f, 2.00f, -0.95f), new Vector3(0.06f, 2.00f, 0.06f), _metal);
            Shape(root, PrimitiveType.Sphere, new Vector3(-1.78f, 4.02f, -0.95f), new Vector3(0.12f, 0.12f, 0.12f), _metal);
            var flagPivot = Anchor(root, "FlagPivot", new Vector3(-1.78f, 3.86f, -0.95f));
            var flag = Shape(flagPivot.gameObject, PrimitiveType.Cube, new Vector3(0.48f, -0.32f, 0f), new Vector3(0.94f, 0.62f, 0.035f), used ? _used : _flag);
            Shape(flagPivot.gameObject, PrimitiveType.Sphere, new Vector3(0.37f, -0.29f, 0.03f), new Vector3(0.18f, 0.16f, 0.02f), _cream);
            Shape(flagPivot.gameObject, PrimitiveType.Cube, new Vector3(0.48f, -0.31f, 0.04f), new Vector3(0.10f, 0.04f, 0.02f), _beak);

            // Two warm lanterns and one point light. The lantern bodies are emissive; the
            // light is only for the counter pool, so the station stays cheap at night.
            var lanternA = Lantern(root, new Vector3(-1.26f, 2.18f, 1.18f));
            var lanternB = Lantern(root, new Vector3(1.26f, 2.18f, 1.18f));
            Light? lanternLight = null;
            try
            {
                var lightObject = new GameObject("LanternLight");
                lightObject.transform.SetParent(root.transform, false);
                lightObject.transform.localPosition = new Vector3(0f, 2.22f, 1.05f);
                lanternLight = lightObject.AddComponent<Light>();
                lanternLight.type = LightType.Point;
                lanternLight.range = 6.5f;
                lanternLight.intensity = 1.15f;
                lanternLight.color = new Color(1f, 0.84f, 0.55f);
                lanternLight.shadows = LightShadows.None;
            }
            catch (Exception exception)
            {
                _log("Station lantern light unavailable: " + exception.Message);
            }

            // Closed shutter and a "停业" board for the used state. Everything lives under an
            // unscaled group so the shutter slabs and text keep clean proportions.
            var shutter = new GameObject("ClosedShutter");
            shutter.transform.SetParent(root.transform, false);
            shutter.transform.localPosition = new Vector3(0, 1.30f, 1.04f);
            Shape(shutter, PrimitiveType.Cube, Vector3.zero, new Vector3(2.30f, 1.15f, 0.06f), _used);
            for (var index = 0; index < 4; index++)
                Shape(shutter, PrimitiveType.Cube, new Vector3(0, -0.42f + index * 0.28f, 0.04f),
                    new Vector3(2.26f, 0.025f, 0.02f), _dark);
            AddText(shutter, "ClosedText", "停业", new Vector3(0f, 0f, 0.05f),
                Quaternion.Euler(0f, 180f, 0f), 0.22f, new Color(0.86f, 0.84f, 0.76f, 1f), 1.35f, 0.52f);
            shutter.SetActive(used);

            // Props around the booth: crates, a barrel and a mail sack.
            Crate(root, new Vector3(-2.15f, 0.34f, 1.62f), 0.56f, 16f);
            Crate(root, new Vector3(-2.13f, 0.84f, 1.56f), 0.42f, -10f);
            Barrel(root, new Vector3(1.98f, 0.48f, 1.02f));
            Sack(root, new Vector3(1.38f, 0.38f, 1.72f));

            var view = root.AddComponent<StationView>();
            view.Configure(new StationViewParts
            {
                SignRenderers = new[] { sign.GetComponent<Renderer>(), hangingFace.GetComponent<Renderer>() },
                ActiveSign = _sign,
                UsedSign = _used,
                FlagPivot = flagPivot,
                FlagRenderer = flag.GetComponent<Renderer>(),
                ActiveFlag = _flag,
                DuckHead = headPivot,
                DuckRenderer = headPivot.GetChild(0).GetComponent<Renderer>(),
                ActiveDuck = _cream,
                Lanterns = new[] { lanternA.transform, lanternB.transform },
                Shutter = shutter,
                LanternLight = lanternLight
            });
            view.SetUsed(used);
            ApplyLayer(root, position);
            return root;
        }

        private TextMesh? AddText(GameObject root, string name, string value, Vector3 position,
            Quaternion rotation, float characterSize, Color color, float maxWidth = 0f, float maxHeight = 0f)
        {
            if (_signFont == null) return null;
            try
            {
                _signFont.RequestCharactersInTexture(value, 64, FontStyle.Normal);
                var textObject = new GameObject(name);
                textObject.transform.SetParent(root.transform, false);
                textObject.transform.localPosition = position;
                textObject.transform.localRotation = rotation;
                var mesh = textObject.AddComponent<TextMesh>();
                mesh.font = _signFont;
                mesh.fontSize = 64;
                mesh.characterSize = characterSize;
                mesh.anchor = TextAnchor.MiddleCenter;
                mesh.alignment = TextAlignment.Center;
                mesh.color = color;
                mesh.text = value;
                var renderer = textObject.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = _signFont.material;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
                if (maxWidth > 0f && maxHeight > 0f)
                {
                    var fit = textObject.AddComponent<WorldTextFit>();
                    fit.MaxWidth = maxWidth;
                    fit.MaxHeight = maxHeight;
                }
                return mesh;
            }
            catch (Exception exception)
            {
                _log("Station sign text failed (" + name + "): " + exception.Message);
                return null;
            }
        }

        private void Crate(GameObject root, Vector3 position, float size, float yaw)
        {
            var crate = Shape(root, PrimitiveType.Cube, position, new Vector3(size, size, size), _wood,
                Quaternion.Euler(0f, yaw, 0f));
            // Child coordinates are in the crate's local space, so they stay normalized; the
            // parent's non-uniform scale supplies the real size.
            Shape(crate, PrimitiveType.Cube, new Vector3(0f, 0f, 0.505f), new Vector3(0.94f, 0.12f, 0.02f), _dark);
            Shape(crate, PrimitiveType.Cube, new Vector3(0.505f, 0.26f, 0f), new Vector3(0.02f, 0.20f, 0.66f), _paper);
            Shape(crate, PrimitiveType.Cube, new Vector3(0f, -0.505f, 0f), new Vector3(0.94f, 0.02f, 0.94f), _dark);
        }

        private void Barrel(GameObject root, Vector3 position)
        {
            Shape(root, PrimitiveType.Cylinder, position, new Vector3(0.76f, 0.46f, 0.76f), _wood);
            Shape(root, PrimitiveType.Cylinder, position + Vector3.up * 0.30f, new Vector3(0.79f, 0.04f, 0.79f), _metal);
            Shape(root, PrimitiveType.Cylinder, position + Vector3.down * 0.30f, new Vector3(0.79f, 0.04f, 0.79f), _metal);
            Shape(root, PrimitiveType.Cylinder, position + Vector3.up * 0.46f, new Vector3(0.62f, 0.02f, 0.62f), _dark);
        }

        private void Sack(GameObject root, Vector3 position)
        {
            Shape(root, PrimitiveType.Capsule, position, new Vector3(0.38f, 0.44f, 0.38f), _burlap);
            Shape(root, PrimitiveType.Cylinder, position + Vector3.up * 0.40f, new Vector3(0.16f, 0.07f, 0.16f), _rope);
            Shape(root, PrimitiveType.Cube, position + new Vector3(0f, 0.10f, 0.19f), new Vector3(0.26f, 0.17f, 0.02f), _paper);
            Shape(root, PrimitiveType.Cube, position + new Vector3(-0.10f, 0.10f, 0.205f), new Vector3(0.10f, 0.03f, 0.01f), _red);
        }

        private void Parcel(GameObject root, Vector3 position, float width, float height, float depth, float yaw)
        {
            Shape(root, PrimitiveType.Cube, position, new Vector3(width, height, depth), _paper,
                Quaternion.Euler(0f, yaw, 0f));
            Shape(root, PrimitiveType.Cube, position + Vector3.up * (height * 0.5f + 0.006f),
                new Vector3(width * 1.03f, 0.015f, depth * 0.30f), _rope, Quaternion.Euler(0f, yaw, 0f));
            Shape(root, PrimitiveType.Cube, position + Vector3.up * (height * 0.5f + 0.007f),
                new Vector3(width * 0.10f, 0.014f, depth * 1.02f), _rope, Quaternion.Euler(0f, yaw, 0f));
        }

        private GameObject Lantern(GameObject root, Vector3 position)
        {
            Shape(root, PrimitiveType.Cylinder, position + Vector3.up * 0.21f, new Vector3(0.02f, 0.18f, 0.02f), _rope);
            Shape(root, PrimitiveType.Cylinder, position + Vector3.up * 0.05f, new Vector3(0.12f, 0.03f, 0.12f), _metal);
            var body = Shape(root, PrimitiveType.Sphere, position, new Vector3(0.24f, 0.24f, 0.24f), _glow);
            Shape(root, PrimitiveType.Cylinder, position + Vector3.down * 0.12f, new Vector3(0.12f, 0.03f, 0.12f), _metal);
            return body;
        }

        private static void ApplyLayer(GameObject root, Vector3 position)
        {
            var layer = VisibleLayer(position);
            foreach (var transform in root.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = layer;
        }

        internal static int VisibleLayer(Vector3 position)
        {
            var camera = Camera.main;
            var mask = camera != null ? camera.cullingMask : ~0;
            if ((mask & 1) != 0) return 0;
            if (Physics.Raycast(position + Vector3.up * 1.5f, Vector3.down, out var hit, 6, ~0, QueryTriggerInteraction.Ignore))
            {
                var ground = hit.collider.gameObject.layer;
                if ((mask & (1 << ground)) != 0 && ground != 8) return ground;
            }
            var actor = CharacterMainControl.Main;
            if (actor != null && actor.characterModel != null)
            {
                foreach (var renderer in actor.characterModel.GetComponentsInChildren<Renderer>(true))
                    if (renderer != null && (mask & (1 << renderer.gameObject.layer)) != 0) return renderer.gameObject.layer;
            }
            for (var index = 0; index < 32; index++)
                if ((mask & (1 << index)) != 0 && index != 5 && index != 8) return index;
            return 0;
        }

        private Sprite CreateIcon()
        {
            const int size = 64;
            const float centre = 31.5f;
            _iconTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            // Palette is tuned for map readability: bright fills plus a white halo ring so the
            // badge stays visible on dark minimaps and in storm/weather tinting.
            var halo = new Color(1f, 1f, 1f, 0.92f);
            var rim = new Color(0.02f, 0.07f, 0.05f, 1f);
            var green = new Color(0.25f, 0.92f, 0.52f, 1f);
            var lightGreen = new Color(0.48f, 1f, 0.68f, 1f);
            var cream = new Color(1f, 0.97f, 0.86f, 1f);
            var orange = new Color(1f, 0.68f, 0.14f, 1f);
            var dark = new Color(0.05f, 0.09f, 0.07f, 1f);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var px = x + 0.5f - centre;
                    var py = y + 0.5f - centre;
                    var distance = Mathf.Sqrt(px * px + py * py);
                    var colour = new Color(0f, 0f, 0f, 0f);
                    // Outer white halo first, so the badge reads as a bright coin on any map.
                    colour = Over(colour, halo, 31.4f - distance + 0.5f);
                    colour = Over(colour, rim, 29.8f - distance + 0.5f);
                    colour = Over(colour, green, 27.6f - distance + 0.5f);
                    // A soft highlight on the upper-left of the badge.
                    var highlight = Mathf.Clamp01(24f - new Vector2(px + 8f, py + 8f).magnitude);
                    colour = Over(colour, lightGreen, highlight * 0.18f);
                    // Duck head, beak and eye.
                    var head = new Vector2(px + 1.5f, py - 1.5f).magnitude;
                    colour = Over(colour, cream, 13.2f - head + 0.5f);
                    var beak = new Vector2((px - 10.5f) / 6.4f, (py + 2.2f) / 4.2f).magnitude;
                    colour = Over(colour, orange, (1f - beak) * 2.2f);
                    var eye = new Vector2(px - 4.2f, py - 3.4f).magnitude;
                    colour = Over(colour, dark, 1.8f - eye + 0.5f);
                    // A tiny mail tag under the duck: cream card with a green line.
                    var card = new Vector2((px - 0.5f) / 7.5f, (py + 17.5f) / 4.2f).magnitude;
                    colour = Over(colour, cream, (1f - card) * 2f);
                    if (card < 1f && Mathf.Abs(py + 17.5f) < 0.7f) colour = Over(colour, green, 1f);
                    _iconTexture.SetPixel(x, y, colour);
                }
            }
            _iconTexture.Apply(false);
            var sprite = Sprite.Create(_iconTexture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            sprite.name = "CaiYaStation.Marker";
            return sprite;
        }

        private static Color Over(Color below, Color above, float coverage)
        {
            var alpha = Mathf.Clamp01(coverage) * above.a;
            if (alpha <= 0.0001f) return below;
            var outAlpha = alpha + below.a * (1f - alpha);
            if (outAlpha <= 0.0001f) return new Color(0f, 0f, 0f, 0f);
            var red = (above.r * alpha + below.r * below.a * (1f - alpha)) / outAlpha;
            var green = (above.g * alpha + below.g * below.a * (1f - alpha)) / outAlpha;
            var blue = (above.b * alpha + below.b * below.a * (1f - alpha)) / outAlpha;
            return new Color(red, green, blue, outAlpha);
        }

        private static Shader? FindShader()
        {
            foreach (var name in new[]
            {
                "Universal Render Pipeline/Lit", "Universal Render Pipeline/Unlit", "Universal Render Pipeline/Simple Lit",
                "Standard", "Unlit/Color", "Sprites/Default", "UI/Default"
            })
            {
                var shader = Shader.Find(name);
                if (shader != null) return shader;
            }
            return null;
        }

        private static Material? FindTemplateMaterial()
        {
            var actor = CharacterMainControl.Main;
            if (actor != null && actor.characterModel != null)
            {
                foreach (var renderer in actor.characterModel.GetComponentsInChildren<Renderer>(true))
                    if (renderer != null && renderer.sharedMaterial != null && renderer.sharedMaterial.shader != null)
                        return renderer.sharedMaterial;
            }
            foreach (var renderer in UnityEngine.Object.FindObjectsOfType<Renderer>())
            {
                if (renderer == null || renderer.sharedMaterial == null || renderer.sharedMaterial.shader == null) continue;
                if (!renderer.gameObject.scene.IsValid() || !renderer.gameObject.scene.isLoaded) continue;
                return renderer.sharedMaterial;
            }
            return null;
        }

        private static Material Tint(Material template, Color color, bool emissive = false)
        {
            var material = new Material(template) { hideFlags = HideFlags.DontSave };
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", null);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", null);
            if (material.HasProperty("_EmissionColor"))
            {
                if (emissive)
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", color * 0.45f);
                }
                else
                {
                    material.DisableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", Color.black);
                }
            }
            return material;
        }

        private static Material Make(Shader shader, Color color, bool emissive = false)
        {
            var material = new Material(shader) { hideFlags = HideFlags.DontSave };
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (emissive && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 0.45f);
            }
            return material;
        }

        private static Transform Anchor(GameObject root, string name, Vector3 position)
        {
            var anchor = new GameObject(name).transform;
            anchor.SetParent(root.transform, false);
            anchor.localPosition = position;
            return anchor;
        }

        private static GameObject Shape(GameObject root, PrimitiveType type, Vector3 position, Vector3 scale,
            Material? material, Quaternion? rotation = null)
        {
            var part = GameObject.CreatePrimitive(type);
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            if (rotation.HasValue) part.transform.localRotation = rotation.Value;
            var collider = part.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.Destroy(collider);
            if (material != null) part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }

        public void Dispose()
        {
            foreach (var material in new[] { _frame, _dark, _metal, _sign, _used, _beak, _glow, _flag, _wood, _cream, _roof, _paper, _rope, _red, _burlap })
                if (material != null) UnityEngine.Object.Destroy(material);
            if (_icon != null) UnityEngine.Object.Destroy(_icon);
            if (_iconTexture != null) UnityEngine.Object.Destroy(_iconTexture);
            if (_signFont != null) UnityEngine.Object.Destroy(_signFont);
            _icon = null;
            _iconTexture = null;
            _signFont = null;
        }
    }

    /// <summary>
    /// Dynamic-font TextMesh has no reliable world size before its first render. Fit it to the
    /// sign once the renderer reports bounds, then stop touching the transform.
    /// </summary>
    public sealed class WorldTextFit : MonoBehaviour
    {
        internal float MaxWidth = 1.4f;
        internal float MaxHeight = 0.45f;
        private bool _fitted;

        private void LateUpdate()
        {
            if (_fitted) return;
            var renderer = GetComponent<Renderer>();
            if (renderer == null) return;
            var size = renderer.bounds.size;
            if (size.x <= 0.005f || size.y <= 0.005f) return;
            var scale = Mathf.Min(MaxWidth / size.x, MaxHeight / size.y);
            if (scale <= 0.01f || scale >= 50f) return;
            transform.localScale *= scale;
            _fitted = true;
        }
    }

    internal sealed class StationViewParts
    {
        internal Renderer?[] SignRenderers = Array.Empty<Renderer?>();
        internal Material? ActiveSign;
        internal Material? UsedSign;
        internal Transform? FlagPivot;
        internal Renderer? FlagRenderer;
        internal Material? ActiveFlag;
        internal Transform? DuckHead;
        internal Renderer? DuckRenderer;
        internal Material? ActiveDuck;
        internal Transform?[] Lanterns = Array.Empty<Transform?>();
        internal GameObject? Shutter;
        internal Light? LanternLight;
    }

    public sealed class StationView : MonoBehaviour
    {
        private Renderer?[] _signRenderers = Array.Empty<Renderer?>();
        private Material? _activeSign;
        private Material? _usedSign;
        private Transform? _flagPivot;
        private Renderer? _flagRenderer;
        private Material? _activeFlag;
        private Transform? _duckHead;
        private Renderer? _duckRenderer;
        private Material? _activeDuck;
        private Transform?[] _lanterns = Array.Empty<Transform?>();
        private Vector3[] _lanternScales = Array.Empty<Vector3>();
        private GameObject? _shutter;
        private Light? _lanternLight;
        private float _baseLightIntensity;
        private Quaternion _duckRest = Quaternion.identity;
        private float _phase;
        private bool _used;

        internal void Configure(StationViewParts parts)
        {
            _signRenderers = parts.SignRenderers ?? Array.Empty<Renderer?>();
            _activeSign = parts.ActiveSign;
            _usedSign = parts.UsedSign;
            _flagPivot = parts.FlagPivot;
            _flagRenderer = parts.FlagRenderer;
            _activeFlag = parts.ActiveFlag;
            _duckHead = parts.DuckHead;
            _duckRenderer = parts.DuckRenderer;
            _activeDuck = parts.ActiveDuck;
            _lanterns = parts.Lanterns ?? Array.Empty<Transform?>();
            _shutter = parts.Shutter;
            _lanternLight = parts.LanternLight;
            _baseLightIntensity = _lanternLight != null ? _lanternLight.intensity : 0f;
            _lanternScales = new Vector3[_lanterns.Length];
            for (var index = 0; index < _lanterns.Length; index++)
                _lanternScales[index] = _lanterns[index] != null ? _lanterns[index]!.localScale : Vector3.one;
            _duckRest = _duckHead != null ? _duckHead.localRotation : Quaternion.identity;
            _phase = UnityEngine.Random.value * Mathf.PI * 2f;
        }

        internal void SetUsed(bool used)
        {
            _used = used;
            foreach (var renderer in _signRenderers)
            {
                if (renderer == null) continue;
                var material = used ? _usedSign : _activeSign;
                if (material != null) renderer.sharedMaterial = material;
            }
            if (_flagRenderer != null)
            {
                var material = used ? _usedSign : _activeFlag;
                if (material != null) _flagRenderer.sharedMaterial = material;
            }
            if (_duckRenderer != null)
            {
                var material = used ? _usedSign : _activeDuck;
                if (material != null) _duckRenderer.sharedMaterial = material;
            }
            if (_shutter != null) _shutter.SetActive(used);
            for (var index = 0; index < _lanterns.Length; index++)
                if (_lanterns[index] != null) _lanterns[index]!.gameObject.SetActive(!used);
            if (_lanternLight != null)
            {
                _lanternLight.enabled = !used;
                _lanternLight.intensity = _baseLightIntensity;
            }
            if (_flagPivot != null) _flagPivot.localRotation = Quaternion.identity;
            if (_duckHead != null) _duckHead.localRotation = _duckRest;
        }

        private void Update()
        {
            if (_used || Time.timeScale <= 0) return;
            var time = Time.time + _phase;
            if (_flagPivot != null)
                _flagPivot.localRotation = Quaternion.Euler(
                    Mathf.Sin(time * 1.1f) * 1.5f, 0f, Mathf.Sin(time * 1.7f) * 6f);
            for (var index = 0; index < _lanterns.Length; index++)
            {
                var lantern = _lanterns[index];
                if (lantern == null || !lantern.gameObject.activeSelf) continue;
                var pulse = 1f + Mathf.Sin(time * 2.1f + index * 0.9f) * 0.045f;
                lantern.localScale = _lanternScales[index] * pulse;
            }
            if (_lanternLight != null && _lanternLight.enabled)
                _lanternLight.intensity = _baseLightIntensity * (0.94f + Mathf.Sin(time * 2.1f) * 0.06f);
            if (_duckHead != null)
                _duckHead.localRotation = _duckRest * Quaternion.Euler(0f, Mathf.Sin(time * 0.8f) * 4f, 0f);
        }
    }

    /// <summary>
    /// Decides whether a kiosk can stand on a point. The first version only asked for "any
    /// collider under a 2.5m navmesh snap", which let the river surface, railing tops and bridge
    /// edges count as ground: a kiosk could land two metres below the road, behind a fence, and
    /// be permanently out of reach. The checks below are the ones that actually matter:
    /// a node on the game's own pathfinding graph, solid ground that agrees with it, dry ground
    /// (not under water), a flat footprint wide enough for the booth, and a walk that a player
    /// would really make.
    /// </summary>
    internal static class SafeGround
    {
        /// <summary>Half extents of the kiosk foundation (3.8 x 2.9 in <see cref="CreateStation"/>).</summary>
        internal const float FootprintHalfX = 1.9f;
        internal const float FootprintHalfZ = 1.45f;

        private static int _groundMask;
        private static int _waterMask;
        private static bool _masksReady;

        /// <summary>Layers that are never floor: water, triggers, camera tricks.</summary>
        private static void EnsureMasks()
        {
            if (_masksReady) return;
            _masksReady = true;
            var water = LayerMask.NameToLayer("Water");
            var trigger = LayerMask.NameToLayer("Trigger");
            var miniGame = LayerMask.NameToLayer("MiniGame");
            var fullScreen = LayerMask.NameToLayer("FullScreenFx");
            var ignore = LayerMask.NameToLayer("Ignore Raycast");
            var excluded = 0;
            foreach (var layer in new[] { water, trigger, miniGame, fullScreen, ignore })
                if (layer >= 0) excluded |= 1 << layer;
            _groundMask = ~excluded;
            _waterMask = water >= 0 ? 1 << water : 0;
        }

        internal static int GroundMask
        {
            get { EnsureMasks(); return _groundMask; }
        }

        /// <summary>
        /// Solid ground under a point, if any. Slope and layer are both checked here so that the
        /// rest of the placement code cannot forget one of them.
        /// </summary>
        private static bool SampleGround(Vector3 point, float maxSlope, out Vector3 ground, out float groundY,
            out Vector3 normal)
        {
            EnsureMasks();
            ground = default;
            groundY = 0f;
            normal = Vector3.up;
            if (!Physics.Raycast(point + Vector3.up * 2f, Vector3.down, out var hit, 6f, _groundMask,
                    QueryTriggerInteraction.Ignore))
                return false;
            if (Vector3.Angle(hit.normal, Vector3.up) > maxSlope) return false;
            ground = hit.point;
            groundY = hit.point.y;
            normal = hit.normal;
            return true;
        }

        private static bool SampleGround(Vector3 point, float maxSlope, out Vector3 ground, out float groundY)
            => SampleGround(point, maxSlope, out ground, out groundY, out _);

        /// <summary>
        /// True when a water surface sits above the candidate: the "ground" there is the river
        /// bed, so the kiosk would be standing in the water.
        /// </summary>
        private static bool UnderWater(Vector3 point)
        {
            EnsureMasks();
            if (_waterMask == 0) return false;
            if (!Physics.Raycast(point + Vector3.up * 8f, Vector3.down, out var water, 16f, _waterMask,
                    QueryTriggerInteraction.Ignore))
                return false;
            return water.point.y > point.y + 0.05f;
        }

        /// <summary>
        /// The booth needs a level pad. Each base corner is compared against the plane the centre
        /// ground follows, so a gentle slope passes while a kerb, a railing top, a bridge edge or
        /// a river bank (where a corner drops away) fails.
        /// </summary>
        private static bool FootprintFlat(Vector3 center, Vector3 normal, float yaw, float centerY, float maxSlope,
            out string reason)
        {
            reason = "";
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            foreach (var x in new[] { -FootprintHalfX, FootprintHalfX })
            {
                foreach (var z in new[] { -FootprintHalfZ, FootprintHalfZ })
                {
                    var offset = rotation * new Vector3(x, 0f, z);
                    var corner = center + offset;
                    if (!SampleGround(corner, maxSlope, out var cornerGround, out var cornerY))
                    {
                        reason = "corner-over-nothing";
                        return false;
                    }
                    var predicted = PlacementPolicy.PredictedCornerY(centerY, normal.x, normal.y, normal.z,
                        offset.x, offset.z);
                    if (!PlacementPolicy.CornerOnPlane(cornerY, predicted))
                    {
                        reason = "corner-step-" + Mathf.Abs(cornerY - (float)predicted).ToString("0.00");
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>Nothing may already occupy the volume the booth will fill.</summary>
        private static bool FootprintClear(Vector3 center, float yaw, CharacterMainControl? actor)
        {
            var half = new Vector3(FootprintHalfX - 0.15f, 1.0f, FootprintHalfZ - 0.15f);
            var boxCenter = center + Vector3.up * 1.15f;
            foreach (var collider in Physics.OverlapBox(boxCenter, half, Quaternion.Euler(0f, yaw, 0f),
                         GroundMask, QueryTriggerInteraction.Ignore))
            {
                if (collider == null) continue;
                if (actor != null && collider.transform.IsChildOf(actor.transform)) continue;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Unobstructed view between two points. The interaction prompt has to sit in front of
        /// the counter, not behind a railing or across a river channel.
        /// </summary>
        internal static bool LineOfSight(Vector3 from, Vector3 to)
        {
            EnsureMasks();
            var delta = to - from;
            var distance = delta.magnitude;
            if (distance < 0.01f) return true;
            return !Physics.Raycast(from, delta / distance, distance, GroundMask, QueryTriggerInteraction.Ignore);
        }

        internal static bool Find(Vector3 origin, Vector3 candidate, CharacterMainControl? actor, float maxSlope,
            float yaw, out Vector3 result, out float pathLength, out string reason)
        {
            result = default;
            pathLength = float.MaxValue;
            reason = "";

            // The station has to stand on a node the game itself considers walkable, so the point
            // that gets built is the snapped node, not the raw candidate. That single change is
            // what kills the original bug: a candidate on a river surface or a railing top has no
            // walkable node under it, so snapping pulls the point onto the nearest real floor and
            // the gap between the two exposes how far off the anchor really was.
            if (!PathProbe.TrySnap(candidate, out var snapped, out reason)) return false;
            var snapGap = Vector3.Distance(snapped, candidate);
            if (snapGap > PathProbe.SnapTolerance)
            {
                reason = "graph-gap-" + snapGap.ToString("0.00");
                return false;
            }

            // Ground is sampled under the node itself. Sampling under the raw candidate would
            // compare a point metres up a slope against the node and reject honest hillsides; at
            // the node, a mismatch means the node floats over water or is buried in a wall.
            if (!SampleGround(snapped, maxSlope, out var ground, out var groundY, out var normal))
            {
                reason = "no-ground";
                return false;
            }
            if (!PlacementPolicy.GraphAligned(groundY, snapped.y))
            {
                reason = "graph-step-" + Mathf.Abs(groundY - snapped.y).ToString("0.00");
                return false;
            }
            if (UnderWater(ground))
            {
                reason = "under-water";
                return false;
            }
            if (!FootprintFlat(ground, normal, yaw, groundY, maxSlope, out reason))
                return false;
            if (!FootprintClear(ground, yaw, actor))
            {
                reason = "occupied";
                return false;
            }
            var stand = ground + Vector3.up * 0.03f;
            if (actor != null)
            {
                if (!PathProbe.TryMeasure(origin, stand, out pathLength, out reason)) return false;
                if (!PlacementPolicy.PathIsReasonable(pathLength, Vector3.Distance(origin, stand)))
                {
                    reason = "detour-" + pathLength.ToString("0") + "m";
                    return false;
                }
            }
            result = stand;
            return true;
        }

        /// <summary>
        /// Re-checks an already-placed station. Saves written before the placement fix contain
        /// stations that sit in a river or behind a railing; without this they would stay
        /// unreachable for the rest of the raid.
        /// </summary>
        internal static bool IsStandable(Vector3 position, float yaw, CharacterMainControl? actor, float maxSlope,
            out string reason)
        {
            reason = "";
            if (!PathProbe.TrySnap(position, out var snapped, out reason)) return false;
            var snapGap = Vector3.Distance(snapped, position);
            if (snapGap > PathProbe.SnapTolerance)
            {
                reason = "graph-gap-" + snapGap.ToString("0.00");
                return false;
            }
            if (!SampleGround(position, maxSlope, out var ground, out var groundY, out var normal))
            {
                reason = "no-ground";
                return false;
            }
            if (!PlacementPolicy.GraphAligned(groundY, snapped.y))
            {
                reason = "graph-step-" + Mathf.Abs(groundY - snapped.y).ToString("0.00");
                return false;
            }
            if (UnderWater(ground))
            {
                reason = "under-water";
                return false;
            }
            // The saved coordinate itself has to sit on that ground. Without this the check would
            // pass for a kiosk buried two metres under the river bed, because the nearest walkable
            // node is on the bank above it.
            if (Mathf.Abs(position.y - groundY) > 0.05f)
            {
                reason = "off-ground-" + Mathf.Abs(position.y - groundY).ToString("0.00");
                return false;
            }
            if (!FootprintFlat(ground, normal, yaw, groundY, maxSlope, out reason))
                return false;
            // Deliberately no path check here: the saved station may be checked while the player
            // stands on the other side of the map, and a long legitimate walk must not be read
            // as a broken one. Route quality is enforced when the replacement point is chosen.
            return true;
        }

        internal static Quaternion GroundRotation(Vector3 position, float yaw, CharacterMainControl? actor)
        {
            EnsureMasks();
            var normal = Vector3.up;
            var best = float.PositiveInfinity;
            foreach (var hit in Physics.RaycastAll(position + Vector3.up * 0.6f, Vector3.down, 2.5f, _groundMask,
                         QueryTriggerInteraction.Ignore))
            {
                if (actor != null && hit.collider.transform.IsChildOf(actor.transform)) continue;
                if (hit.distance >= best) continue;
                if (Vector3.Angle(hit.normal, Vector3.up) > 35) continue;
                best = hit.distance;
                normal = hit.normal;
            }
            var forward = Vector3.ProjectOnPlane(Quaternion.Euler(0, yaw, 0) * Vector3.forward, normal).normalized;
            return forward.sqrMagnitude < 0.0001f ? Quaternion.Euler(0, yaw, 0) : Quaternion.LookRotation(forward, normal);
        }
    }

    /// <summary>
    /// Runtime-created InteractableBase needs its serialized lists seeded before Awake;
    /// otherwise the native Awake enumerates null and throws, leaving the prompt dead.
    /// </summary>
    public sealed class StationInteractable : InteractableBase
    {
        private static readonly FieldInfo? GroupField = typeof(InteractableBase).GetField("otherInterablesInGroup", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo? ListField = typeof(InteractableBase).GetField("_interactbleList", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo? TimeField = typeof(InteractableBase).GetField("interactTime", BindingFlags.NonPublic | BindingFlags.Instance);

        internal StationWorld? World;
        internal string StationId = "";

        protected override void Awake()
        {
            try { if (GroupField != null && GroupField.GetValue(this) == null) GroupField.SetValue(this, new List<InteractableBase>()); }
            catch (Exception exception) { Debug.LogWarning("[CaiYaStation] interactable group init failed: " + exception.Message); }
            try { base.Awake(); }
            catch (Exception exception) { Debug.LogWarning("[CaiYaStation] interactable base wake failed: " + exception.Message); }
            try
            {
                if (ListField != null && ListField.GetValue(this) == null) ListField.SetValue(this, new List<InteractableBase>());
                finishWhenTimeOut = true;
                interactMarkerOffset = Vector3.up * 1.6f;
                InteractName = "菜鸭驿站";
                TimeField?.SetValue(this, 0.25f);
            }
            catch (Exception exception) { Debug.LogWarning("[CaiYaStation] interactable setup failed: " + exception.Message); }
        }

        protected override bool IsInteractable()
        {
            if (World == null || !World.Ready) return false;
            return World.CanOpen(StationId);
        }

        protected override void OnInteractFinished()
        {
            World?.OpenStation(StationId);
            StopInteract();
        }
    }
}
