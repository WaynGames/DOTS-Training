﻿using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

[UpdateAfter(typeof(TransformSystemGroup))]
public partial struct PresentationGOSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
    }

    public void OnDestroy(ref SystemState state)
    {
    }

    public void OnUpdate(ref SystemState state)
    {
        var ecbBOS = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        foreach(var (pgo,transform,entity) in SystemAPI.Query<PresentationGO,RefRO<LocalTransform>>().WithEntityAccess())
        {
            GameObject go = GameObject.Instantiate(pgo.Prefab);
            go.AddComponent<EntityGameObject>().AssignEntity(entity, state.World);
            go.transform.position = transform.ValueRO.Position;
            ecbBOS.AddComponent(entity, new TransformGO() { Transform = go.transform });
            ecbBOS.AddComponent(entity, new AnimatorGO() { Animator = go.GetComponent<Animator>() });

            ecbBOS.RemoveComponent<PresentationGO>(entity);
        }
        var ecbEOS = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var (goTransform,goAnimator, transform,speed) in SystemAPI.Query<TransformGO, AnimatorGO, RefRO<LocalTransform>, RefRO<Speed>>())
        {
            goTransform.Transform.position =  transform.ValueRO.Position;
            goTransform.Transform.rotation =  transform.ValueRO.Rotation;
            goAnimator.Animator.SetFloat("speed", speed.ValueRO.value);
        }
        foreach(var (goTransform,entity) in SystemAPI.Query<TransformGO>().WithNone<LocalTransform>().WithEntityAccess())
        {
            if (goTransform.Transform != null)
            {
                GameObject.Destroy(goTransform.Transform.gameObject);
            }
            ecbEOS.RemoveComponent<TransformGO>(entity);
        }
    }
}