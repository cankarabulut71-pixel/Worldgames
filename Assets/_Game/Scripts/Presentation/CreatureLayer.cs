using System.Collections.Generic;
using TinyPlanet.Data;
using TinyPlanet.Simulation;
using UnityEngine;

namespace TinyPlanet.Presentation
{
    /// <summary>Keeps one pooled CreatureView per living creature in sync with the simulation.</summary>
    public sealed class CreatureLayer : MonoBehaviour
    {
        private readonly Dictionary<int, CreatureView> active = new Dictionary<int, CreatureView>();
        private readonly Dictionary<CreatureDefinition, Stack<CreatureView>> pools = new Dictionary<CreatureDefinition, Stack<CreatureView>>();
        private readonly List<CreatureView> golden = new List<CreatureView>();
        private float glintTimer;
        private readonly Dictionary<string, CreatureDefinition> definitions = new Dictionary<string, CreatureDefinition>();

        private PlanetSimulation simulation;
        private MaterialLibrary materials;
        private TerrainShape terrain;

        /// <summary>Where golden creatures get their sparkle from.</summary>
        public Effects Effects { get; set; }

        public void Init(PlanetSimulation simulation, IEnumerable<CreatureDefinition> creatures, MaterialLibrary materials, TerrainShape terrain)
        {
            this.simulation = simulation;
            this.materials = materials;
            this.terrain = terrain;

            foreach (var definition in creatures)
            {
                if (definition != null) definitions[definition.id] = definition;
            }

            foreach (var creature in simulation.Creatures)
                Spawn(creature, animate: false);

            simulation.CreatureArrived += OnArrived;
            simulation.CreatureMoved += OnMoved;
            simulation.CreatureLeft += OnLeft;
        }

        private void OnDestroy()
        {
            if (simulation == null) return;
            simulation.CreatureArrived -= OnArrived;
            simulation.CreatureMoved -= OnMoved;
            simulation.CreatureLeft -= OnLeft;
        }

        /// <summary>
        /// The creature nearest to a screen point, if one is within <paramref name="maxPixels"/> and on the visible side of the planet.
        /// </summary>
        public bool TryPick(Camera camera, Vector2 screenPoint, float maxPixels, out CreatureView picked)
        {
            picked = null;
            float best = maxPixels * maxPixels;
            Vector3 cameraPosition = camera.transform.position;

            foreach (var view in active.Values)
            {
                Vector3 centre = view.BodyCentre;
                Vector3 normal = (centre - transform.position).normalized;
                if (Vector3.Dot(normal, cameraPosition - centre) <= 0f) continue; // around the back

                Vector3 screen = camera.WorldToScreenPoint(centre);
                if (screen.z <= 0f) continue;

                float distance = ((Vector2)screen - screenPoint).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    picked = view;
                }
            }
            return picked != null;
        }

        private void OnArrived(CreatureInstance creature) => Spawn(creature, animate: true);

        private void OnMoved(CreatureInstance creature)
        {
            if (active.TryGetValue(creature.Id, out var view)) view.SetHome(creature.HomeCell);
        }

        private void OnLeft(CreatureInstance creature)
        {
            if (!active.TryGetValue(creature.Id, out var view)) return;
            active.Remove(creature.Id);
            golden.Remove(view);
            view.Leave(gone =>
            {
                gone.gameObject.SetActive(false);
                Pool(gone.Definition).Push(gone);
            });
        }

        private void Spawn(CreatureInstance creature, bool animate)
        {
            if (!definitions.TryGetValue(creature.Spec.Id, out var definition)) return;
            if (creature.Golden) definition = GoldenLook.Of(definition);

            var pool = Pool(definition);
            CreatureView view;
            if (pool.Count > 0)
            {
                view = pool.Pop();
                view.gameObject.SetActive(true);
            }
            else
            {
                var go = new GameObject(definition.displayName);
                go.transform.SetParent(transform, false);
                view = go.AddComponent<CreatureView>();
                view.Build(definition, materials);
            }

            view.Place(creature, simulation.Planet, terrain, animate);
            active[creature.Id] = view;
            if (creature.Golden) golden.Add(view);
        }

        // Golden creatures glint now and then, so they catch the eye from across the planet.
        private void Update()
        {
            if (Effects == null || golden.Count == 0) return;
            glintTimer -= Time.deltaTime;
            if (glintTimer > 0f) return;
            glintTimer = 0.5f;
            foreach (var view in golden)
            {
                if (Random.value < 0.6f) Effects.Glint(view.BodyCentre, view.transform.up);
            }
        }

        private Stack<CreatureView> Pool(CreatureDefinition definition)
        {
            if (!pools.TryGetValue(definition, out var pool))
            {
                pool = new Stack<CreatureView>();
                pools[definition] = pool;
            }
            return pool;
        }
    }

    /// <summary>The rare golden colours of a species: a copy of its definition, dressed in gold.</summary>
    public static class GoldenLook
    {
        public static readonly Color Gold = new Color(1f, 0.78f, 0.22f);
        private static readonly Dictionary<CreatureDefinition, CreatureDefinition> Cache = new Dictionary<CreatureDefinition, CreatureDefinition>();

        public static CreatureDefinition Of(CreatureDefinition definition)
        {
            if (Cache.TryGetValue(definition, out var golden)) return golden;
            golden = Object.Instantiate(definition);
            golden.name = definition.name + " (Golden)";
            golden.hideFlags = HideFlags.DontSave;
            golden.displayName = string.Format(UI.Strings.GoldenNameFormat, definition.displayName);
            // Brighter than white, so the bloom makes it shine even at night.
            golden.primaryColor = Color.Lerp(definition.primaryColor, Gold, 0.9f) * 1.6f;
            golden.secondaryColor = Color.Lerp(definition.secondaryColor, new Color(1f, 0.95f, 0.70f), 0.8f) * 1.4f;
            Cache[definition] = golden;
            return golden;
        }
    }
}
