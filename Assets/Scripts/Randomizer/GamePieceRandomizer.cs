using System;
using System.Collections.Generic;
using Tags;
using UnityEngine;
using UnityEngine.Perception.Randomization.Parameters;
using UnityEngine.Perception.Randomization.Randomizers;
using UnityEngine.Perception.Randomization.Samplers;
using Random = UnityEngine.Random;

namespace Randomizer
{
    [Serializable]
    [AddRandomizerMenu("Custom/GamePiecePlacementRandomizer")]
    public class GamePiecePlacementRandomizer : UnityEngine.Perception.Randomization.Randomizers.Randomizer
    {
        [Tooltip("Region component that returns random points in world space")]
        public PolyRegionRandomSampler regionSampler;

        [Tooltip("Random vertical offset (in metres, positive moves piece downward)")]
        public FloatParameter heightOffset = new() { value = new UniformSampler(-0.002f, 0.002f) };

        [Tooltip("Minimum separation between pieces (centre‑to‑centre, metres)")]
        public FloatParameter minSeparation = new() { value = new UniformSampler(0.3f, 0.7f) };

        [Tooltip("Maximum attempts before giving up on a piece")]
        public int maxAttempts = 30;

        [Tooltip("Random scale for each axis (uniformly sampled)")]
        public Vector3Parameter scaleParameter = new()
        {
            x = new UniformSampler(0.8f, 1.2f),
            y = new UniformSampler(0.8f, 1.2f),
            z = new UniformSampler(0.8f, 1.2f)
        };

        public bool randomizeRotation = true;

        readonly Dictionary<GameObject, Bounds> cachedBounds = new();
        readonly List<Vector3> placedCentres = new();

        protected override void OnIterationStart()
        {
            placedCentres.Clear();
            var tags = tagManager.Query<GamePiecePlacementTag>();

            foreach (var tag in tags)
                TryPlace(tag.gameObject);
        }

        #region Placementhelpers

        bool TryPlace(GameObject go)
        {
            go.transform.localScale = scaleParameter.Sample();

            if (randomizeRotation)
                go.transform.rotation = Random.rotationUniform;

            if (regionSampler == null)
                return true;

            var r = GetBounds(go).extents.magnitude;

            for (int i = 0; i < maxAttempts; ++i)
            {
                var pos = regionSampler.SampleWorldSpace();
                pos.y -= heightOffset.Sample();

                if (IsFarEnough(pos, r))
                {
                    go.transform.position = pos;
                    placedCentres.Add(pos);
                    return true;
                }
            }

            return false;
        }

        bool IsFarEnough(Vector3 candidate, float radius)
        {
            var minDist = minSeparation.Sample();
            foreach (var c in placedCentres)
                if (Vector3.Distance(candidate, c) < minDist + radius)
                    return false;
            return true;
        }

        Bounds GetBounds(GameObject go)
        {
            if (cachedBounds.TryGetValue(go, out var b)) return b;

            var renderers = go.GetComponentsInChildren<Renderer>();
            b = renderers.Length > 0
                ? renderers[0].bounds
                : new Bounds(go.transform.position, Vector3.one * 0.1f);

            for (int i = 1; i < renderers.Length; ++i)
                b.Encapsulate(renderers[i].bounds);

            cachedBounds[go] = b;
            return b;
        }

        #endregion
    }
}