using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace GK2.MapMarkers.Map
{
    /// <summary>
    /// World -> map conversion. Mirrors MapPageWidget.UpdatePlayerPos / UpdateMilestones exactly so
    /// markers line up with the game's own player icon and teleport milestones.
    /// </summary>
    internal sealed class MapProjection
    {
        private static readonly FieldInfo WorldZonePointsField = AccessTools.Field(typeof(MapPageWidget), "worldZonePoints");

        private readonly float minX, maxX, minZ, maxZ;
        private readonly float tan;
        private readonly Vector2 mapSize;
        private readonly Rect worldRect;
        private readonly Dictionary<string, Vector2> zonePoints;

        private MapProjection(Transform worldMin, Transform worldMax, Vector2 mapSize, Dictionary<string, Vector2> zonePoints)
        {
            minX = worldMin.position.x;
            maxX = worldMax.position.x;
            minZ = worldMin.position.z;
            maxZ = worldMax.position.z;
            // Same (odd but authoritative) formula as the game: uses the quaternion's x component.
            tan = Mathf.Tan(MathF.PI / 180f * worldMin.rotation.x);
            this.mapSize = mapSize;
            this.zonePoints = zonePoints;
            float x0 = Mathf.Min(minX, maxX), x1 = Mathf.Max(minX, maxX);
            float z0 = Mathf.Min(minZ, maxZ), z1 = Mathf.Max(minZ, maxZ);
            worldRect = new Rect(x0, z0, x1 - x0, z1 - z0);
        }

        public static MapProjection TryCreate(MapPageWidget widget, RectTransform mapRect)
        {
            GUIElements gui = GUIElements.Instance;
            if (gui == null || gui.WorldMin == null || gui.WorldMax == null || mapRect == null)
            {
                return null;
            }
            return new MapProjection(gui.WorldMin, gui.WorldMax, mapRect.sizeDelta, ReadZonePoints(widget));
        }

        /// <summary>Returns true when the position can be shown. <paramref name="inInterior"/> is set when a world-zone anchor was used.</summary>
        public bool TryProject(Vector3 position, string worldZoneId, out Vector2 mapPosition, out bool inInterior)
        {
            inInterior = false;
            float projectedZ = position.z + position.y * tan;
            if (worldRect.Contains(new Vector2(position.x, projectedZ)))
            {
                float u = Mathf.InverseLerp(minX, maxX, position.x);
                float v = Mathf.InverseLerp(minZ, maxZ, projectedZ);
                mapPosition = new Vector2((u - 0.5f) * mapSize.x, (v - 0.5f) * mapSize.y);
                return true;
            }
            if (!string.IsNullOrEmpty(worldZoneId) && zonePoints.TryGetValue(worldZoneId, out mapPosition))
            {
                inInterior = true;
                return true;
            }
            mapPosition = default;
            return false;
        }

        private static Dictionary<string, Vector2> ReadZonePoints(MapPageWidget widget)
        {
            var result = new Dictionary<string, Vector2>();
            if (WorldZonePointsField == null || !(WorldZonePointsField.GetValue(widget) is IList list))
            {
                return result;
            }
            foreach (object point in list)
            {
                if (point == null)
                {
                    continue;
                }
                Traverse t = Traverse.Create(point);
                string id = t.Field("worldZoneId").GetValue<string>();
                if (!string.IsNullOrEmpty(id) && !result.ContainsKey(id))
                {
                    result.Add(id, t.Field("mapPosition").GetValue<Vector2>());
                }
            }
            return result;
        }
    }
}
