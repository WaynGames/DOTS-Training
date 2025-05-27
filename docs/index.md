# Part 1: Health Regeneration in Unity DOTS (ECS Training)

## Concepts Introduced

* **IComponentData (ECS Components):** ECS components are plain data containers (structs) that implement `IComponentData`. They hold only data, no behavior, and are stored in optimized memory chunks by the ECS. In Unity’s ECS, this is analogous to MonoBehaviour fields but without any methods. In Part 1, we define components for Health and HealthRegeneration as `IComponentData` structs to represent an entity’s health state and regeneration rate.

* **Systems:** Systems encapsulate the logic that operates on entities’ components every frame. Part 1 introduces a `HealthRegenerationSystem` that runs each frame to increment the health values of entities. Unity ECS systems can be written by inheriting `SystemBase` or implementing `ISystem`. In this project, the system is a struct implementing `ISystem` (with `OnUpdate()`), which means it can be Burst-compiled for performance. The system uses queries to efficiently iterate over all entities with the required components and update their data.

* **Authoring & Baking Workflow:** Rather than adding ECS components directly in the editor, we use an Authoring GameObject with a MonoBehaviour to provide initial values, and a Baker to convert that into an entity at build/runtime. The authoring MonoBehaviour (here named `DamageableAuthoring`) exposes fields like Max Health and Regen Rate in the Unity Editor. During baking (via a SubScene or conversion workflow), the Baker reads those fields and adds the corresponding `IComponentData` to a new entity. A single authoring MonoBehaviour can add multiple components to an entity, which is exactly what we do for adding both Health and HealthRegen data in one go.

* **TransformUsageFlags:** When baking, we specify how the GameObject’s Transform should be converted. Unity’s `GetEntity` method in a Baker takes a `TransformUsageFlags` parameter to determine which transform components (like `LocalToWorld`, `LocalTransform`, `Parent`) to include on the entity. In Part 1, the Baker uses `TransformUsageFlags.Dynamic` to indicate the entity will move at runtime, ensuring it gets a `LocalTransform` and `LocalToWorld` component.

* **DOTS Editor Windows:** Unity provides specialized editor windows—Entities Hierarchy and Entities Inspector—to observe the converted entities and their components at runtime. You can verify that your `DamageableAuthoring` GameObjects have been converted into entities with the expected Health and HealthRegen components, and that the system is updating them.

## Code Walkthrough

### Project Structure & Files

* **Health (Component Data):** `Health.cs` defines a struct `Health : IComponentData` holding the entity’s health information. It contains at least two fields: `float Current` and `float Max`, representing the current and maximum health. The authoring sets both to the same starting value so entities begin at full health.

* **HealthRegen (Component Data):** `HealthRegen.cs` defines a struct `HealthRegen : IComponentData` that holds the regeneration rate (`float PointPerSec`). By separating this into its own component, only entities with a `HealthRegen` component will gradually heal.

* **DamageableAuthoring (MonoBehaviour + Baker):** A MonoBehaviour you attach to your unit in the Scene or prefab, exposing public fields for `MaxHealth` and `HealthRegenPerSec`. A nested `Baker` class (inheriting `Baker<DamageableAuthoring>`) overrides `Bake()`, calls `GetEntity(TransformUsageFlags.Dynamic)`, and adds both `Health` and `HealthRegen` components to the entity.

### HealthRegenSystem (ECS System)

Located in the `Systems` folder, `HealthRegenSystem.cs` contains the logic that performs health regeneration. It is marked with `[BurstCompile]` and implements `ISystem`. In its `OnUpdate(ref SystemState state)` method, it queries for all entities with both `Health` and `HealthRegen` components:

```csharp
foreach (var (hpRW, hpRegenRO) in SystemAPI.Query<RefRW<Health>, RefRO<HealthRegen>>())
{
    float newHealth = hpRW.ValueRO.Current + hpRegenRO.ValueRO.PointPerSec * SystemAPI.Time.DeltaTime;
    hpRW.ValueRW.Current = math.min(newHealth, hpRW.ValueRO.Max);
}
```

This loop uses `RefRW<Health>` for read-write access and `RefRO<HealthRegen>` for read-only access, then clamps the new health so it never exceeds the maximum.

> **Note:** A `Debug.DrawLine` call (`Vector3.zero` to `Vector3.one*3`) may appear in the code for visual debugging, but it is not essential to the core ECS logic.

## Summary of Learning Objectives

By the end of Part 1, you should be able to:

1. **Define ECS Components:** Create data-only structs implementing `IComponentData` to represent game state (e.g., Health, HealthRegen).
2. **Author GameObjects to Entities:** Use MonoBehaviour authoring scripts with public fields and Bakers to convert GameObjects into entities with corresponding components.
3. **Implement Systems for Game Logic:** Write an ECS System that runs each frame to update component data (health regeneration) using `SystemAPI.Query`, `RefRW`, and `RefRO`.
4. **Debug and View ECS Worlds:** Use DOTS Editor Windows (Entities Hierarchy, Entities Inspector) to observe entity state and verify system execution.

## Sources

* Unity Entities Package: [Component Data](https://docs.unity3d.com/Packages/com.unity.entities@0.8/manual/component_data.html)
* Unity Entities Package: [Transform Usage Flags](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/transforms-usage-flags.html)
* WaynGames/DOTS-Training Repository (Part 1 Commit): [https://github.com/WaynGames/DOTS-Training/commit/7046630bdcdd4388accb944a37b7c579a8dd7883](https://github.com/WaynGames/DOTS-Training/commit/7046630bdcdd4388accb944a37b7c579a8dd7883)
