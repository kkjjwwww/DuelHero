using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using DuelHero.Cards;
using DuelHero.Units;
using UnityEngine;
namespace DuelHero.Battle
{
    public sealed class CardEffectExecutor : MonoBehaviour
    {
        private const float VfxDurationMultiplier = 1.3f;
        private const float VfxLifetime = .3f * VfxDurationMultiplier;
        [SerializeField] private CombatEffectResolver combatEffects;
        [SerializeField] private GameObject slashVfx, dashVfx, headCrushVfx, hitVfx, blockVfx;
        [SerializeField] private GameObject energyRestoreVfx;
        [SerializeField] private GameObject moveDustVfx;
        [SerializeField] private GameObject tearWoundVfx, whirlwindVfx, thornThrowVfx, thornImpactVfx;
        [SerializeField] private GameObject riftTileVfx, riftBurstVfx;
        private readonly Dictionary<string, List<GameObject>> riftTiles = new();
        private Tween whirlwindSorting;

        private void PlayMoveDust(GridMovement unit, Vector3 visualStart, Vector2Int direction)
        {
            if (!Application.isPlaying || moveDustVfx == null || !unit.isActiveAndEnabled) return;
            var grid = unit.Grid;
            var ground = grid.CellToWorld(unit.GridPosition);
            var travel = grid.CellToWorld(unit.GridPosition + direction) - ground;
            if (travel.sqrMagnitude < .0001f) return;
            var feet = ground + Vector3.ProjectOnPlane(visualStart - ground, grid.transform.up);
            var rotation = Quaternion.LookRotation(Vector3.Cross(travel, grid.transform.up), grid.transform.up);
            var instance = Instantiate(moveDustVfx, feet + grid.transform.up * .02f,
                rotation * moveDustVfx.transform.rotation);
            DuelHero.Audio.SoundEffectPlayer.Play("battle.move.launch");
            Destroy(instance, .42f);
        }
        private Material thrustLineMaterial;
        private readonly Dictionary<Transform, Tween> hitShakes = new();

        private void OnEnable()
        {
            if (combatEffects == null) return;
            combatEffects.ActionResolved += PlayDamageVfx;
            combatEffects.EnergyRestored += PlayEnergyRestoreVfx;
        }

        private void OnDisable()
        {
            if (combatEffects != null)
            {
                combatEffects.ActionResolved -= PlayDamageVfx;
                combatEffects.EnergyRestored -= PlayEnergyRestoreVfx;
            }
            foreach (var shake in hitShakes.Values.ToArray()) shake.Kill();
            foreach (var actorId in riftTiles.Keys.ToArray()) ClearRiftTiles(actorId);
            whirlwindSorting?.Kill();
        }

        private void PlayEnergyRestoreVfx(GridMovement unit)
        {
            if (!Application.isPlaying || energyRestoreVfx == null || !unit.isActiveAndEnabled) return;
            var sprite = unit.GetComponentInChildren<SpriteRenderer>();
            var feet = sprite != null ? sprite.transform.position : unit.transform.position;
            var camera = Camera.main;
            feet -= unit.Grid.transform.up * .06f;
            if (camera != null) feet -= camera.transform.forward * .18f;
            var instance = Instantiate(energyRestoreVfx, feet, energyRestoreVfx.transform.rotation);
            DuelHero.Audio.SoundEffectPlayer.Play("battle.energy.restore");
            foreach (var particles in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var settings = particles.main;
                settings.startSizeMultiplier *= 1.5f;
                if (sprite != null && particles.TryGetComponent<ParticleSystemRenderer>(out var renderer))
                {
                    renderer.sortingLayerID = sprite.sortingLayerID;
                    renderer.sortingOrder = sprite.sortingOrder + 1;
                }
                particles.Play(false);
            }
            Destroy(instance, .7f);
        }

        private void OnDestroy()
        {
            if (thrustLineMaterial != null) Destroy(thrustLineMaterial);
        }

