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
        private readonly Dictionary<string, Stack<CreatureView>> pools = new Dictionary<string, Stack<CreatureView>>();
        private readonly Dictionary<string, CreatureDefinition> definitions = new Dictionary<string, CreatureDefinition>();

        private PlanetSimulation simulation;
        private MaterialLibrary materials;
        private TerrainShape terrain;

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
            view.Leave(gone =>
            {
                gone.gameObject.SetActive(false);
                Pool(gone.Definition.id).Push(gone);
            });
        }

        private void Spawn(CreatureInstance creature, bool animate)
        {
            if (!definitions.TryGetValue(creature.Spec.Id, out var definition)) return;

            var pool = Pool(definition.id);
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
        }

        private Stack<CreatureView> Pool(string id)
        {
            if (!pools.TryGetValue(id, out var pool))
            {
                pool = new Stack<CreatureView>();
                pools[id] = pool;
            }
            return pool;
        }
    }
}
