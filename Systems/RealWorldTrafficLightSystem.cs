using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

using ObjectSubObject = Game.Objects.SubObject;
using PrefabSubObject = Game.Prefabs.SubObject;

namespace RealWorldTrafficLights.Systems
{
    public sealed partial class RealWorldTrafficLightSystem : GameSystemBase
    {
        private const float kHalfTurnRadians = 3.1415927f;

        private EntityQuery m_NetPieceQuery;
        private EntityQuery m_CompositionObjectQuery;
        private EntityQuery m_PrefabSubObjectQuery;
        private EntityQuery m_LiveNetOwnerQuery;
        private Dictionary<Entity, bool> m_TrafficLightPrefabCache;
        private bool m_PrefabsPatched;
        private bool m_RefreshQueued;
        private int m_FramesBeforeRefresh;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_NetPieceQuery = GetEntityQuery(ComponentType.ReadWrite<NetPieceObject>());
            m_CompositionObjectQuery = GetEntityQuery(ComponentType.ReadWrite<NetCompositionObject>());
            m_PrefabSubObjectQuery = GetEntityQuery(ComponentType.ReadWrite<PrefabSubObject>());
            m_LiveNetOwnerQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<ObjectSubObject>(),
                    ComponentType.ReadOnly<PrefabRef>()
                },
                Any = new[]
                {
                    ComponentType.ReadOnly<Game.Net.Node>(),
                    ComponentType.ReadOnly<Game.Net.Edge>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });
            m_TrafficLightPrefabCache = new Dictionary<Entity, bool>();
            m_RefreshQueued = true;
            m_FramesBeforeRefresh = 2;
            Enabled = true;
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            QueueRefresh();
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            QueueRefresh();
        }

        protected override void OnUpdate()
        {
            if (!m_PrefabsPatched)
            {
                PatchResult result = PatchTrafficLightPlacements();
                if (result.TotalPatched > 0)
                {
                    m_PrefabsPatched = true;
                    m_RefreshQueued = true;
                    m_FramesBeforeRefresh = 2;
                    Mod.log.Info($"Mirrored traffic light placement data. Net pieces: {result.NetPieceObjects}, compositions: {result.CompositionObjects}, prefab subobjects: {result.PrefabSubObjects}.");
                }
            }

            if (m_RefreshQueued)
            {
                if (m_FramesBeforeRefresh > 0)
                {
                    m_FramesBeforeRefresh--;
                    return;
                }

                int refreshed = RefreshLiveNetOwners();
                m_RefreshQueued = false;
                Mod.log.Info($"Requested traffic light redraw on {refreshed} road node/edge object(s).");
            }

            if (m_PrefabsPatched && !m_RefreshQueued)
                Enabled = false;
        }

        private void QueueRefresh()
        {
            m_RefreshQueued = true;
            m_FramesBeforeRefresh = 2;
            Enabled = true;
        }

        private PatchResult PatchTrafficLightPlacements()
        {
            PatchResult result = default;
            m_TrafficLightPrefabCache.Clear();

            using (NativeArray<Entity> entities = m_NetPieceQuery.ToEntityArray(Allocator.Temp))
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    if (!EntityManager.HasBuffer<NetPieceObject>(entities[i]))
                        continue;

                    DynamicBuffer<NetPieceObject> buffer = EntityManager.GetBuffer<NetPieceObject>(entities[i]);
                    for (int j = 0; j < buffer.Length; j++)
                    {
                        NetPieceObject item = buffer[j];
                        if (!IsTrafficLightPrefab(item.m_Prefab, 0))
                            continue;

                        item.m_Position.z = -item.m_Position.z;
                        item.m_CurveOffsetRange = MirrorCurveRange(item.m_CurveOffsetRange);
                        item.m_Rotation = RotateHalfTurn(item.m_Rotation);
                        buffer[j] = item;
                        result.NetPieceObjects++;
                    }
                }
            }

            using (NativeArray<Entity> entities = m_CompositionObjectQuery.ToEntityArray(Allocator.Temp))
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    if (!EntityManager.HasBuffer<NetCompositionObject>(entities[i]))
                        continue;

                    DynamicBuffer<NetCompositionObject> buffer = EntityManager.GetBuffer<NetCompositionObject>(entities[i]);
                    for (int j = 0; j < buffer.Length; j++)
                    {
                        NetCompositionObject item = buffer[j];
                        if (!IsTrafficLightPrefab(item.m_Prefab, 0))
                            continue;

                        item.m_Position.y = -item.m_Position.y;
                        item.m_CurveOffsetRange = MirrorCurveRange(item.m_CurveOffsetRange);
                        item.m_Rotation = RotateHalfTurn(item.m_Rotation);
                        buffer[j] = item;
                        result.CompositionObjects++;
                    }
                }
            }

            using (NativeArray<Entity> entities = m_PrefabSubObjectQuery.ToEntityArray(Allocator.Temp))
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    if (!EntityManager.HasBuffer<PrefabSubObject>(entities[i]))
                        continue;

                    DynamicBuffer<PrefabSubObject> buffer = EntityManager.GetBuffer<PrefabSubObject>(entities[i]);
                    for (int j = 0; j < buffer.Length; j++)
                    {
                        PrefabSubObject item = buffer[j];
                        if (!IsTrafficLightPrefab(item.m_Prefab, 0))
                            continue;

                        item.m_Position.z = -item.m_Position.z;
                        item.m_Rotation = RotateHalfTurn(item.m_Rotation);
                        buffer[j] = item;
                        result.PrefabSubObjects++;
                    }
                }
            }

            return result;
        }

        private bool IsTrafficLightPrefab(Entity prefab, int depth)
        {
            if (prefab == Entity.Null || !EntityManager.Exists(prefab))
                return false;

            if (m_TrafficLightPrefabCache.TryGetValue(prefab, out bool cached))
                return cached;

            if (EntityManager.HasComponent<TrafficLightData>(prefab))
            {
                m_TrafficLightPrefabCache[prefab] = true;
                return true;
            }

            if (depth < 4 && EntityManager.HasBuffer<PlaceholderObjectElement>(prefab))
            {
                DynamicBuffer<PlaceholderObjectElement> placeholders = EntityManager.GetBuffer<PlaceholderObjectElement>(prefab, true);
                for (int i = 0; i < placeholders.Length; i++)
                {
                    if (IsTrafficLightPrefab(placeholders[i].m_Object, depth + 1))
                    {
                        m_TrafficLightPrefabCache[prefab] = true;
                        return true;
                    }
                }
            }

            m_TrafficLightPrefabCache[prefab] = false;
            return false;
        }

        private int RefreshLiveNetOwners()
        {
            int refreshed = 0;
            using (NativeArray<Entity> entities = m_LiveNetOwnerQuery.ToEntityArray(Allocator.Temp))
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    Entity entity = entities[i];
                    if (!EntityManager.Exists(entity) || EntityManager.HasComponent<Updated>(entity))
                        continue;

                    EntityManager.AddComponent<Updated>(entity);
                    refreshed++;
                }
            }

            return refreshed;
        }

        private static float2 MirrorCurveRange(float2 range)
        {
            return new float2(1f - range.y, 1f - range.x);
        }

        private static quaternion RotateHalfTurn(quaternion rotation)
        {
            return math.mul(quaternion.RotateY(kHalfTurnRadians), rotation);
        }

        private struct PatchResult
        {
            public int NetPieceObjects;
            public int CompositionObjects;
            public int PrefabSubObjects;

            public int TotalPatched => NetPieceObjects + CompositionObjects + PrefabSubObjects;
        }
    }
}
