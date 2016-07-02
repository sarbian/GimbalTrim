using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

namespace GimbalTrim
{
    [KSPModule("Trim")]
    public class GimbalTrim : PartModule
    {
        // name of the transform of the gimbal we control
        [KSPField(isPersistant = false)]
        public string gimbalTransformName = "thrustTransform";

        // Copy of the default engine rotation
        public List<Quaternion> originalInitalRots;

        private ModuleGimbal gimbal;
        
        //[KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Auto-Trim Limit"),
        // UI_FloatEdit(minValue = 0f, maxValue = 90f, scene = UI_Scene.Editor, stepIncrement = 5f)]
        [KSPField(isPersistant = false)]
        public float trimRange = 30f;

        [KSPField]
        public float trimRangeXP = -1f;

        [KSPField]
        public float trimRangeYP = -1f;

        [KSPField]
        public float trimRangeXN = -1f;

        [KSPField]
        public float trimRangeYN = -1f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "X-Trim"),
         UI_FloatRange(minValue = -14f, maxValue = 14f, stepIncrement = 0.5f)]
        public float trimX = 0;

        public float TrimX
        {
            get { return trimX; }
            set { trimX = Mathf.Clamp(value, -trimRangeXN, trimRangeXP); }
        }

        public float lastTrimX = 0;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Y-Trim"),
         UI_FloatRange(minValue = -14f, maxValue = 14f, stepIncrement = 0.5f)]
        public float trimY = 0;

        public float TrimY
        {
            get { return trimY; }
            set { trimY = Mathf.Clamp(value, -trimRangeYN, trimRangeYP); }
        }

        public float lastTrimY = 0;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Trim"),
         UI_Toggle(disabledText = "Disabled", enabledText = "Enabled")]
        public bool enableTrim = true;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Method"),
         UI_Toggle(disabledText = "Precise", enabledText = "Smooth")]
        public bool useTrimlResponseSpeed = false;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Speed"),
         UI_FloatRange(minValue = 1f, maxValue = 100.0f, stepIncrement = 1f)]
        public float trimResponseSpeed = 60;

        [KSPAction("X Trim +")]
        public void plusTrimX(KSPActionParam param)
        {
            TrimX += 1f;
        }

        [KSPAction("X Trim -")]
        public void minusTrim(KSPActionParam param)
        {
            TrimX -= 1f;
        }

        [KSPAction("X Trim +5")]
        public void plus5Trim(KSPActionParam param)
        {
            TrimX += 5f;
        }

        [KSPAction("X Trim -5")]
        public void minus5Trim(KSPActionParam param)
        {
            TrimX -= 5f;
        }

        [KSPAction("Y Trim +")]
        public void plusTrimY(KSPActionParam param)
        {
            TrimY += 1f;
        }

        [KSPAction("Y Trim -")]
        public void minusTrimY(KSPActionParam param)
        {
            TrimY -= 1f;
        }

        [KSPAction("Y Trim +5")]
        public void plus5TrimY(KSPActionParam param)
        {
            TrimY += 5f;
        }

        [KSPAction("Y Trim -5")]
        public void minus5TrimY(KSPActionParam param)
        {
            TrimY -= 5f;
        }

        [KSPAction("Toggle Trim")]
        public void toggleTrim(KSPActionParam param)
        {
            enableTrim = !enableTrim;
        }

        public override void OnLoad(ConfigNode node)
        {
            if (trimRangeXP < 0f)
            {
                trimRangeXP = trimRange;
            }
            if (trimRangeYP < 0f)
            {
                trimRangeYP = trimRange;
            }
            if (trimRangeXN < 0f)
            {
                trimRangeXN = trimRangeXP;
            }
            if (trimRangeYN < 0f)
            {
                trimRangeYN = trimRangeYP;
            }
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            gimbal = part.Modules.GetModules<ModuleGimbal>().FirstOrDefault(g => g.gimbalTransformName == gimbalTransformName);

            if (gimbal == null)
            {
                print("Could not find a ModuleGimbal with gimbalTransformName = " + gimbalTransformName);
                return;
            }

            for (int i = 0; i < gimbal.initRots.Count; i++)
            {
                originalInitalRots.Add(gimbal.initRots[i]);
            }

            BaseField trimXField = Fields["trimX"];
            trimXField.guiActive = trimRangeXP > 0 || trimRangeXN > 0;
            trimXField.guiActiveEditor = trimRangeXP > 0 || trimRangeXN > 0;
            UI_FloatRange trimXRange = (UI_FloatRange) (state == StartState.Editor ? trimXField.uiControlEditor : trimXField.uiControlFlight);
            trimXRange.minValue = -trimRangeXN;
            trimXRange.maxValue = trimRangeXP;
            trimXRange.stepIncrement = trimRangeXP + trimRangeXN >= 20f ? 1f : trimRangeXP + trimRangeXN >= 10f ? 0.5f : 0.25f;

            BaseField trimYField = Fields["trimY"];
            trimYField.guiActive = trimRangeYP > 0 || trimRangeYN > 0;
            trimYField.guiActiveEditor = trimRangeYP > 0 || trimRangeYN > 0;
            UI_FloatRange trimYRange = (UI_FloatRange) (state == StartState.Editor ? trimYField.uiControlEditor : trimYField.uiControlFlight);
            trimYRange.minValue = -trimRangeYN;
            trimYRange.maxValue = trimRangeYP;
            trimYRange.stepIncrement = trimRangeXN + trimRangeYN >= 10f ? 1f : trimRangeXN + trimRangeYN >= 5f ? 0.5f : 0.25f;
        }

        private float lastEditorTime = 0;

        public void Update()
        {
            if (gimbal == null)
                return;

            if (HighLogic.LoadedSceneIsEditor && Time.time > lastEditorTime + TimeWarp.fixedDeltaTime)
            {
                lastEditorTime = Time.time;
                FixedUpdate();
                int count = originalInitalRots.Count;
                for (int i = 0; i < count; i++)
                {
                    gimbal.gimbalTransforms[i].localRotation = gimbal.initRots[i];
                }
            }
        }

        public void FixedUpdate()
        {
            if (gimbal == null)
                return;
            int count = originalInitalRots.Count;
            for (int i = 0; i < count; i++)
            {
                float x = enableTrim ? trimX : 0;
                float y = enableTrim ? trimY : 0;

                if (useTrimlResponseSpeed)
                {
                    float timeFactor = trimResponseSpeed * TimeWarp.fixedDeltaTime;
                    x = Mathf.Lerp(lastTrimX, x, timeFactor);
                    y = Mathf.Lerp(lastTrimY, y, timeFactor);
                }
                lastTrimX = x;
                lastTrimY = y;

                gimbal.initRots[i] = originalInitalRots[i] * Quaternion.AngleAxis(x, Vector3.right) * Quaternion.AngleAxis(y, Vector3.up);
            }
        }

        public new static void print(object message)
        {
            MonoBehaviour.print("[GimbalTrim] " + message);
        }
    }
}