        private void PlayDamageVfx(BattleActionResult result)
        {
            if (result.Kind != BattleActionKind.Damage || result.Outcome != BattleActionOutcome.Success ||
                !combatEffects.TryGetUnit(result.TargetId, out var target, out _)) return;

            var renderer = target.GetComponentInChildren<SpriteRenderer>();
            Vector3 position = renderer != null ? renderer.bounds.center : target.transform.position;
            if (result.ReducedDamage > 0) SpawnVfx(blockVfx, position);
            if (result.Value <= 0) return;

            if (renderer != null) ShakeHitSprite(renderer.transform);
            if (result.HitVfx == "tear_wound" && tearWoundVfx != null)
            {
                var effect = PlayTechniqueVfx(tearWoundVfx, position, tearWoundVfx.transform.rotation);
                if (result.BleedBonusDamage > 0) effect.transform.Find("BleedBonus")?.gameObject.SetActive(true);
                return;
            }
            if (result.HitVfx == "thorn_impact" && thornImpactVfx != null)
            {
                PlayTechniqueVfx(thornImpactVfx, position, thornImpactVfx.transform.rotation);
                return;
            }
            SpawnVfx(result.HitVfx == "head_crush" && headCrushVfx != null ? headCrushVfx : hitVfx, position);
        }

        private void ShakeHitSprite(Transform visual)
        {
            if (hitShakes.TryGetValue(visual, out var previous)) previous.Kill();
            var restingPosition = visual.localPosition;
            hitShakes[visual] = DOVirtual.Float(0f, 1f, .18f, progress =>
                {
                    if (visual == null) return;
                    float offset = Mathf.Sin(progress * Mathf.PI * 4f) * (1f - progress) * .065f;
                    visual.localPosition = restingPosition + Vector3.right * offset;
                })
                .SetEase(Ease.Linear)
                .SetLink(visual.gameObject)
                .OnKill(() =>
                {
                    if (visual != null) visual.localPosition = restingPosition;
                    hitShakes.Remove(visual);
                });
        }

        private void PlayWeaponVfx(UnitAttackPresentation attack, GridMovement unit, CardDefinition card,
            IReadOnlyList<Vector2Int> dashPath = null, bool counter = false)
        {
            if (attack == null || unit == null || card == null) return;
            bool thrust = card.attackVfx == "thrust";
            bool sweep = card.attackVfx == "sweep";
            GameObject prefab = card.attackVfx switch
            {
                "sweep" => slashVfx,
                "dash_slash" => dashVfx,
                _ => null
            };
            if (!thrust && prefab == null && card.attackVfx != "whirlwind" && card.attackVfx != "thorn_throw") return;

            var sprite = unit.GetComponentInChildren<SpriteRenderer>();
            var direction = card.executionDirection;
            if (!CardDirectionRules.IsCardinal(direction))
                direction = sprite != null && sprite.flipX ? Vector2Int.left : Vector2Int.right;
            var origin = unit.Grid.CellToWorld(unit.GridPosition);
            var step = unit.Grid.CellToWorld(unit.GridPosition + direction) - origin;
            var anchor = sprite != null ? sprite.bounds.center : attack.MotionWorldPosition;
            if (card.attackVfx == "whirlwind")
            {
                PlayWhirlwind(unit, attack.MotionWorldPosition, sprite);
                return;
            }
            if (card.attackVfx == "thorn_throw")
            {
                var heightFromFeet = Vector3.Project(anchor - attack.MotionWorldPosition, unit.Grid.transform.up);
                var throwOrigin = anchor - heightFromFeet * .2f;
                PlayThornThrow(unit, card, direction, step, throwOrigin, counter);
                return;
            }
            var weaponHeight = Vector3.Project(anchor - origin, unit.Grid.transform.up);
            var position = attack.MotionWorldPosition + weaponHeight + step * .85f;
            if (thrust)
            {
                PlayThrustLine(position + unit.Grid.transform.up * .08f, step, sprite, direction == Vector2Int.up);
                return;
            }
            if (dashPath != null && dashPath.Count > 0)
            {
                var start = unit.Grid.CellToWorld(dashPath[0]);
                var end = unit.Grid.CellToWorld(dashPath[dashPath.Count - 1]);
                position = (start + end) * .5f + weaponHeight;
            }
            var instance = SpawnVfx(prefab, position, sweep ? 2f : 1f);
            var particles = instance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var particle in particles)
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (!sweep && direction == Vector2Int.left)
            {
                var scale = instance.transform.localScale;
                scale.x = -scale.x;
                instance.transform.localScale = scale;
            }
            else if ((sweep || direction.y != 0) && Camera.main != null)
            {
                var camera = Camera.main;
                var screenStep = camera.WorldToScreenPoint(origin + step) - camera.WorldToScreenPoint(origin);
                float angle = Mathf.Atan2(screenStep.y, screenStep.x) * Mathf.Rad2Deg;
                // Give the directional sweep an additional counterclockwise quarter turn on screen.
                instance.transform.rotation = sweep
                    ? Quaternion.AngleAxis(angle + 180f, camera.transform.forward) * camera.transform.rotation
                    : Quaternion.AngleAxis(angle, camera.transform.forward) * instance.transform.rotation;
            }

