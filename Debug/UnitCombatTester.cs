#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
using UnityEngine;
using Unity.Mathematics;
using OneBitRob.AI;
using OneBitRob.FX;
using OneBitRob.ECS;

namespace OneBitRob.Tools
{
    [DisallowMultipleComponent]
    public class UnitCombatTester : MonoBehaviour
    {
        [Header("Who am I?")]
        [SerializeField] UnitBrain self;

        [Header("Who do I hit?")]
        [SerializeField] UnitBrain target;

        [Header("Options")]
        [Tooltip("If the unit has an ECS entity, also write the corresponding requests into ECS.")]
        public bool UseEcsIfAvailable = false;

        [Tooltip("Draw helper gizmos while testing.")]
        public bool DrawGizmos = true;

        [Header("Overrides (optional)")]
        public bool OverrideMelee = false;
        public float MeleeRange = 1.8f;
        [Range(0f, 179f)] public float MeleeHalfAngleDeg = 60f;
        public int MeleeMaxTargets = 3;

        public bool OverrideRanged = false;
        public float RangedSpeed = 60f;
        public float RangedMaxDistance = 40f;
        [Range(0,1)] public float RangedCritChance = 0f;
        [Min(1f)] public float RangedCritMultiplier = 1.5f;

        [Header("Spell")]
        public SpellDefinition SpellToTest;

        [Header("Spell DoT/HoT Simulation (sandbox only)")]
        public bool SimulateOverTimeInSandbox = true;

        void OnValidate()
        {
            if (!self) self = GetComponent<UnitBrain>();
        }

#if ODIN_INSPECTOR
        [Button(ButtonSizes.Large), GUIColor(0.9f, 0.55f, 0.55f)]
#else
        [ContextMenu("Test/Melee Attack Target")]
#endif
        public void TestMeleeAttackTarget()
        {
            if (!self) { Debug.LogWarning("[UnitCombatTester] Self UnitBrain missing."); return; }
            if (!target) { Debug.LogWarning("[UnitCombatTester] Target UnitBrain missing."); return; }

            // Pull melee definition if available
            MeleeWeaponDefinition mw = self.UnitDefinition && self.UnitDefinition.weapon is MeleeWeaponDefinition mwd
                ? mwd : null;

            float range       = (!OverrideMelee && mw) ? Mathf.Max(0.01f, mw.attackRange) : Mathf.Max(0.01f, MeleeRange);
            float halfAngle   = (!OverrideMelee && mw) ? mw.halfAngleDeg : MeleeHalfAngleDeg;
            float halfAngleRad= math.radians(math.clamp(halfAngle, 0f, 179f));
            float damage      = (!OverrideMelee && mw) ? Mathf.Max(1f, mw.attackDamage)
                                                       : (self.UnitDefinition && self.UnitDefinition.weapon ? self.UnitDefinition.weapon.attackDamage : 10f);
            float invuln      = (!OverrideMelee && mw) ? Mathf.Max(0f, mw.invincibility) : 0f;
            int maxTargets    = (!OverrideMelee && mw) ? Mathf.Max(1, mw.maxTargets) : Mathf.Max(1, MeleeMaxTargets);

            // ANIMATION (melee)
            if (mw) self.CombatSubsystem?.PlayMeleeAttack(mw.attackAnimations);

            // Mono-side resolution (matches your ECS melee resolver)
            RunMeleeHitMono(self, transform.position, transform.forward, range, halfAngleRad, damage, invuln, self.GetDamageableLayerMask().value, maxTargets);

            // Optional ECS echo
            if (UseEcsIfAvailable && self.GetEntity() != Unity.Entities.Entity.Null)
            {
                var e = self.GetEntity();
                var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
                if (world != null)
                {
                    var em = world.EntityManager;
                    var req = new OneBitRob.ECS.MeleeHitRequest
                    {
                        Origin = transform.position,
                        Forward= transform.forward,
                        Range  = range,
                        HalfAngleRad = halfAngleRad,
                        Damage = damage,
                        Invincibility = invuln,
                        LayerMask = self.GetDamageableLayerMask().value,
                        MaxTargets = maxTargets,
                        HasValue = 1
                    };
                    if (em.HasComponent<OneBitRob.ECS.MeleeHitRequest>(e)) em.SetComponentData(e, req);
                    else em.AddComponentData(e, req);
                }
            }
        }

#if ODIN_INSPECTOR
        [Button(ButtonSizes.Large), GUIColor(0.55f, 0.7f, 0.95f)]
#else
        [ContextMenu("Test/Ranged Attack Target")]
#endif
        public void TestRangedAttackTarget()
        {
            if (!self) { Debug.LogWarning("[UnitCombatTester] Self UnitBrain missing."); return; }
            if (!self.CombatSubsystem) { Debug.LogWarning("[UnitCombatTester] CombatSubsystem missing on self."); return; }

            RangedWeaponDefinition rw = self.UnitDefinition && self.UnitDefinition.weapon is RangedWeaponDefinition rwd
                ? rwd : null;

            if (rw == null && !OverrideRanged)
            {
                Debug.LogWarning("[UnitCombatTester] No RangedWeaponDefinition on this unit. Enable OverrideRanged or assign a ranged weapon.");
                return;
            }

            StartCoroutine(CoRangedFire(rw));
        }

