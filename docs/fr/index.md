# Partie 1 : Régénération de la santé dans Unity DOTS (Formation ECS)

## Concepts introduits

* **IComponentData (Composants ECS) :** Les composants ECS sont de simples conteneurs de données (structs) implémentant [`IComponentData`](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/components-unmanaged.html). Ils ne contiennent que des données (pas de comportement) et sont stockés dans des blocs mémoire optimisés par l’ECS. Dans l’ECS de Unity, cela équivaut aux champs publics des MonoBehaviours, mais sans méthodes. Dans la Partie 1, nous définissons les composants **Health** et **HealthRegen** comme des structs `IComponentData` pour représenter l’état de santé et le taux de régénération d’une entité.

* **Systèmes :** Les systèmes encapsulent la logique qui opère sur les composants des entités à chaque frame. La Partie 1 introduit un `HealthRegenSystem` qui s’exécute à chaque frame pour incrémenter la valeur de santé des entités. Les systèmes ECS de Unity peuvent être écrits en héritant de `SystemBase` (classe) ou en implémentant [`ISystem`](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/systems-isystem.html) (struct). Dans ce projet, le système est un `partial struct` implémentant `ISystem` avec une méthode `OnUpdate()` – ce qui lui permet d’être compilé avec Burst pour des performances optimales (via le compilateur [Burst](https://docs.unity3d.com/Packages/com.unity.burst@1.8/manual/index.html)). Le système utilise `SystemAPI.Query` pour itérer efficacement sur toutes les entités possédant les composants requis et mettre à jour leurs données à chaque frame (voir plus bas sur `SystemAPI`).

* **Workflow Authoring & Baking :** Plutôt que d’ajouter manuellement les composants ECS dans l’éditeur, nous utilisons un **Authoring** GameObject avec un MonoBehaviour pour fournir les valeurs initiales, et un **Baker** pour convertir ces données en entité au moment de la compilation ou à l’exécution. Le MonoBehaviour d’authoring (`DamageableAuthoring`) expose dans l’inspecteur des champs comme **Max Health** et **Regen Rate**. Lors du **baking** (processus de conversion, par exemple via une SubScene), le Baker (classe héritant de `Baker<T>`) lit ces champs et ajoute les composants ECS correspondants à une nouvelle entité. Un seul MonoBehaviour d’authoring peut ajouter plusieurs composants ; dans la Partie 1, notre Baker ajoute à la fois un composant **Health** et un composant **HealthRegen**.

* **TransformUsageFlags :** Lors du baking, on précise comment le transform du GameObject doit être traité sur l’entité. La méthode `Baker.GetEntity` de Unity exige un paramètre [`TransformUsageFlags`](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/transforms-usage-flags.html) pour déterminer quels composants de transform (comme `LocalToWorld`, `LocalTransform`, `Parent`) inclure. Dans la Partie 1, le Baker utilise `TransformUsageFlags.Dynamic` (prévu pour des entités mobiles à l’exécution, afin d’ajouter `LocalTransform` et `LocalToWorld`). **Note :** Dans le code ci-dessous, nous passons en fait `None` par simplicité, mais d’autres bakers (physique, rendu, etc.) ajoutent finalement les données de transform nécessaires. En pratique, on utiliserait `Dynamic` pour s’assurer explicitement que l’entité dispose d’un transform déplaçable.

* **Fenêtres d’éditeur DOTS :** Unity propose des fenêtres d’éditeur spécialisées – **Entities Hierarchy** et **Entities Inspector** – pour observer les entités converties et leurs composants à l’exécution. Après le baking, vous pouvez lancer le jeu et utiliser ces fenêtres pour vérifier que vos GameObjects avec `DamageableAuthoring` sont bien convertis en entités avec les composants `Health` et `HealthRegen`, et que `HealthRegenSystem` met correctement à jour leur santé au fil du temps.

## Parcours du code

### Structure du projet & fichiers

* **Health (données de composant) :** `Health.cs` définit un struct `Health : IComponentData` contenant deux champs : `float Current` et `float Max`, représentant respectivement la santé actuelle et la santé maximale de l’entité. Lors de l’authoring, on initialise les deux au même point de départ pour commencer avec une santé pleine.

* **HealthRegen (données de composant) :** `HealthRegen.cs` définit un struct `HealthRegen : IComponentData` contenant le taux de régénération (`float PointPerSec`). En séparant cela dans son propre composant, seules les entités dotées de `HealthRegen` guériront progressivement.

* **DamageableAuthoring (MonoBehaviour + Baker) :** Un MonoBehaviour que vous attachez à un GameObject (ennemi ou unité) comme prefab, avec des champs publics pour `MaxHealth` et `HealthRegenPerSec`. Il contient une classe imbriquée `Baker` (héritant `Baker<DamageableAuthoring>`) qui surcharge `Bake()`. Dans `Bake()`, on appelle `GetEntity(TransformUsageFlags.Dynamic)` pour obtenir ou créer l’entité ECS du GameObject, puis on ajoute à cette entité les composants `Health` et `HealthRegen` avec les valeurs de l’authoring. Cette conversion a lieu lors de la compilation ou à l’entrée en mode Play (via SubScenes).

* **HealthRegenSystem (système ECS) :** Dans le dossier `Systems`, `HealthRegenSystem.cs` contient la logique de régénération à chaque frame. Il est annoté `[BurstCompile]` et implémenté comme un `ISystem`. Dans sa méthode `OnUpdate(ref SystemState state)`, il utilise `SystemAPI.Query` pour sélectionner toutes les entités ayant à la fois `Health` et `HealthRegen`, puis augmente la valeur `Current` en fonction du taux `PointPerSec` et du deltaTime, en veillant à ne pas dépasser `Max`.

Ci-dessous, focus sur chaque script avec extraits et explications :

#### [`Health.cs`](https://github.com/WaynGames/DOTS-Training/blob/main/1-HealthRegen/Components/Health.cs) (Component Data)

```csharp
--8<-- "1-HealthRegen/Components/Health.cs:3:12"
```

Le composant `Health` est un struct simple à deux champs. Le fait d’implémenter `IComponentData` permet à l’ECS de stocker et de modifier ces données de façon très performante. Lorsqu’une entité a ce composant, elle possède un état de santé défini par `Current` et `Max`.

#### [`HealthRegen.cs`](https://github.com/WaynGames/DOTS-Training/blob/main/1-HealthRegen/Components/HealthRegen.cs) (Component Data)

```csharp
--8<-- "1-HealthRegen/Components/HealthRegen.cs:3:6"
```

Le composant `HealthRegen` contient le taux de régénération. On le donne uniquement aux entités qui doivent guérir automatiquement. Le champ `PointPerSec` indique le nombre de points de vie récupérés par seconde.

#### [`DamageableAuthoring.cs`](https://github.com/WaynGames/DOTS-Training/blob/main/1-HealthRegen/Authoring/DamageableAuthoring.cs) (Authoring MonoBehaviour + Baker)

```csharp
--8<-- "1-HealthRegen/Authoring/DamageableAuthoring.cs:4:22"
[...]  
--8<-- "1-HealthRegen/Authoring/DamageableAuthoring.cs:52:54"
```

Le MonoBehaviour `DamageableAuthoring` sert de passerelle entre le monde GameObject et le monde ECS. Vous ajoutez ce script à un GameObject et fixez **Max Health** et **HealthRegenPerSec** dans l’Inspector. Lors du baking, la classe `Baker` interne s’exécute :

```csharp
--8<-- "1-HealthRegen/Authoring/DamageableAuthoring.cs:21:52"
```

Dans `Bake()` :

1. Appel de `GetEntity(TransformUsageFlags.None)` pour obtenir l’entité ECS correspondant au GameObject (création si absente).
2. Construction et ajout du composant `Health` : initialisation de `Max` et `Current` à `MaxHealth`.
3. Construction et ajout du composant `HealthRegen` : initialisation de `PointPerSec` à `HealthRegenPerSec`.

Après le baking, le GameObject est converti en entité avec les composants **Health** et **HealthRegen** prêts à l’exécution.

#### `HealthRegenSystem.cs` (système ECS)

```csharp
--8<-- "1-HealthRegen/Systems/HealthRegenSystem.cs:6:60"
```

Ce système s’exécute chaque frame. Annoté `[BurstCompile]`, il optimise la boucle de mise à jour. Dans `OnUpdate` :

1. `SystemAPI.Query<RefRW<Health>, RefRO<HealthRegen>>()` récupère toutes les entités avec `Health` (lecture/écriture) et `HealthRegen` (lecture seule).
2. Pour chaque entité, on calcule la nouvelle santé :

   ```csharp
   newHealth = healthRW.ValueRO.Current + regenRO.ValueRO.PointPerSec * SystemAPI.Time.DeltaTime;
   healthRW.ValueRW.Current = math.min(newHealth, healthRW.ValueRO.Max);
   ```
3. On clamp la valeur pour ne pas dépasser `Max`.

> **Note :** Le code inclut un appel `Debug.DrawLine(Vector3.zero, Vector3.one*3, Color.blue, 5)` pour du debug visuel. Il n’est pas essentiel à la régénération et peut être retiré sans impact.

## Objectifs d’apprentissage

À la fin de la **Partie 1 : Régénération de la santé**, vous saurez :

1. **Définir des composants ECS :** Créer des structs `IComponentData` pour représenter l’état du jeu (`Health`, `HealthRegen`).
2. **Authoring des GameObjects vers entités :** Utiliser des MonoBehaviour d’authoring et des `Baker<T>` pour convertir vos GameObjects en entités dotées des composants appropriés.
3. **Implémenter la logique de jeu via des systèmes :** Écrire un système ECS mis à jour chaque frame pour modifier les données de composants, en utilisant `SystemAPI.Query` et `RefRW`/`RefRO`.
4. **Déboguer et inspecter les données ECS :** Utiliser les fenêtres **Entities Hierarchy** et **Entities Inspector** pour observer les composants et vérifier l’évolution de `Health.Current` en temps réel.

## Aperçu de la Partie 2 : Suivi de chemin

Dans la **Partie 2 : Suivi de chemin**, nous étendrons ces bases pour déplacer des entités le long d’un tracé. Les nouveaux concepts incluront :

* **Dynamic Buffers et IBufferElementData :** Création de buffers dynamiques (structs `IBufferElementData`) pour stocker des listes de valeurs, par exemple une série de waypoints. Nous verrons comment définir une `InternalBufferCapacity` et comment ces buffers sont organisés en chunks.

* **Logique de suivi de chemin avec les Transforms :** Utilisation des composants de transform ECS (`LocalTransform`) et d’helpers pour déplacer et orienter l’entité vers chaque waypoint. Cela remplace l’usage de `Transform` des GameObjects par du code data-oriented.

* **Performance et profiling :** Toujours avec `[BurstCompile]`, nous mesurerons les gains de performance et apprendrons à utiliser le Profiler Unity pour comparer différents scénarios (plus d’entités, logique plus complexe).

* **Techniques avancées de baking :** Déclaration de dépendances de baking (`DependsOn`) pour re-baker automatiquement si une ressource change, et création de données “uniquement présentes pendant le baking” (Baking-only).

* **Patterns de code ECS plus lisibles :** Astuces pour réduire la verbosité des `ValueRO`/`ValueRW` (utilisation de `ref` locaux) et structurer vos systèmes pour plus de clarté.

À la fin de la Partie 2, vous aurez des entités parcourant des waypoints dans votre scène, et vous maîtriserez les buffers dynamiques, les systèmes de transform et le workflow DOTS du début à la fin. Bon apprentissage dans l’univers data-oriented de Unity !