            if (direction == Vector2Int.up && sprite != null)
            {
                foreach (var particleRenderer in instance.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    particleRenderer.sortingLayerID = sprite.sortingLayerID;
                    particleRenderer.sortingOrder = sprite.sortingOrder - 1;
                }
            }
            foreach (var particle in particles)
            {
                var settings = particle.main;
                settings.simulationSpace = ParticleSystemSimulationSpace.Local;
                if ((sweep || direction.y != 0) && particle.TryGetComponent<ParticleSystemRenderer>(out var renderer))
                    renderer.alignment = ParticleSystemRenderSpace.Local;
                particle.Play(false);
            }
        }

        private static Quaternion FaceTravel(Vector3 travel, Vector3 up) =>
            Quaternion.LookRotation(Vector3.Cross(travel, up), up);

        private GameObject PlayTechniqueVfx(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            var instance = Instantiate(prefab, position, rotation);
            Destroy(instance, .45f);
            return instance;
        }

        private void PlayWhirlwind(GridMovement unit, Vector3 visualFeet, SpriteRenderer caster)
        {
            if (whirlwindVfx == null) return;
            var ground = unit.Grid.CellToWorld(unit.GridPosition);
            var feet = ground + Vector3.ProjectOnPlane(visualFeet - ground, unit.Grid.transform.up);
            var instance = PlayTechniqueVfx(whirlwindVfx, feet, whirlwindVfx.transform.rotation);
            if (caster == null) return;

            whirlwindSorting?.Kill();
            int originalLayer = caster.sortingLayerID;
            int originalOrder = caster.sortingOrder;
            var foremost = FindObjectsByType<GridMovement>(FindObjectsSortMode.None)
                .Select(unitOnBoard => unitOnBoard.GetComponentInChildren<SpriteRenderer>())
                .Where(sprite => sprite != null)
                .OrderByDescending(sprite => SortingLayer.GetLayerValueFromID(sprite.sortingLayerID))
                .ThenByDescending(sprite => sprite.sortingOrder)
                .FirstOrDefault() ?? caster;
            int layer = foremost.sortingLayerID;
            int order = foremost.sortingOrder + 1;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sortingLayerID = layer;
                renderer.sortingOrder = order;
            }
            caster.sortingLayerID = layer;
            caster.sortingOrder = order + 1;
            whirlwindSorting = DOVirtual.DelayedCall(.45f, () => { }, ignoreTimeScale: false).OnKill(() =>
            {
                if (caster == null) return;
                caster.sortingLayerID = originalLayer;
                caster.sortingOrder = originalOrder;
            });
        }

        private void PlayThornThrow(GridMovement unit, CardDefinition card, Vector2Int direction, Vector3 step,
            Vector3 origin, bool counter)
        {
            if (thornThrowVfx == null) return;
            int cells = card.effects.Where(effect => effect.effectType == "damage" && effect.IsCounter == counter)
                .SelectMany(effect => effect.rangeOffsets)
                .Where(offset => unit.Grid.Contains(unit.GridPosition + offset))
                .Select(offset => offset.x * direction.x + offset.y * direction.y).DefaultIfEmpty(0).Max();
            if (cells <= 0) return;
            var instance = PlayTechniqueVfx(thornThrowVfx, origin,
                FaceTravel(step, unit.Grid.transform.up) * thornThrowVfx.transform.rotation);
            foreach (var particles in instance.GetComponentsInChildren<ParticleSystem>())
            {
                particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var velocity = particles.velocityOverLifetime;
                if (velocity.enabled) velocity.x = cells * step.magnitude / .2f;
                particles.Play(false);
            }
        }

        private void ClearRiftTiles(string actorId)
        {
            if (!riftTiles.TryGetValue(actorId, out var tiles)) return;
            foreach (var tile in tiles)
            {
                if (tile == null) continue;
                tile.SetActive(false);
                Destroy(tile);
            }
            riftTiles.Remove(actorId);
        }

        public bool ResolveRiftImpact(string actorId, CardDefinition card, CombatEffectResolver.DamageBatch batch)
        {
            if (card?.attackVfx != "rift_charge") return false;
            ClearRiftTiles(actorId);
            if (riftBurstVfx == null || !batch.AttackCells.TryGetValue(actorId, out var cells) ||
                cells.Count == 0 || !combatEffects.TryGetUnit(actorId, out var unit, out _)) return false;
            var origin = unit.Grid.CellToWorld(unit.GridPosition);
            var step = unit.Grid.CellToWorld(unit.GridPosition + card.executionDirection) - origin;
            if (step.sqrMagnitude < .0001f) return false;
            var rotation = FaceTravel(step, unit.Grid.transform.up) * riftBurstVfx.transform.rotation;
            foreach (var cell in cells) PlayTechniqueVfx(riftBurstVfx, unit.Grid.CellToWorld(cell), rotation);
            return true;
        }

        private void PlayThrustLine(Vector3 tip, Vector3 step, SpriteRenderer sprite, bool behind)
        {
            if (thrustLineMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader == null) return;
                thrustLineMaterial = new Material(shader);
            }

            var effect = new GameObject("FX_ThrustLine");
            effect.transform.position = tip;
            var line = effect.AddComponent<LineRenderer>();
            line.sharedMaterial = thrustLineMaterial;
            line.positionCount = 2;
            line.startWidth = .045f;
            line.endWidth = .012f;
            line.numCapVertices = 3;
            if (sprite != null)
            {
                line.sortingLayerID = sprite.sortingLayerID;
                line.sortingOrder = sprite.sortingOrder + (behind ? -1 : 1);
            }
            line.SetPosition(0, tip);
            line.SetPosition(1, tip + step * .15f);
            var color = new Color(.58f, .91f, 1f);
            line.startColor = color;
            line.endColor = Color.white;
            float duration = .2f * VfxDurationMultiplier;
            DOVirtual.Float(0f, 1f, duration, progress =>
            {
                line.SetPosition(1, tip + step * Mathf.Lerp(.15f, .8f, progress));
                color.a = 1f - progress;
                line.startColor = color;
                line.endColor = new Color(1f, 1f, 1f, color.a);
            }).SetEase(Ease.OutQuad).SetLink(effect);
            Destroy(effect, duration);
        }

        private GameObject SpawnVfx(GameObject prefab, Vector3 position, float extraDurationMultiplier = 1f)
        {
            if (prefab == null) return null;
            var instance = Instantiate(prefab, position, prefab.transform.rotation);
            foreach (var particleSystem in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                var settings = particleSystem.main;
                settings.simulationSpeed /= VfxDurationMultiplier * extraDurationMultiplier;
            }
            Destroy(instance, VfxLifetime * extraDurationMultiplier);
            return instance;
        }

        public bool Supports(string actorId, CardDefinition card)
        {
            if (combatEffects == null || !combatEffects.CanResolve(actorId)) return false;
            return SupportsDefinition(card);
        }
        public static bool SupportsDefinition(CardDefinition card)
        {
            if (card == null) return true;
            if (WaitCard.IsWait(card) || StunCard.IsStun(card)) return card.energyCost == 0 && card.effects != null && card.effects.Length == 0;
            return card.energyCost >= 0 && card.effects != null && card.effects.Length > 0 && card.effects.All(effect =>
                effect != null && SupportsMovementMetadata(card, effect) && effect.value >= 0 && effect.statusMultiplier >= 0 &&
                (string.IsNullOrEmpty(effect.triggerTiming) || effect.triggerTiming == "action" || effect.IsCounter) &&
                (!effect.IsCounter || new[] { "damage", "guard", "energyRestore", "healthRestore" }.Contains(effect.effectType)) &&
                (effect.statusMultiplier == 0 || (effect.effectType == "damage" && effect.statusId == "bleed")) && (effect.effectType switch
                {
                    "move" => effect.targetType == "자신" && effect.value > 0 && TryDirection(effect.rangeOffsets, out _),
                    "guard" => effect.targetType == "자신" && effect.durationSlots > 0,
                    "energyRestore" => effect.targetType == "자신",
                    "healthRestore" => effect.targetType == "자신",
                    "damage" => effect.targetType == "적" && effect.rangeOffsets != null && effect.rangeOffsets.Length > 0,
                    "applyStatus" => effect.targetType == "적" && (effect.statusId == "stun" || effect.statusId == "bleed") && effect.value > 0 &&
                        card.effects.Any(hit => hit.effectType == "damage" && hit.id == effect.triggerEffectId && hit.resolutionOrder < effect.resolutionOrder && hit.IsCounter == effect.IsCounter),
                    _ => false
                }));
        }
        public static bool SupportsMovementMetadata(CardDefinition card, CardEffectDefinition effect) =>
            (string.IsNullOrEmpty(effect.movementStyle) || effect.IsLongJump || (effect.IsDash && !effect.IsCounter)) &&
            (string.IsNullOrEmpty(effect.rangeBasis) || (effect.UsesMovementPath && effect.effectType == "damage" && !effect.IsCounter &&
                card.effects.Any(e => e.effectType == "move" && !e.IsCounter && e.resolutionOrder < effect.resolutionOrder) &&
                card.effects.Where(e => e.effectType == "move").All(e => e.resolutionOrder < effect.resolutionOrder)));
        public void ExecutePreparation(string actorId, CardDefinition card, int round, bool counter = false, List<Vector2Int> movementPath = null)
        {
            var steps = ExecutePreparationSteps(actorId, card, round, counter, null, null, false, null, movementPath);
            try { while (steps.MoveNext()) { } }
            finally { (steps as System.IDisposable)?.Dispose(); }
        }
        public IEnumerator ExecutePreparationAnimated(string actorId, CardDefinition card, int round,
            UnitJumpPresentation jump, UnitVisualPlacement placement, bool counter = false,
            UnitAttackPresentation attack = null, List<Vector2Int> movementPath = null) =>
            ExecutePreparationSteps(actorId, card, round, counter, jump, placement, true, attack, movementPath);

        public void ShowAttackRange(UnitAttackPresentation attack, string actorId, CardDefinition card,
            IReadOnlyList<Vector2Int> movementPath = null, bool counter = false)
        {
            if (attack == null || card == null || !combatEffects.TryGetUnit(actorId, out var unit, out _)) return;
            var cells = new HashSet<Vector2Int>();
            foreach (var effect in card.effects.Where(e => e.effectType == "damage" && e.IsCounter == counter))
                cells.UnionWith(AttackRangeCalculator.Calculate(unit.GridPosition, effect, movementPath));
            cells.RemoveWhere(cell => !unit.Grid.Contains(cell));
            attack.ShowRange(unit, cells);
        }

        public IEnumerator PlayAttackAnimated(UnitAttackPresentation attack, string actorId, CardDefinition card,
            IReadOnlyList<Vector2Int> movementPath = null, bool counter = false, UnitVisualPlacement placement = null)
        {
            if (attack == null) yield break;
            combatEffects.TryGetUnit(actorId, out var unit, out _);
            try
            {
                ShowAttackRange(attack, actorId, card, movementPath, counter);
                SetCardFacing(placement, unit, card);
                yield return attack.PlayAttack(card?.attackStyle, () => PlayWeaponVfx(attack, unit, card, counter: counter));
            }
            finally
            {
                attack.HideRange();
                if (unit != null) placement?.SetActionFacing(unit, 0);
            }
        }

        public IEnumerator PlayDashImpact(UnitAttackPresentation attack, string actorId, CardDefinition card,
            IReadOnlyList<Vector2Int> movementPath)
        {
            if (attack == null || !attack.isActiveAndEnabled ||
                !combatEffects.TryGetUnit(actorId, out var unit, out _)) yield break;
            try
            {
                ShowAttackRange(attack, actorId, card, movementPath);
                PlayWeaponVfx(attack, unit, card, movementPath);
                yield return new WaitForSeconds(VfxLifetime);
            }
            finally { attack.HideRange(); }
        }

        private static void SetCardFacing(UnitVisualPlacement placement, GridMovement unit, CardDefinition card)
        {
            if (unit != null && CardDirectionRules.CanSelect(card) &&
                CardDirectionRules.IsCardinal(card.executionDirection))
                placement?.SetActionFacing(unit, card.executionDirection.x);
        }

        public void CancelPreparationPresentation(string actorId, UnitVisualPlacement placement = null)
        {
            ClearRiftTiles(actorId);
            if (combatEffects != null && combatEffects.TryGetUnit(actorId, out var movement, out _))
            {
                movement.GetComponent<UnitSpritePresenter>()?.CancelSupportPose();
                placement?.SetActionFacing(movement, 0);
            }
        }

        private IEnumerator ExecutePreparationSteps(string actorId, CardDefinition card, int round, bool counter,
            UnitJumpPresentation jump, UnitVisualPlacement placement, bool animateSupport, UnitAttackPresentation attack, List<Vector2Int> movementPath)
        {
            if (card == null) yield break;
            combatEffects.TryGetUnit(actorId, out var movement, out var stats);
            var sprites = animateSupport ? movement.GetComponent<UnitSpritePresenter>() : null;
            movementPath?.Clear(); movementPath?.Add(movement.GridPosition);
            bool holdFacing = false;
            foreach (var effect in card.effects.Where(e => e.IsCounter == counter).OrderBy(e => e.resolutionOrder))
            {
                if (animateSupport && (effect.effectType == "move" || effect.effectType == "guard" ||
                    effect.effectType == "healthRestore" || effect.effectType == "energyRestore"))
                    SetCardFacing(placement, movement, card);
                switch (effect.effectType)
                {
                    case "move":
                        TryDirection(effect.rangeOffsets, out var direction);
                        bool dash = effect.IsDash && attack != null && attack.isActiveAndEnabled;
                        Vector3 dashStart = dash ? attack.MotionWorldPosition : Vector3.zero;
                        bool animate = !effect.IsDash && jump != null && jump.isActiveAndEnabled;
                        bool longJump = animate && effect.IsLongJump;
                        Vector3 jumpStart = longJump ? jump.MotionWorldPosition : Vector3.zero;
                        bool moved = false;
                        for (int step = 0; step < effect.value; step++)
                        {
                            Vector3 before = animate && !longJump ? jump.MotionWorldPosition : Vector3.zero;
                            if (!movement.TryMove(direction, card.id, round)) break;
                            moved = true;
                            movementPath?.Add(movement.GridPosition);
                            if (animate && !longJump)
                            {
                                // Include layout offsets when entering/leaving an occupied cell.
                                placement?.Refresh();
                                yield return jump.PlayJump(jump.MotionWorldPosition - before,
                                    () => PlayMoveDust(movement, before, direction));
                                if (!movement.isActiveAndEnabled) yield break;
                            }
                        }
                        if (longJump && moved)
                        {
                            placement?.Refresh();
                            yield return jump.PlayJump(jump.MotionWorldPosition - jumpStart,
                                () => PlayMoveDust(movement, jumpStart, direction));
                            if (!movement.isActiveAndEnabled) yield break;
                        }
                        if (dash)
                        {
                            holdFacing = true;
                            placement?.Refresh();
                            System.Action<float> markRift = null;
                            if (riftTileVfx != null && card.attackVfx == "rift_charge" && movementPath?.Count > 0)
                            {
                                ClearRiftTiles(actorId);
                                var tiles = new List<GameObject>();
                                riftTiles.Add(actorId, tiles);
                                var ground = movement.Grid.CellToWorld(movement.GridPosition);
                                var travel = movement.Grid.CellToWorld(movement.GridPosition + direction) - ground;
                                var rotation = FaceTravel(travel, movement.Grid.transform.up) * riftTileVfx.transform.rotation;
                                markRift = progress =>
                                {
                                    int arrived = Mathf.FloorToInt(progress * (movementPath.Count - 1)) + 1;
                                    while (tiles.Count < arrived)
                                        tiles.Add(Instantiate(riftTileVfx, movement.Grid.CellToWorld(movementPath[tiles.Count]), rotation));
                                };
                            }
                            yield return attack.PlayDash(attack.MotionWorldPosition - dashStart,
                                onLaunch: () =>
                                {
                                    if (moved) PlayMoveDust(movement, dashStart, direction);
                                    markRift?.Invoke(0f);
                                }, onTravel: markRift);
                            if (!attack.IsPlaying) ClearRiftTiles(actorId);
                        }
                        break;
                    case "guard":
                        combatEffects.ResolveGuard(actorId, card.id, effect, round);
                        holdFacing = true;
                        break;
                    case "energyRestore": stats.RestoreEnergy(effect.value); break;
                    case "healthRestore": stats.Heal(effect.value); break;
                }
                if (sprites != null && effect.value > 0)
                {
                    if (effect.effectType == "guard") yield return sprites.PlaySupportPose(UnitPose.Block, true);
                    else if (effect.effectType == "healthRestore" || effect.effectType == "energyRestore")
                        yield return sprites.PlaySupportPose(UnitPose.Heal);
                    if (!movement.isActiveAndEnabled) yield break;
                }
            }
            if (animateSupport && !holdFacing) placement?.SetActionFacing(movement, 0);
        }
        public static bool TryDirection(Vector2Int[] offsets, out Vector2Int direction)
        {
            direction = offsets != null && offsets.Length == 1 ? offsets[0] : Vector2Int.zero;
            return direction == Vector2Int.up || direction == Vector2Int.down || direction == Vector2Int.left || direction == Vector2Int.right;
        }
    }
}