        System.Collections.IEnumerator CoRangedFire(RangedWeaponDefinition rw)
        {
            // ANIMATION: Prepare
            if (rw) self.CombatSubsystem?.PlayRangedPrepare(rw.animations);

            float windup = (rw ? Mathf.Max(0f, rw.windupSeconds) : 0f);
            if (windup > 0f) yield return new WaitForSeconds(windup);

            // Compute origin/aim after windup (like live system)
            ComputeRangedMuzzle(self, out var origin, out var fwd);

            Vector3 dir = fwd;
            if (target)
            {
                Vector3 to = target.transform.position - origin;
                to.y = 0;
                if (to.sqrMagnitude > 1e-6f) dir = to.normalized;
            }

            float speed   = rw ? Mathf.Max(0.01f, rw.projectileSpeed) : Mathf.Max(0.01f, RangedSpeed);
            float maxDist = rw ? Mathf.Max(0.1f, rw.projectileMaxDistance) : Mathf.Max(0.1f, RangedMaxDistance);
            float damage  = rw ? Mathf.Max(0f, rw.attackDamage)
                               : (self.UnitDefinition && self.UnitDefinition.weapon ? self.UnitDefinition.weapon.attackDamage : 10f);

            // Check pooler presence (otherwise you won't see a projectile in sandbox)
            bool hasPool = self.CombatSubsystem.HasRangedProjectileConfigured();
            if (!hasPool)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[UnitCombatTester] No projectile pooler for this weapon. Ensure your sandbox scene has the same ProjectilePools as the main scene. Weapon projectileId: {(rw ? rw.projectileId : "<override/no-id>")}");
#endif
            }

#if UNITY_EDITOR
            if (DrawGizmos) Debug.DrawRay(origin, dir * 1.25f, Color.red, 0.5f, false);
#endif

            // Fire + ANIMATION: Fire
            self.CombatSubsystem.FireProjectile(origin, dir, self.gameObject, speed, damage, maxDist, self.GetDamageableLayerMask().value, RangedCritChance, RangedCritMultiplier);
            if (rw) self.CombatSubsystem?.PlayRangedFire(rw.animations);
        }

#if ODIN_INSPECTOR
        [Button(ButtonSizes.Large), GUIColor(0.6f, 0.95f, 0.6f)]
#else
        [ContextMenu("Test/Cast Selected Spell")]
#endif
        public void TestCastSelectedSpell()
        {
            if (!self) { Debug.LogWarning("[UnitCombatTester] Self UnitBrain missing."); return; }
            if (!self.CombatSubsystem) { Debug.LogWarning("[UnitCombatTester] CombatSubsystem missing on self."); return; }
            if (!SpellToTest) { Debug.LogWarning("[UnitCombatTester] Assign SpellToTest in inspector."); return; }

            StartCoroutine(CoCastSpell(SpellToTest));
        }

