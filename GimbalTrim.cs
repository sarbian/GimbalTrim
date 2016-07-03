using System.Collections.Generic;
using System.Linq;
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

        private float lastEditorTime = 0;
        
        private Vector3 currentTrim;
        private Vector3[] oldLocalTrim;

        [KSPField(isPersistant = false)]
        public bool limitToGimbalRange = false;

        // The range have 2 uses :
        // When limitToGimbalRange is false they store the max range of the trimming
        // When limitToGimbalRange is true they store the gimbal initial max range
        
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

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Y-Trim"),
         UI_FloatRange(minValue = -14f, maxValue = 14f, stepIncrement = 0.5f)]
        public float trimY = 0;

        public float TrimY
        {
            get { return trimY; }
            set { trimY = Mathf.Clamp(value, -trimRangeYN, trimRangeYP); }
        }

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Trim"),
         UI_Toggle(disabledText = "Disabled", enabledText = "Enabled")]
        public bool enableTrim = true;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Method"),
         UI_Toggle(disabledText = "Precise", enabledText = "Smooth")]
        public bool useTrimResponseSpeed = false;

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

        public void InitRange()
        {
            if (limitToGimbalRange)
            {
                trimRange = gimbal.gimbalRange;
                trimRangeXP = gimbal.gimbalRangeXP;
                trimRangeYP = gimbal.gimbalRangeYP;
                trimRangeXN = gimbal.gimbalRangeXN;
                trimRangeYN = gimbal.gimbalRangeYN;
            }

            //print("Ranges = " + trimRange.ToString("F1") + " " + trimRangeXN.ToString("F1") + " " + trimRangeXP.ToString("F1") + " " + trimRangeYN.ToString("F1") + " " + trimRangeYP.ToString("F1"));

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

            //print("Ranges = " + trimRangeXN.ToString("F1") + " " + trimRangeXP.ToString("F1") + " " + trimRangeYN.ToString("F1") + " " + trimRangeYP.ToString("F1"));
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

            InitRange();

            for (int i = 0; i < gimbal.initRots.Count; i++)
            {
                originalInitalRots.Add(gimbal.initRots[i]);
            }
            oldLocalTrim = new Vector3[gimbal.initRots.Count];

            BaseField trimXField = Fields["trimX"];
            trimXField.guiActive = trimRangeXP + trimRangeXN > 0;
            trimXField.guiActiveEditor = trimRangeXP + trimRangeXN > 0;
            UI_FloatRange trimXRange = (UI_FloatRange) (state == StartState.Editor ? trimXField.uiControlEditor : trimXField.uiControlFlight);
            trimXRange.minValue = -trimRangeXN;
            trimXRange.maxValue = trimRangeXP;
            trimXRange.stepIncrement = trimRangeXP + trimRangeXN >= 20f ? 1f : trimRangeXP + trimRangeXN >= 10f ? 0.5f : 0.25f;

            BaseField trimYField = Fields["trimY"];
            trimYField.guiActive = trimRangeYP + trimRangeYN > 0;
            trimYField.guiActiveEditor = trimRangeYP + trimRangeYN > 0;
            UI_FloatRange trimYRange = (UI_FloatRange) (state == StartState.Editor ? trimYField.uiControlEditor : trimYField.uiControlFlight);
            trimYRange.minValue = -trimRangeYN;
            trimYRange.maxValue = trimRangeYP;
            trimYRange.stepIncrement = trimRangeXN + trimRangeYN >= 10f ? 1f : trimRangeXN + trimRangeYN >= 5f ? 0.5f : 0.25f;
        }

        public void Update()
        {
            if (gimbal == null)
                return;

            if (HighLogic.LoadedSceneIsEditor && Time.time > lastEditorTime + TimeWarp.fixedDeltaTime)
            {
                lastEditorTime = Time.time;
                DoTrim(EditorLogic.RootPart.transform, EditorMarker_CoM.CraftCoM);
                int count = originalInitalRots.Count;
                for (int i = 0; i < count; i++)
                {
                    gimbal.gimbalTransforms[i].localRotation = gimbal.initRots[i];
                }
            }
        }

        public void FixedUpdate()
        {
            if (HighLogic.LoadedSceneIsEditor || gimbal == null)
                return;
           
            DoTrim(vessel.ReferenceTransform, vessel.CurrentCoM);
        }

        private void DoTrim(Transform vesselTransform, Vector3 CoM)
        {
            Vector3 localCoM = vesselTransform.transform.InverseTransformPoint(CoM);

            int count = originalInitalRots.Count;
            for (int i = 0; i < count; i++)
            {
                currentTrim.x = enableTrim ? trimX : 0;
                currentTrim.y = enableTrim ? trimY : 0;

                Transform gimbalTransform = gimbal.gimbalTransforms[i];

                Vector3 localPos = vesselTransform.InverseTransformPoint(gimbalTransform.position);
                float sign = 1f;
                if (localCoM.y < localPos.y)
                {
                    sign = -1f;
                }

                Vector3 localTrim = sign * currentTrim;

                if (useTrimResponseSpeed)
                {
                    float timeFactor = trimResponseSpeed * TimeWarp.fixedDeltaTime;
                    localTrim.x = Mathf.Lerp(oldLocalTrim[i].x, localTrim.x, timeFactor);
                    localTrim.y = Mathf.Lerp(oldLocalTrim[i].y, localTrim.y, timeFactor);
                    oldLocalTrim[i] = localTrim;
                }

                //print(localPos.ToString("F2") + " " + currentTrim.ToString("F2") + "  " + localTrim.ToString("F2") + " " + sign.ToString("F0"));

                gimbal.initRots[i] = originalInitalRots[i] * Quaternion.AngleAxis(localTrim.x, Vector3.right) * Quaternion.AngleAxis(localTrim.y, Vector3.up);

                if (limitToGimbalRange)
                {
                    gimbal.gimbalRangeXP = trimRangeXP - localTrim.x;
                    gimbal.gimbalRangeXN = trimRangeXN + localTrim.x;
                    gimbal.gimbalRangeYP = trimRangeYP - localTrim.y;
                    gimbal.gimbalRangeYN = trimRangeYN + localTrim.y;
                }
            }
        }

        public new static void print(object message)
        {
            MonoBehaviour.print("[GimbalTrim] " + message);
        }
    }
}