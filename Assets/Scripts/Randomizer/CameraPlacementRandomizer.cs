using System;
using System.Linq;
using Tags;
using UnityEngine;
using UnityEngine.Perception.Randomization.Parameters;
using UnityEngine.Perception.Randomization.Randomizers;
using UnityEngine.Perception.Randomization.Samplers;
using Random = UnityEngine.Random;

namespace Randomizer
{
    /// <summary>
    /// Randomizes a tagged camera’s position, height and orientation each iteration.
    /// </summary>
    [Serializable]
    [AddRandomizerMenu("Custom/Camera Placement Randomizer")]
    public class CameraPlacementRandomizer : UnityEngine.Perception.Randomization.Randomizers.Randomizer
    {
        [Tooltip("Polygon region used for in‑bounds sampling.")]
        public PolyRegionRandomSampler regionSampler;

        [Tooltip("Vertical height range above the sampled plane (m).")]
        public FloatParameter height = new() { value = new UniformSampler(0.5f, 2.5f) };

        [Tooltip("Extra radial offset beyond the region border (m, can be negative).")]
        public FloatParameter outsideOffset = new() { value = new UniformSampler(-0.3f, 0.6f) };

        [Tooltip("Yaw in degrees.")] public FloatParameter yaw = new() { value = new UniformSampler(0f, 360f) };

        [Tooltip("Pitch in degrees (down is positive).")]
        public FloatParameter pitch = new() { value = new UniformSampler(-25f, 25f) };

        [Tooltip("Roll in degrees.")] public FloatParameter roll = new() { value = new UniformSampler(-5f, 5f) };

        protected override void OnIterationStart()
        {
            // Find a camera to move
            var cam = tagManager.Query<MainCameraTag>().Any()
                ? tagManager.Query<MainCameraTag>().First().GetComponent<Camera>()
                : Camera.main;

            if (cam == null)
            {
                Debug.LogWarning($"{nameof(CameraPlacementRandomizer)}: No camera found to randomize.");
                return;
            }

            if (regionSampler == null)
            {
                Debug.LogError($"{nameof(CameraPlacementRandomizer)}: regionSampler missing.");
                return;
            }

            Vector3 basePos = regionSampler.SampleWorldSpace();

            Vector3 dir = Random.insideUnitSphere;
            dir.y = 0;
            dir.Normalize();
            basePos += dir * outsideOffset.Sample();

            basePos.y += height.Sample();

            cam.transform.position = basePos;
            cam.transform.rotation = Quaternion.Euler(pitch.Sample(), yaw.Sample(), roll.Sample());
        }
    }
}