        System.Collections.IEnumerator CoCastSpell(SpellDefinition spell)
        {
            // ANIMATION: Prepare (two-stage)
            self.CombatSubsystem?.PlaySpellPrepare(spell.animations);

            float cast = Mathf.Max(0f, spell.CastTime);
            if (cast > 0f) yield return new WaitForSeconds(cast);

            switch (spell.Kind)
            {
                case SpellKind.ProjectileLine:
                {
                    // Compute origin/aim (projectile)
                    ComputeRangedMuzzle(self, out var origin, out var fwd);
                    Vector3 dir = fwd;
                    Vector3 aim = target ? target.transform.position : (origin + fwd);
                    Vector3 to = aim - origin; to.y = 0f;
                    if (to.sqrMagnitude > 1e-6f) dir = to.normalized;

                    float dmg = spell.EffectType == SpellEffectType.Negative ? Mathf.Max(0f, spell.Amount) : -Mathf.Max(0f, spell.Amount);
                    int layer = spell.TargetLayerMask.value != 0 ? spell.TargetLayerMask.value : self.GetDamageableLayerMask().value;

                    self.CombatSubsystem.FireSpellProjectile(
                        spell.ProjectileId,
                        origin,
                        dir,
                        self.gameObject,
                        Mathf.Max(0.01f, spell.ProjectileSpeed),
                        dmg,
                        Mathf.Max(0.1f, spell.ProjectileMaxDistance),
                        layer,
                        Mathf.Max(0f, spell.ProjectileRadius),
                        pierce: true
                    );
                    break;
                }

                case SpellKind.EffectOverTimeTarget:
                {
                    if (!target || !target.Health) { Debug.LogWarning("[UnitCombatTester] Need a valid target with Health."); break; }
                    float amt = Mathf.Max(0f, spell.Amount);
                    bool isHot = spell.EffectType == SpellEffectType.Positive;

                    if (!SimulateOverTimeInSandbox)
                    {
                        target.Health.Damage(isHot ? -amt : amt, self.gameObject, 0f, 0f, (target.transform.position - transform.position).normalized);
                        DamageNumbersManager.Popup(new DamageNumbersParams
                        {
                            Kind = isHot ? DamagePopupKind.Heal : DamagePopupKind.Dot,
                            Follow = target.transform,
                            Position = target.transform.position,
                            Amount = amt
                        });
                    }
                    else
                    {
                        StartCoroutine(CoTicksTarget(target, amt, spell.TickInterval, spell.Duration, isHot));
                    }
                    break;
                }

                case SpellKind.EffectOverTimeArea:
                {
                    Vector3 center = target ? target.transform.position : transform.position + transform.forward * Mathf.Min(spell.Range, 6f);
                    float radius = Mathf.Max(0.1f, spell.AreaRadius);
                    float amt = Mathf.Max(0f, spell.Amount);
                    bool isHot = spell.EffectType == SpellEffectType.Positive;

                    if (!SimulateOverTimeInSandbox)
                    {
                        ApplyAreaTick(center, radius, amt, isHot, spell.TargetLayerMask.value != 0 ? spell.TargetLayerMask.value : ~0);
                    }
                    else
                    {
                        StartCoroutine(CoTicksArea(center, radius, amt, spell.TickInterval, spell.Duration, isHot, spell.TargetLayerMask.value != 0 ? spell.TargetLayerMask.value : ~0));
                    }
#if UNITY_EDITOR
                    if (DrawGizmos) DrawDisc(center, radius, new Color(0.2f, 1f, 0.2f, 0.75f), 1.5f);
#endif
                    break;
                }

                case SpellKind.Chain:
                {
                    if (!target) { Debug.LogWarning("[UnitCombatTester] Need a starting target for Chain."); break; }
                    int hops = Mathf.Max(1, spell.ChainMaxTargets);
                    float hopRadius = Mathf.Max(0.1f, spell.ChainRadius);
                    float delay = Mathf.Max(0f, spell.ChainPerJumpDelay);
                    bool heal = spell.EffectType == SpellEffectType.Positive;
                    float amt = Mathf.Max(0f, spell.Amount);
                    StartCoroutine(CoChain(target, hops, hopRadius, delay, heal, amt));
                    break;
                }

                case SpellKind.Summon:
                default:
                    Debug.Log("[UnitCombatTester] Summon/Other: test in main ECS scene (bridge handles spawning).");
                    break;
            }

            // ANIMATION: Fire
            self.CombatSubsystem?.PlaySpellFire(spell.animations);
        }

        // ───────────────────────────────────────────────────────────── helpers (unchanged core)
        static void ComputeRangedMuzzle(UnitBrain brain, out Vector3 origin, out Vector3 forward)
        {
            var t = brain.transform;
            forward = t.forward;

            float muzzleForward = 0.6f;
            Vector3 muzzleLocal = Vector3.zero;

            if (brain.UnitDefinition && brain.UnitDefinition.weapon is RangedWeaponDefinition rw)
            {
                muzzleForward = Mathf.Max(0f, rw.muzzleForward);
                muzzleLocal   = rw.muzzleLocalOffset;
            }

            origin = t.position
                   + t.forward * muzzleForward
                   + t.right   * muzzleLocal.x
                   + t.up      * muzzleLocal.y
                   + t.forward * muzzleLocal.z;
        }

