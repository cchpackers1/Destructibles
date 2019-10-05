using thelebaron.Damage;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace Destructibles
{
    public struct RemoveNeighborEvent : IComponentData
    {
        public Entity Target;
        public int Index;
    }

    public class StrainSystem : JobComponentSystem
    {
        private EntityQuery m_ChainsQuery;

        //[BurstCompile]
        //[RequireComponentTag(typeof(PhysicsVelocity))]
        private struct RemoveNodeNeighbor : IJobForEachWithEntity_EB<NodeNeighbor>
        {
            public EntityCommandBuffer.Concurrent EntityCommandBuffer;
            [ReadOnly] public ComponentDataFromEntity<PhysicsVelocity> Velocity;

            public void Execute(Entity entity, int index, DynamicBuffer<NodeNeighbor> neighbors)
            {
                if (neighbors.Length <= 0)
                {
                    EntityCommandBuffer.AddComponent<PhysicsVelocity>(index, entity);
                    EntityCommandBuffer.RemoveComponent<NodeNeighbor>(index, entity);
                    return;
                }

                for(var i = neighbors.Length - 1; i > -1; i--)
                {
                    if(Velocity.Exists(neighbors[i].Node))//neighbors[i].Node.Equals(Entity.Null) && 
                    {
                        neighbors.RemoveAt(i);
                    }
                }
                

            }
        }

        


        private struct ChunkChainJob : IJobChunk
        {
            public EntityCommandBuffer.Concurrent EntityCommandBuffer;
            [ReadOnly] public ArchetypeChunkEntityType EntityType;
            public ArchetypeChunkComponentType<Node> NodeType;

            public ArchetypeChunkBufferType<Chain> ChainType;
            //public BufferAccessor<Chain> ChainBufferAccessor;

            //public            EntityCommandBuffer.Concurrent                    ECB;

            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                var chunkEntity = chunk.GetNativeArray(EntityType);
                var chunkNode = chunk.GetNativeArray(NodeType);
                var chainBufferAccessor = chunk.GetBufferAccessor(ChainType);
                for (int entityIndex = 0; entityIndex < chunkEntity.Length; entityIndex++)
                {
                    // Does entity exist in other chains? if not, add velocity to it.
                    var exists = false;
                    var node = chunkNode[entityIndex];
                    var entity = chunkEntity[entityIndex];


                    for (int i = 0; i < chainBufferAccessor.Length; i++)
                    {
                        var b = chainBufferAccessor[i];
                        for (int j = 0; j < b.Length; j++)
                        {
                            if (node.Value.Equals(b[j].Node))
                                exists = true;
                        }
                    }

                    if (!exists)
                    {
                        EntityCommandBuffer.SetComponent(chunkIndex, node.Value, new Health
                        {
                            Max = 10,
                            Value = 0
                        });
                    }
                }
            }
        }

        private struct DestroyChain : IJobForEachWithEntity_EBC<Chain, Node>
        {
            public EntityCommandBuffer.Concurrent EntityCommandBuffer;
            [ReadOnly] public ComponentDataFromEntity<PhysicsVelocity> Velocity;

            public void Execute(Entity entity, int index, DynamicBuffer<Chain> buffer, ref Node node)
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    if (Velocity.Exists(buffer[i].Node))
                    {
                        EntityCommandBuffer.DestroyEntity(index, entity);
                    }
                }
            }
        }

        //[RequireComponentTag(typeof(Anchored))]
        [ExcludeComponent(typeof(PhysicsVelocity))]
        private struct CheckHealth : IJobForEachWithEntity<Health, NodeBreakable>
        {
            public EntityCommandBuffer.Concurrent EntityCommandBuffer;

            public void Execute(Entity entity, int index, ref Health health, ref NodeBreakable nodeBreakable)
            {
                if (health.Value <= 0)
                {
                    EntityCommandBuffer.AddComponent(index, entity, new PhysicsVelocity());
                    EntityCommandBuffer.AddComponent(index, entity, new Unanchored());
                    //EntityCommandBuffer.RemoveComponent(index, entity, typeof(Anchored));
/*
                    var e = EntityCommandBuffer.CreateEntity(index);
                    
                    EntityCommandBuffer.AddComponent(index, e, new BreakEvent
                    {
                        NodeEntity = entity,
                        //GraphEntity = node.Graph //TODO replace with graph component that has graph entity on it!!!!!!!
                    });*/


                    //EntityCommandBuffer.RemoveComponent(index, entity, typeof(Connection));
                    //EntityCommandBuffer.RemoveComponent(index, entity, typeof(DynamicAnchor));
                }
            }
        }


        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var removeNeighborsJob = new RemoveNodeNeighbor
            {
                EntityCommandBuffer = m_EndSimulationEntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent(),
                Velocity = GetComponentDataFromEntity<PhysicsVelocity>(true)
            };
            var removeNeighborsHandle = removeNeighborsJob.Schedule(this, inputDeps);
            m_EndSimulationEntityCommandBufferSystem.AddJobHandleForProducer(removeNeighborsHandle);


            var checkHealthJob = new CheckHealth
                {EntityCommandBuffer = m_EndSimulationEntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent()};
            var checkHealthHandle = checkHealthJob.Schedule(this, removeNeighborsHandle);
            m_EndSimulationEntityCommandBufferSystem.AddJobHandleForProducer(checkHealthHandle);
            return checkHealthHandle;


            /*
            var addVelocityJob = new AddVelocity { EntityCommandBuffer = m_EndSimulationEntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent()};
            var addVelocityHandle = addVelocityJob.Schedule(this, checkHealthHandle);
            m_EndSimulationEntityCommandBufferSystem.AddJobHandleForProducer(addVelocityHandle);
            
            
            
            var destroyChain = new DestroyChain
                {
                    EntityCommandBuffer = m_EndSimulationEntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent(),
                    Velocity = GetComponentDataFromEntity<PhysicsVelocity>(true)
                };
            var destroyChainHandle = destroyChain.Schedule(this,addVelocityHandle);
            m_EndSimulationEntityCommandBufferSystem.AddJobHandleForProducer(destroyChainHandle);
            
            
            
            var entityType             = GetArchetypeChunkEntityType();
            var nodeType             = GetArchetypeChunkComponentType<Node>();
            var chainType             = GetArchetypeChunkBufferType<Chain>();
            
            var chunkJob = new ChunkChainJob
                {
                    EntityCommandBuffer =  m_EndSimulationEntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent(),
                    EntityType = entityType,
                    NodeType = nodeType,
                    ChainType = chainType,
                    //ChainBufferAccessor = m_ChainsQuery.to,
                    //ECB = 
                };
            
            var chunkJobHandle = chunkJob.Schedule(m_ChainsQuery, destroyChainHandle);
            m_EndSimulationEntityCommandBufferSystem.AddJobHandleForProducer(chunkJobHandle);
            

            return chunkJobHandle;*/
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            m_EndSimulationEntityCommandBufferSystem =
                World.Active.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            m_ChainsQuery = GetEntityQuery(typeof(Node), typeof(Chain));
        }

        private EndSimulationEntityCommandBufferSystem m_EndSimulationEntityCommandBufferSystem;
    }
}

/*        
        /// <summary>
        /// If a node has no immediate anchor, detach it. For now set health value so we can burst this job.
        /// </summary>
        [BurstCompile]
        [RequireComponentTag(typeof(Node), typeof(DynamicAnchor))]
        [ExcludeComponent(typeof(PhysicsVelocity),typeof(StaticAnchor))]
        struct CheckDynamicAnchors : IJobForEachWithEntity_EBC<Connection, Health>
        {
            public EntityCommandBuffer.Concurrent EntityCommandBuffer;
            [ReadOnly] public ComponentDataFromEntity<DynamicAnchor> DynamicAnchorData;
            
            public void Execute(Entity entity, int index, DynamicBuffer<Connection> connection, ref Health health)
            {
                bool removeAnchor = true;
                
                for (int i = 0; i < connection.Length; i++)
                {
                    if (DynamicAnchorData.Exists(connection[i].Node))
                    {
                        removeAnchor = false;
                    }
                }

                if (removeAnchor)
                    health.Value = 0; //EntityCommandBuffer.RemoveComponent(index, entity, typeof(DynamicAnchor));
            }
        }*/