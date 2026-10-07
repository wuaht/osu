// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Input.Bindings;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Tests.Beatmaps;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Editing
{
    public partial class TestSceneEditorUndoRedoKeyBindings : EditorTestScene
    {
        protected override Ruleset CreateEditorRuleset() => new OsuRuleset();

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset) => new TestBeatmap(ruleset, false);

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        private static Key platformModifier => RuntimeInfo.OS == RuntimeInfo.Platform.macOS ? Key.LWin : Key.ControlLeft;

        /// <summary>
        /// Original key combinations of the undo bindings, keyed by binding ID, for restoring after a test modifies them.
        /// </summary>
        private readonly Dictionary<Guid, string> originalUndoBindings = new Dictionary<Guid, string>();

        private int initialHitObjectCount;

        [SetUpSteps]
        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("store initial object count", () => initialHitObjectCount = EditorBeatmap.HitObjects.Count);
            AddStep("add hitobject", () => EditorBeatmap.Add(new HitCircle { StartTime = 1000 }));
            AddAssert("hitobject added", () => EditorBeatmap.HitObjects.Count, () => Is.EqualTo(initialHitObjectCount + 1));
        }

        [TearDownSteps]
        public void TearDownSteps()
        {
            AddStep("restore undo bindings", restoreUndoBindings);
        }

        [Test]
        public void TestDefaultBindings()
        {
            AddStep("press undo", () => pressWithModifier(Key.Z));
            AddAssert("hitobject removed", () => EditorBeatmap.HitObjects.Count, () => Is.EqualTo(initialHitObjectCount));

            AddStep("press redo", () => pressWithModifier(Key.Z, shift: true));
            AddAssert("hitobject restored", () => EditorBeatmap.HitObjects.Count, () => Is.EqualTo(initialHitObjectCount + 1));

            if (RuntimeInfo.OS != RuntimeInfo.Platform.macOS)
            {
                AddStep("press undo", () => pressWithModifier(Key.Z));
                AddAssert("hitobject removed", () => EditorBeatmap.HitObjects.Count, () => Is.EqualTo(initialHitObjectCount));

                AddStep("press alternative redo", () => pressWithModifier(Key.Y));
                AddAssert("hitobject restored", () => EditorBeatmap.HitObjects.Count, () => Is.EqualTo(initialHitObjectCount + 1));
            }
        }

        [Test]
        public void TestRebindUndo()
        {
            AddStep("rebind undo to ctrl+u", () => realm.Write(r =>
            {
                var undoBindings = r.All<RealmKeyBinding>()
                                    .Where(b => b.RulesetName == null && b.ActionInt == (int)GlobalAction.EditorUndo)
                                    .ToList();

                for (int i = 0; i < undoBindings.Count; i++)
                {
                    originalUndoBindings[undoBindings[i].ID] = undoBindings[i].KeyCombinationString;
                    undoBindings[i].KeyCombination = i == 0 ? new KeyCombination(InputKey.Control, InputKey.U) : new KeyCombination(InputKey.None);
                }
            }));

            // the key binding container reloads its bindings asynchronously after the realm change, so keep trying until it takes effect.
            // pressing undo again after the change has been undone is a no-op.
            AddUntilStep("undo via new binding", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Key(Key.U);
                InputManager.ReleaseKey(Key.ControlLeft);

                return EditorBeatmap.HitObjects.Count == initialHitObjectCount;
            });

            AddStep("press redo", () => pressWithModifier(Key.Z, shift: true));
            AddAssert("hitobject restored", () => EditorBeatmap.HitObjects.Count, () => Is.EqualTo(initialHitObjectCount + 1));

            AddStep("press old undo binding", () => pressWithModifier(Key.Z));
            AddAssert("nothing was undone", () => EditorBeatmap.HitObjects.Count, () => Is.EqualTo(initialHitObjectCount + 1));
        }

        private void pressWithModifier(Key key, bool shift = false)
        {
            InputManager.PressKey(platformModifier);
            if (shift)
                InputManager.PressKey(Key.ShiftLeft);

            InputManager.Key(key);

            if (shift)
                InputManager.ReleaseKey(Key.ShiftLeft);
            InputManager.ReleaseKey(platformModifier);
        }

        private void restoreUndoBindings()
        {
            if (originalUndoBindings.Count == 0)
                return;

            realm.Write(r =>
            {
                foreach (var (id, keyCombination) in originalUndoBindings)
                {
                    var binding = r.Find<RealmKeyBinding>(id);
                    if (binding != null)
                        binding.KeyCombinationString = keyCombination;
                }
            });

            originalUndoBindings.Clear();
        }
    }
}