        void RunMeleeHitMono(UnitBrain attacker, Vector3 origin, Vector3 fwd, float range, float halfAngleRad, float damage, float invuln, int layerMask, int maxTargets)
        {
            const int kMax = 256;
            var hits = new Collider[kMax];

            int count = Physics.OverlapSphereNonAlloc(origin, range, hits, layerMask, QueryTriggerInteraction.Collide);
            if (count == 0)
                count = Physics.OverlapSphereNonAlloc(origin, range, hits, ~0, QueryTriggerInteraction.Collide);

            bool attackerIsEnemy = attacker.UnitDefinition && attacker.UnitDefinition.isEnemy;
            float cosHalf = math.cos(halfAngleRad);
            float cos2    = cosHalf * cosHalf;
            float rangeSq = range * range;

#if UNITY_EDITOR
            if (DrawGizmos) DrawDisc(origin, range, new Color(1f, 0f, 0f, 0.25f), 0.75f);
#endif
            int applied = 0;
            for (int i = 0; i < count; i++)
            {
                var col = hits[i];
                if (!col) continue;
                if (col.transform.root == attacker.transform.root) continue;

                var tb = col.GetComponentInParent<UnitBrain>();
                if (!tb || !tb.Health || !tb.IsTargetAlive()) continue;

                bool targetIsEnemy = tb.UnitDefinition && tb.UnitDefinition.isEnemy;
                if (attackerIsEnemy == targetIsEnemy) continue;

                Vector3 to = tb.transform.position - origin;
                float sq = to.sqrMagnitude;
                if (sq > rangeSq) continue;

                float dot = Vector3.Dot(fwd.normalized, to.normalized);
                if (dot <= 0f) continue;
                if ((dot * dot) < (cos2)) continue;

                tb.Health.Damage(damage, attacker.gameObject, 0f, invuln, to.normalized);

                DamageNumbersManager.Popup(new DamageNumbersParams
                {
                    Kind = DamagePopupKind.Damage,
                    Follow = tb.transform,
                    Position = tb.transform.position,
                    Amount = damage
                });

#if UNITY_EDITOR
                if (DrawGizmos) Debug.DrawLine(origin, tb.transform.position, new Color(1f, 0.95f, 0.2f, 1f), 0.35f, false);
#endif
                if (++applied >= Mathf.Max(1, maxTargets)) break;
            }
        }

        System.Collections.IEnumerator CoTicksTarget(UnitBrain tb, float amount, float interval, float duration, bool heal)
        {
            float end = Time.time + Mathf.Max(0f, duration);
            float next = 0f;
            while (Time.time < end)
            {
                if (Time.time >= next)
                {
                    if (tb && tb.Health)
                    {
                        float val = heal ? -amount : amount;
                        tb.Health.Damage(val, self ? self.gameObject : null, 0f, 0f, Vector3.zero);
                        DamageNumbersManager.Popup(new DamageNumbersParams
                        {
                            Kind = heal ? DamagePopupKind.Hot : DamagePopupKind.Dot,
                            Follow = tb.transform,
                            Position = tb.transform.position,
                            Amount = amount
                        });
                    }
                    next = Time.time + Mathf.Max(0.05f, interval);
                }
                yield return null;
            }
        }

        void ApplyAreaTick(Vector3 center, float radius, float amount, bool heal, int layerMask)
        {
            var cols = new Collider[256];
            int count = Physics.OverlapSphereNonAlloc(center, radius, cols, layerMask, QueryTriggerInteraction.Collide);
            if (count == 0) count = Physics.OverlapSphereNonAlloc(center, radius, cols, ~0, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                var col = cols[i]; if (!col) continue;
                var tb = col.GetComponentInParent<UnitBrain>();
                if (!tb || !tb.Health) continue;

                float val = heal ? -amount : amount;
                tb.Health.Damage(val, self ? self.gameObject : null, 0f, 0f, (tb.transform.position - center).normalized);

                DamageNumbersManager.Popup(new DamageNumbersParams
                {
                    Kind = heal ? DamagePopupKind.Hot : DamagePopupKind.Dot,
                    Follow = tb.transform,
                    Position = tb.transform.position,
                    Amount = amount
                });
            }
        }

