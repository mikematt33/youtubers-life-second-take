using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SecondTake.Visuals
{
    [BepInPlugin("mikematt33.secondtake.visuals", "Second Take Visuals", "0.1.0")]
    [BepInProcess("YoutubersLife.exe")]
    public sealed class BedroomVisuals : BaseUnityPlugin
    {
        private ConfigEntry<bool> enabledPreset;
        private ConfigEntry<bool> wideView;
        private ConfigEntry<float> fillStrength;
        private ConfigEntry<float> textureBias;
        private ConfigEntry<KeyboardShortcut> toggleKey;
        private ConfigEntry<KeyboardShortcut> viewKey;
        private Light fill;
        private Camera renderingCamera;
        private CameraMove framedController;
        private float previousDistance;
        private float presetDistance;
        private int roomHandle;
        private bool inRoom;
        private float nextScan;
        private readonly Dictionary<Texture2D, TextureState> originals = new Dictionary<Texture2D, TextureState>();

        private struct TextureState
        {
            public FilterMode Filter;
            public int Anisotropy;
            public float Bias;
        }

        private void Awake()
        {
            enabledPreset = Config.Bind("Bedroom", "Enabled", true, "Use the visual preset in the first home.");
            wideView = Config.Bind("Camera", "WideView", false, "Start with a wider view on the room camera.");
            fillStrength = Config.Bind("Bedroom", "FillIntensity", 0.16f, "Warm camera-side fill intensity, from 0 to 1.");
            textureBias = Config.Bind("Bedroom", "TextureMipBias", -0.35f, "Mip bias adjustment for room surfaces, from -1 to 0. Set to 0 to disable texture tuning.");
            toggleKey = Config.Bind("Keys", "Compare", new KeyboardShortcut(KeyCode.F6), "Toggle the bedroom preset.");
            viewKey = Config.Bind("Keys", "Framing", new KeyboardShortcut(KeyCode.F7), "Toggle wide framing.");
            Logger.LogInfo("Bedroom preset ready. F6 compares the preset; F7 switches camera framing.");
        }

        private void OnEnable()
        {
            Camera.onPreCull += BeforeCamera;
            Camera.onPostRender += AfterCamera;
        }

        private void Update()
        {
            if (Loader.ActualScene != Loader.Escenario.Casa1)
            {
                if (inRoom) LeaveRoom();
                return;
            }
            if (toggleKey.Value.IsDown()) enabledPreset.Value = !enabledPreset.Value;
            if (viewKey.Value.IsDown()) wideView.Value = !wideView.Value;
            var scene = SceneManager.GetActiveScene();
            if (!enabledPreset.Value || (inRoom && scene.handle != roomHandle))
            {
                if (inRoom) LeaveRoom();
            }
            if (!enabledPreset.Value || !scene.isLoaded || !CameraMove.instance) return;
            if (!inRoom)
            {
                inRoom = true;
                roomHandle = scene.handle;
                nextScan = 0;
                var lightObject = new GameObject("Second Take - bedroom fill");
                lightObject.transform.SetParent(transform, false);
                fill = lightObject.AddComponent<Light>();
                fill.enabled = false;
                fill.type = LightType.Directional;
                fill.color = new Color(1f, 0.92f, 0.83f);
                fill.shadows = LightShadows.None;
                Logger.LogInfo("Bedroom preset active in " + scene.name);
            }
            UpdateFraming(CameraMove.instance);
            // Furniture can arrive after the room camera. Scan occasionally, outside the draw callbacks.
            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + 5f;
                ScanTextures(scene);
            }
        }

        private void UpdateFraming(CameraMove controller)
        {
            if (framedController && (!wideView.Value || framedController != controller)) RestoreFraming();
            if (!wideView.Value || framedController || !controller.isActiveAndEnabled ||
                !controller.canScroll || controller.distanceMax <= controller.distanceMin) return;
            previousDistance = controller.currentDistance;
            presetDistance = Mathf.Max(previousDistance, Mathf.Lerp(controller.distanceMin, controller.distanceMax, 0.88f));
            controller.currentDistance = presetDistance;
            framedController = controller;
        }

        private void RestoreFraming()
        {
            // Keep any manual zoom the player made after using the preset.
            if (framedController && Mathf.Approximately(framedController.currentDistance, presetDistance))
                framedController.currentDistance = previousDistance;
            framedController = null;
        }

        private void ScanTextures(Scene scene)
        {
            float bias = Mathf.Clamp(textureBias.Value, -1f, 0f);
            if (bias == 0f) return;
            foreach (var renderer in Object.FindObjectsOfType<MeshRenderer>())
            {
                if (renderer.gameObject.scene != scene || renderer.GetComponentInParent<Canvas>()) continue;
                foreach (var material in renderer.sharedMaterials)
                {
                    if (!material || !material.HasProperty("_MainTex")) continue;
                    var texture = material.GetTexture("_MainTex") as Texture2D;
                    if (!texture || texture.mipmapCount <= 1 || texture.filterMode == FilterMode.Point || originals.ContainsKey(texture)) continue;
                    originals.Add(texture, new TextureState { Filter = texture.filterMode, Anisotropy = texture.anisoLevel, Bias = texture.mipMapBias });
                    texture.filterMode = FilterMode.Trilinear;
                    texture.anisoLevel = Mathf.Max(4, texture.anisoLevel);
                    texture.mipMapBias = Mathf.Clamp(texture.mipMapBias + bias, -3f, 3f);
                }
            }
        }

        private void BeforeCamera(Camera camera)
        {
            var controller = CameraMove.instance;
            if (!inRoom || !fill || !controller || !controller.isActiveAndEnabled ||
                camera.gameObject != controller.gameObject || camera.targetTexture || Loader.ActualScene != Loader.Escenario.Casa1) return;
            EndCamera();
            renderingCamera = camera;
            fill.transform.rotation = camera.transform.rotation * Quaternion.Euler(12f, -32f, 0f);
            fill.cullingMask = camera.cullingMask;
            fill.intensity = Mathf.Clamp01(fillStrength.Value);
            fill.enabled = fill.intensity > 0f;
        }

        private void AfterCamera(Camera camera)
        {
            if (camera == renderingCamera) EndCamera();
        }

        private void EndCamera()
        {
            renderingCamera = null;
            if (fill) fill.enabled = false;
        }

        private void LeaveRoom()
        {
            EndCamera();
            RestoreFraming();
            if (fill) Destroy(fill.gameObject);
            fill = null;
            foreach (var pair in originals)
            {
                if (!pair.Key) continue;
                pair.Key.filterMode = pair.Value.Filter;
                pair.Key.anisoLevel = pair.Value.Anisotropy;
                pair.Key.mipMapBias = pair.Value.Bias;
            }
            originals.Clear();
            inRoom = false;
        }

        private void OnDisable()
        {
            Camera.onPreCull -= BeforeCamera;
            Camera.onPostRender -= AfterCamera;
            LeaveRoom();
        }
    }
}
