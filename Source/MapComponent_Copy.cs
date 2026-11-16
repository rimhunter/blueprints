// MapComponent_Copy.cs
// Copyright Karel Kroeze, 2020-2020

using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Blueprints {
    public class MapComponent_Copy: MapComponent {
        public MapComponent_Copy(Map map) : base(map) {
        }

        public static bool Valid =>
            Find.UIRoot is UIRoot_Play &&
            Find.Selector.SelectedObjects.OfType<Thing>().Count(b => b.IsValidBlueprintThing()) > 2;

        const EventModifiers Chord = EventModifiers.Control | EventModifiers.Shift | EventModifiers.Alt;

        public static bool CopyModifiersHeld(Event ev) => (ev.modifiers & Chord) == EventModifiers.Control;

        public override void MapComponentUpdate() {
            BlueprintController.ShowPendingNotices();
        }

        public override void MapComponentOnGUI() {
            if (CopyModifiersHeld(Event.current) && KeyBindingDefOf2.Blueprint_Copy.KeyDownEvent && Valid) {
                Event.current.Use();
                Blueprint.Create(Find.Selector.SelectedObjects.OfType<Thing>().Where(b => b.IsValidBlueprintThing()),
                                  true);
            }
        }
    }
}