        System.Collections.IEnumerator CoTicksArea(Vector3 center, float radius, float amount, float interval, float duration, bool heal, int layerMask)
        {
            float end = Time.time + Mathf.Max(0f, duration);
            float next = 0f;
            while (Time.time < end)
            {
                if (Time.time >= next)
                {
                    ApplyAreaTick(center, radius, amount, heal, layerMask);
                    next = Time.time + Mathf.Max(0.05f, interval);
                }
                yield return null;
            }
        }

        System.Collections.IEnumerator CoChain(UnitBrain start, int hops, float radius, float perHopDelay, bool heal, float amount)
        {
            var visited = new System.Collections.Generic.HashSet<UnitBrain>();
            var current = start;

            while (current && hops-- > 0)
            {
                visited.Add(current);

                if (current.Health)
                {
                    float val = heal ? -amount : amount;
                    current.Health.Damage(val, self ? self.gameObject : null, 0f, 0f, Vector3.zero);
                    DamageNumbersManager.Popup(new DamageNumbersParams
                    {
                        Kind = heal ? DamagePopupKind.Heal : DamagePopupKind.Damage,
                        Follow = current.transform,
                        Position = current.transform.position,
                        Amount = amount
                    });
                }

                if (perHopDelay > 0f) yield return new WaitForSeconds(perHopDelay);

                current = FindNextChain(current, radius, heal);
                if (!current || visited.Contains(current)) break;
            }
        }

        UnitBrain FindNextChain(UnitBrain from, float radius, bool heal)
        {
            var cols = new Collider[256];
            int count = Physics.OverlapSphereNonAlloc(from.transform.position, radius, cols, ~0, QueryTriggerInteraction.Collide);

            bool fromIsEnemy = from.UnitDefinition && from.UnitDefinition.isEnemy;

            UnitBrain best = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var col = cols[i]; if (!col) continue;
                var tb = col.GetComponentInParent<UnitBrain>();
                if (!tb || tb == from) continue;

                bool tbIsEnemy = tb.UnitDefinition && tb.UnitDefinition.isEnemy;

                if (heal) { if (tbIsEnemy != fromIsEnemy) continue; }
                else      { if (tbIsEnemy == fromIsEnemy) continue; }

                float d = Vector3.SqrMagnitude(tb.transform.position - from.transform.position);
                if (d < bestDist) { bestDist = d; best = tb; }
            }

            return best;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (!DrawGizmos || !self) return;

            // Melee cone/range
            float range = OverrideMelee ? MeleeRange :
                (self.UnitDefinition && self.UnitDefinition.weapon is MeleeWeaponDefinition mw ? mw.attackRange : 1.5f);
            float halfDeg = OverrideMelee ? MeleeHalfAngleDeg :
                (self.UnitDefinition && self.UnitDefinition.weapon is MeleeWeaponDefinition mw2 ? mw2.halfAngleDeg : 60f);

            UnityEditor.Handles.color = new Color(1f, 0.3f, 0.2f, 0.5f);
            UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, range);
            var fwd = transform.forward;
            var left = Quaternion.AngleAxis(-halfDeg, Vector3.up) * fwd;
            var right = Quaternion.AngleAxis(halfDeg, Vector3.up) * fwd;
            Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.9f);
            Gizmos.DrawLine(transform.position, transform.position + left * range);
            Gizmos.DrawLine(transform.position, transform.position + right * range);

            // Ranged muzzle + aim
            ComputeRangedMuzzle(self, out var origin, out var rfwd);
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(origin, 0.05f);
            Gizmos.DrawRay(origin, rfwd * 0.75f);

            // Spell AoE preview
            if (SpellToTest && (SpellToTest.Kind == SpellKind.EffectOverTimeArea))
            {
                float r = Mathf.Max(0.1f, SpellToTest.AreaRadius);
                UnityEditor.Handles.color = new Color(0.2f, 1f, 0.2f, 0.4f);
                UnityEditor.Handles.DrawWireDisc(transform.position + transform.forward * Mathf.Min(SpellToTest.Range, 6f), Vector3.up, r);
            }
        }

        static void DrawDisc(Vector3 center, float radius, Color color, float duration)
        {
            const int seg = 48;
            Vector3 prev = center + Vector3.right * radius;
            for (int i = 1; i <= seg; i++)
            {
                float t = (i / (float)seg) * 2f * Mathf.PI;
                Vector3 p = center + new Vector3(Mathf.Cos(t) * radius, 0f, Mathf.Sin(t) * radius);
                Debug.DrawLine(prev, p, color, duration, false);
                prev = p;
            }
        }
#endif
    }
}
