# Part 1: Health Regeneration in Unity DOTS (ECS Training)

## Concepts Introduced

* **IComponentData (ECS Components):** ECS components are plain data containers (structs) that implement [`IComponentData`](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/components-unmanaged.html). They hold only data (no behavior) and are stored in optimized memory chunks by the ECS. In Unity’s ECS, this is analogous to public fields on MonoBehaviours, but without any methods. In Part 1, we define components for Health and HealthRegeneration as `IComponentData` structs to represent an entity’s health state and regeneration rate.

* **Systems:** Systems encapsulate the logic that operates on entity components each frame. Part 1 introduces a `HealthRegenSystem` that runs every frame to increment health values of entities. Unity ECS systems can be written by inheriting `SystemBase` (class) or implementing [`ISystem`](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/systems-isystem.html) (struct). In this project, the system is a `partial struct` implementing `ISystem` with an `OnUpdate()` function – this allows it to be Burst-compiled for performance (using the Unity [Burst compiler](https://docs.unity3d.com/Packages/com.unity.burst@1.8/manual/index.html)). The system uses `SystemAPI.Query` to efficiently iterate over all entities with the required components and update their data each frame (more on `SystemAPI` below).

* **Authoring & Baking Workflow:** Rather than adding ECS components in the editor manually, we use an **Authoring** GameObject with a MonoBehaviour to provide initial values, and a **Baker** to convert that authoring data into an entity at build/runtime. The authoring MonoBehaviour (here `DamageableAuthoring`) exposes fields like Max Health and Regen Rate in the Unity Editor. During **baking** (the conversion process, e.g. via a SubScene), the Baker (a class inheriting `Baker<T>`) reads those fields and adds the corresponding ECS components to a new entity. A single authoring MonoBehaviour can add multiple components to one entity – in Part 1, our baker adds both a Health component and a HealthRegen component in one go.

* **TransformUsageFlags:** When baking, we specify how the GameObject’s transform should be handled on the entity. Unity’s `Baker.GetEntity` method requires a [`TransformUsageFlags`](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/transforms-usage-flags.html) parameter to determine which transform components (like `LocalToWorld`, `LocalTransform`, `Parent`) to include on the entity. In Part 1, the Baker uses `TransformUsageFlags.Dynamic` (intending that the entity will move at runtime, so it should get a `LocalTransform` and `LocalToWorld`). **Note:** In the code below, we actually pass `None` for simplicity, but because other bakers (e.g. physics or rendering) may add transform data, the entity still ends up with the necessary transform components. In practice, you would use `Dynamic` if you need to explicitly ensure the entity has moveable transform data.

* **DOTS Editor Windows:** Unity provides specialized Editor windows—**Entities Hierarchy** and **Entities Inspector**—to observe the converted entities and their components at runtime. After baking, you can run the game and use these windows to verify that your `DamageableAuthoring` GameObjects have been converted into entities with the expected `Health` and `HealthRegen` components, and that the `HealthRegenSystem` is correctly updating their health values over time.

## Code Walkthrough

### Project Structure & Files

* **Health (Component Data):** `Health.cs` defines a struct `Health : IComponentData` holding the entity’s health information. It contains two fields: `float Current` and `float Max`, representing the current and maximum health. In the authoring process we set both to the same starting value so entities begin at full health.

* **HealthRegen (Component Data):** `HealthRegen.cs` defines a struct `HealthRegen : IComponentData` that holds the regeneration rate (`float PointPerSec`). By separating this into its own component, only entities that have a `HealthRegen` component will gradually heal over time.

* **DamageableAuthoring (MonoBehaviour + Baker):** A MonoBehaviour you attach to a GameObject (e.g. an enemy or unit) in the Scene or as a prefab, with public fields for `MaxHealth` and `HealthRegenPerSec`. It has an inner `Baker` class (inheriting `Baker<DamageableAuthoring>`) that overrides `Bake()`. In `Bake()`, we call `GetEntity(TransformUsageFlags.Dynamic)` (to get or create the ECS entity for this GameObject) and then add both a `Health` and `HealthRegen` component to that entity, using the values from the authoring component. This conversion happens during the build or when entering Play mode (if using SubScenes).

* **HealthRegenSystem (ECS System):** Located in the `Systems` folder, `HealthRegenSystem.cs` contains the logic that performs health regeneration each frame. It is marked with `[BurstCompile]` and implemented as an `ISystem`. In its `OnUpdate(ref SystemState state)` method, it uses `SystemAPI.Query` to find all entities that have both `Health` and `HealthRegen` components, then increases their current health based on the regen rate and delta time, clamping the value so it does not exceed the max health.

Below we examine each of these scripts with code and inline commentary:

#### [`Health.cs`](https://github.com/WaynGames/DOTS-Training/blob/main/1-HealthRegen/Components/Health.cs) (Component Data)

```csharp
--8<-- "1-HealthRegen/Components/Health.cs:3:12"
```


The `Health` component is a simple struct with two fields. Marking it as `IComponentData` means the ECS will store this data efficiently and allow systems to query and modify it. When an entity has this component, it represents that the entity has a health stat with a current value and a maximum value.

#### [`HealthRegen.cs`](https://github.com/WaynGames/DOTS-Training/blob/main/1-HealthRegen/Components/HealthRegen.cs) (Component Data)

```csharp
--8<-- "1-HealthRegen/Components/HealthRegen.cs:3:6"
```


The `HealthRegen` component holds the regeneration rate. By giving an entity a `HealthRegen` component (in addition to `Health`), we mark that entity as one that should regenerate health over time. `PointPerSec` defines how many health points are regained each second. Entities without `HealthRegen` will not regenerate (for example, you might not give this component to objects that do not auto-heal).

#### [`DamageableAuthoring.cs`](https://github.com/WaynGames/DOTS-Training/blob/main/1-HealthRegen/Authoring/DamageableAuthoring.cs) (Authoring MonoBehaviour + Baker)

```csharp
--8<-- "1-HealthRegen/Authoring/DamageableAuthoring.cs:4:54"
```


The `DamageableAuthoring` MonoBehaviour acts as the bridge between Unity’s GameObject world and the ECS world. You add this script to a GameObject (for example, an enemy character prefab) and set the **Max Health** and **Regen Rate** in the Inspector. When Unity “bakes” the GameObject to an Entity (such as when entering Play Mode with a SubScene workflow), the `Baker` nested class runs.

In the `Bake()` method above:

* We call `GetEntity(TransformUsageFlags.None)` to obtain the Entity representation for this GameObject. (If this GameObject had no Entity yet, it creates one. The `TransformUsageFlags.None` here indicates we are not explicitly adding any transform data via this baker – as noted earlier, other built-in bakers will add the necessary transform components if the object has renderers, colliders, etc. If we wanted to explicitly ensure a dynamic transform, we could use `TransformUsageFlags.Dynamic`.)

* We construct a `Health` struct and initialize its fields from the authoring component (`MaxHealth`). Here we set both `Max` and `Current` to the same value (starting the entity at full health). We then call `AddComponent(entity, health)` to add this component to the entity.

* Similarly, we create a `HealthRegen` struct, set its `PointPerSec` using the authoring’s **HealthRegenPerSec** value, and add it to the entity with `AddComponent(entity, regen)`.

After baking, the GameObject is converted into an Entity that now has both a Health and HealthRegen component with the values from the authoring script. At runtime, these values will be manipulated by ECS systems.

#### [`HealthRegenSystem.cs`](https://github.com/WaynGames/DOTS-Training/blob/main/1-HealthRegen/Systems/HealthRegenSystem.cs) (ECS System)
```csharp
--8<-- "1-HealthRegen/Systems/HealthRegenSystem.cs:6:60"
```

This system runs every frame (its `OnUpdate` is called by the ECS scheduler). We annotate it with `[BurstCompile]` to have the Burst compiler optimize the code. Inside `OnUpdate`:

* We use `SystemAPI.Query<RefRW<Health>, RefRO<HealthRegen>>()` with an idiomatic C# `foreach` to loop through **every entity** that has both a `Health` component and a `HealthRegen` component. For each such entity, the query provides two things: a **read-write reference** to its Health (`RefRW<Health>` named `healthRW`) and a **read-only reference** to its HealthRegen (`RefRO<HealthRegen>` named `regenRO`). We destructure these in the loop as `(healthRW, regenRO)`.

* Using the data from those references, we calculate the new health value by adding `regenRO.ValueRO.PointPerSec * deltaTime` to the current health. Note that we access the current health with `healthRW.ValueRO.Current` (ValueRO gives us a *read-only* view of the component’s data, which is cheaper when we only need to read). We multiply the regen rate by `SystemAPI.Time.DeltaTime` (the time elapsed since the last frame) to scale the regeneration per frame.

* We then set the entity’s `Health.Current` to this new value, but first we clamp it using `math.min` to ensure it does not exceed `Health.Max`. We write to the component via `healthRW.ValueRW.Current` (ValueRW gives us a *read-write* access to modify the component’s data).

In summary, `HealthRegenSystem` finds all entities that should regenerate health and increases their `Health.Current` each frame. By using `RefRW`/`RefRO` wrappers provided by [`SystemAPI`](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/systems-systemapi.html), the code cleanly and safely updates component data. Only entities with both required components are touched, and the system does nothing if there are no such entities (thanks to the query filtering automatically).

> **Note:** The code includes a `Debug.DrawLine(Vector3.zero, Vector3.one*3, Color.blue, 5)` (not shown above) for visual debugging in the Scene. This call is not essential to the ECS logic; it just draws a temporary line in the Scene view for debugging purposes. It can be removed without affecting the health regen functionality.

## Summary of Learning Objectives

By the end of **Part 1: Health Regeneration**, you should be able to:

1. **Define ECS Components:** Create data-only structs implementing `IComponentData` to represent game state (e.g. `Health`, `HealthRegen` components for an entity’s health and regen rate).

2. **Author GameObjects to Entities:** Use MonoBehaviour authoring scripts with public fields, along with `Baker<T>` classes, to convert GameObjects into entities with corresponding components during the baking process.

3. **Implement Systems for Game Logic:** Write an ECS System that runs each frame to update component data (like regenerating health) using `SystemAPI.Query` to efficiently iterate over entities, and use `RefRW`/`RefRO` access wrappers to read and write component values safely.

4. **Debug and Inspect ECS Data:** Utilize the DOTS Editor windows (Entities Hierarchy and Entities Inspector) to observe entity states and verify that your systems are working. You can see the `Health` and `HealthRegen` components on entities in real time and confirm that `Health.Current` increases each frame as expected.

## Preview of Part 2: Path Following

In **Part 2: Path Following**, we build on these fundamentals to move entities along a path. Key new concepts introduced will include:

* **Dynamic Buffers and IBufferElementData:** You'll learn to create buffer components (implementing [`IBufferElementData`](https://docs.unity3d.com/Packages/com.unity.entities@1.0/api/Unity.Entities.IBufferElementData.html)) that allow an entity to store a list of values. For example, we use a dynamic buffer of waypoints (positions) that an entity will navigate through. We’ll discuss setting an appropriate `InternalBufferCapacity` and how buffer data is stored in chunks.

* **Path Following Logic with Transforms:** Using Unity’s new transform components (`LocalTransform`) and helper functions, Part 2 will show how to move an entity toward successive waypoints. The system will update an entity’s `LocalTransform.Position` each frame, and use utility methods (like a `LookAt` rotation helper) to orient the entity along its direction of travel. This demonstrates how to manipulate spatial data in ECS (replacing what would traditionally be done with `Transform` in GameObject code).

* **Performance and Profiling:** We continue to leverage `[BurstCompile]` on our systems for optimized performance. You’ll see how enabling Burst and writing data-oriented code can yield high-performance results, and we’ll briefly look at using the Unity **Profiler** to measure the difference. This part emphasizes the performance benefits of DOTS, especially as we start handling more entities or more complex logic.

* **Advanced Baking Techniques:** Part 2’s authoring script will introduce the concept of declaring baking dependencies (using `DependsOn`) so that if a referenced asset (like a ScriptableObject defining speed) changes, the entity will automatically re-bake. We’ll also cover the idea of “baking-only” data – for instance, creating an entity or component that exists only during baking to facilitate conversion, but is not present at runtime.

* **Cleaner ECS Code Patterns:** As a bonus, Part 2 provides some tips for writing cleaner ECS code. This includes techniques to avoid repetitive `ValueRO/ValueRW` calls (using local `ref` variables for components, as you might have noticed in our PathFollow system code), and structuring your systems and data for clarity.

By the end of Part 2, you will have an entity (or multiple entities) following a series of waypoints in your game, and you will have deeper insight into using dynamic buffers and transform systems in Unity ECS. You’ll also become more comfortable with the DOTS workflow, from authoring and baking through to debugging and performance tuning. Part 2 builds a solid foundation for even more advanced ECS scenarios in subsequent modules. Enjoy your continued journey into Unity’s Data-Oriented Tech Stack!
