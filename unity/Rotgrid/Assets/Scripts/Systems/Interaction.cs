using System.Collections.Generic;
using UnityEngine;

namespace Rotgrid
{
    public enum ToastKind { Good, Warn, Bad }

    /// <summary>What the HUD should show when the player is looking at something.</summary>
    public struct Prompt
    {
        public bool valid;
        public string text;
        public string sub;
        public int cost;
        public bool hasCost;
        public bool blocked;
        public float hold;
        public bool hasHold;

        public static Prompt None { get { return new Prompt { valid = false }; } }

        public static Prompt Plain(string t, string s)
        {
            return new Prompt { valid = true, text = t, sub = s };
        }

        public static Prompt Blocked(string t, string s)
        {
            return new Prompt { valid = true, text = t, sub = s, blocked = true };
        }

        public static Prompt Buy(string t, int cost, string s)
        {
            return new Prompt { valid = true, text = t, cost = cost, hasCost = true, sub = s };
        }

        public static Prompt Hold(string t, string s, float progress)
        {
            return new Prompt { valid = true, text = t, sub = s, hold = progress, hasHold = true };
        }
    }

    /// <summary>Base for everything the player can walk up to and use.</summary>
    public abstract class Interactable
    {
        public readonly string id;
        public Vector3 pos;
        public float radius;
        public int priority;
        public bool enabled = true;

        protected Interactable(string id, Vector3 pos, float radius, int priority)
        {
            this.id = id;
            this.pos = pos;
            this.radius = radius;
            this.priority = priority;
        }

        public abstract Prompt GetPrompt(GameRun run);
        public abstract bool Activate(GameRun run);
    }

    /// <summary>
    /// Central registry. Systems push items in; the game loop asks for the best
    /// candidate every frame, biased toward what the player is looking at.
    /// </summary>
    public class InteractionSystem
    {
        public readonly List<Interactable> items = new List<Interactable>();

        public T Add<T>(T item) where T : Interactable
        {
            items.Add(item);
            return item;
        }

        public void Remove(Interactable item) { items.Remove(item); }
        public void Clear() { items.Clear(); }

        public Interactable FindBest(Vector3 playerPos, Vector3 forward)
        {
            Interactable best = null;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (!it.enabled) continue;
                float dx = it.pos.x - playerPos.x;
                float dz = it.pos.z - playerPos.z;
                float dy = it.pos.y - playerPos.y;
                if (Mathf.Abs(dy) > 3.2f) continue;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);
                if (dist > it.radius) continue;
                float len = Mathf.Max(0.001f, dist);
                float dot = (dx / len) * forward.x + (dz / len) * forward.z;
                if (dot < -0.35f && dist > 1.3f) continue;      // behind the player
                float score = dot * 2.2f - dist * 0.5f + it.priority;
                if (score > bestScore) { bestScore = score; best = it; }
            }
            return best;
        }

        public Interactable ById(string id)
        {
            foreach (var it in items) if (it.id == id) return it;
            return null;
        }
    }
}
