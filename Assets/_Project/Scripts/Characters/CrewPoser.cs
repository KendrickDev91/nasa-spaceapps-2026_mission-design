using UnityEngine;

namespace MissionDesign.Characters
{
    public enum CrewReaction { None, Celebrate, Stress, PointScreen, Type }

    /// <summary>
    /// Animación procedural sobre el esqueleto Humanoid, aplicada después del Animator (LateUpdate):
    /// • Sentado: cadera sobre el asiento, muslos hacia delante, ligera inclinación y respiración.
    /// • Brazos con IK de dos huesos (hombro → codo → mano): las manos van a objetivos reales —el teclado (con pulsaciones
    ///   alternas), arriba con los puños al celebrar, juntas al aplaudir, sobre la cabeza por tensión o señalando la pantalla—
    ///   y el codo se dobla hacia un vector polar natural (abajo, afuera y atrás).
    /// • Cabeza que mira la pantalla, hacia arriba al celebrar o hacia abajo al preocuparse, con miradas ocasionales al costado.
    /// Todas las transiciones se mezclan suavemente.
    /// </summary>
    public sealed class CrewPoser : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [Tooltip("Punto del asiento (superficie). Si es nulo, el personaje está de pie.")]
        [SerializeField] private Transform seat;
        [SerializeField] private bool seatedByDefault = true;
        [Tooltip("Estilo al celebrar: 0 = brazos en V con puños, 1 = aplaude frente al pecho, 2 = aplaude sobre la cabeza, 3 = aplaude y luego levanta los brazos.")]
        [SerializeField] private int variant;
        [SerializeField] private float blendSpeed = 3f;
        [Tooltip("Ambiente: de vez en cuando señala la pantalla o mira al compañero.")]
        [SerializeField] private bool ambient;

        private Transform hips, spine, chest, head, lUpLeg, lLeg, lFoot, rUpLeg, rLeg, rFoot, lArm, lFore, lHand, rArm, rFore, rHand;
        private Transform lIndex, lLittle, rIndex, rLittle;
        private Vector3 headForwardLocal;
        private float sit, armsUp, clap, handsHead, point, bounce, typing;
        private float sitTarget, armsUpTarget, clapTarget, handsHeadTarget, pointTarget, bounceTarget;
        private float reactionEnd, pendingAt = -1f;
        private bool highClap;
        private float switchToCheerAt = -1f;
        /// <summary>Palmadas por segundo de esta persona (cada una aplaude a su ritmo).</summary>
        private float clapRate = 3f;
        private CrewReaction pending = CrewReaction.None;
        private float phase, nextAmbient, glanceUntil;
        private float glanceSide = 1f;

        public CrewReaction Current { get; private set; }
        public float SitAmount => sit;
        public bool IsStanding => sit < 0.2f;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            phase = Random.value * 10f;
            clapRate = 2.4f + Random.value * 1.2f;
            nextAmbient = Time.time + Random.Range(4f, 12f);
            Bind();
            sit = sitTarget = seat != null && seatedByDefault ? 1f : 0f;
        }

        private void Bind()
        {
            if (animator == null || !animator.isHuman) return;
            Transform B(HumanBodyBones b) => animator.GetBoneTransform(b);
            hips = B(HumanBodyBones.Hips); spine = B(HumanBodyBones.Spine); chest = B(HumanBodyBones.Chest); head = B(HumanBodyBones.Head);
            lUpLeg = B(HumanBodyBones.LeftUpperLeg); lLeg = B(HumanBodyBones.LeftLowerLeg); lFoot = B(HumanBodyBones.LeftFoot);
            rUpLeg = B(HumanBodyBones.RightUpperLeg); rLeg = B(HumanBodyBones.RightLowerLeg); rFoot = B(HumanBodyBones.RightFoot);
            lArm = B(HumanBodyBones.LeftUpperArm); lFore = B(HumanBodyBones.LeftLowerArm); lHand = B(HumanBodyBones.LeftHand);
            rArm = B(HumanBodyBones.RightUpperArm); rFore = B(HumanBodyBones.RightLowerArm); rHand = B(HumanBodyBones.RightHand);
            lIndex = B(HumanBodyBones.LeftIndexProximal); lLittle = B(HumanBodyBones.LeftLittleProximal);
            rIndex = B(HumanBodyBones.RightIndexProximal); rLittle = B(HumanBodyBones.RightLittleProximal);
            if (head != null) headForwardLocal = Quaternion.Inverse(head.rotation) * transform.forward;
        }

        public void Configure(Animator a, Transform seatPoint, bool seated, int celebrateVariant)
        {
            animator = a;
            seat = seatPoint;
            seatedByDefault = seated;
            variant = celebrateVariant;
            Bind();
            sit = sitTarget = seat != null && seated ? 1f : 0f;
        }

        /// <summary>Sienta al personaje en <paramref name="seatPoint"/> (o lo levanta si es nulo).</summary>
        public void SitAt(Transform seatPoint)
        {
            seat = seatPoint;
            seatedByDefault = seatPoint != null;
            sitTarget = seatedByDefault ? 1f : 0f;
        }

        public void React(CrewReaction reaction, float delay = 0f)
        {
            if (delay > 0f) { pending = reaction; pendingAt = Time.time + delay; return; }
            Current = reaction;
            ResetTargets();
            switch (reaction)
            {
                case CrewReaction.Celebrate:
                    sitTarget = 0f;
                    highClap = variant % 4 == 2;
                    switch (variant % 4)
                    {
                        case 0: armsUpTarget = 1f; bounceTarget = 1f; break;                 // brazos en V, saltitos
                        case 3: clapTarget = 1f; switchToCheerAt = Time.time + 2.4f; break;  // aplaude y luego festeja
                        default: clapTarget = 1f; bounceTarget = 0.35f; break;               // aplauso (pecho o cabeza)
                    }
                    reactionEnd = Time.time + 6f;
                    break;
                case CrewReaction.Stress:
                    handsHeadTarget = 1f;
                    reactionEnd = Time.time + 3.5f;
                    break;
                case CrewReaction.PointScreen:
                    pointTarget = 1f;
                    reactionEnd = Time.time + 2.5f;
                    break;
                default:
                    reactionEnd = 0f;
                    break;
            }
        }

        private void ResetTargets()
        {
            sitTarget = seat != null && seatedByDefault ? 1f : 0f;
            armsUpTarget = clapTarget = handsHeadTarget = pointTarget = bounceTarget = 0f;
            switchToCheerAt = -1f;
        }

        private void LateUpdate()
        {
            if (hips == null) return;
            float dt = Time.deltaTime;
            if (pendingAt > 0f && Time.time >= pendingAt) { pendingAt = -1f; React(pending); }
            if (reactionEnd > 0f && Time.time > reactionEnd) { reactionEnd = 0f; Current = CrewReaction.None; ResetTargets(); }
            if (switchToCheerAt > 0f && Time.time > switchToCheerAt)
            {
                switchToCheerAt = -1f;
                clapTarget = 0f; armsUpTarget = 1f; bounceTarget = 1f;
            }
            if (ambient && Current == CrewReaction.None && Time.time > nextAmbient)
            {
                nextAmbient = Time.time + Random.Range(7f, 16f);
                if (Random.value < 0.5f) React(CrewReaction.PointScreen);
                else { glanceUntil = Time.time + 1.8f; glanceSide = Random.value < 0.5f ? -1f : 1f; }
            }

            float k = blendSpeed * dt;
            sit = Mathf.MoveTowards(sit, sitTarget, k * 0.8f);
            armsUp = Mathf.MoveTowards(armsUp, armsUpTarget, k);
            clap = Mathf.MoveTowards(clap, clapTarget, k);
            handsHead = Mathf.MoveTowards(handsHead, handsHeadTarget, k);
            point = Mathf.MoveTowards(point, pointTarget, k);
            bounce = Mathf.MoveTowards(bounce, bounceTarget, k);
            typing = Mathf.MoveTowards(typing, sit > 0.6f ? 1f : 0f, k);
            Apply();
        }

        private void Apply()
        {
            Vector3 up = Vector3.up, fwd = transform.forward, right = transform.right;
            float t = Time.time + phase;
            float s = Mathf.SmoothStep(0f, 1f, sit);

            // ── Cadera y piernas ──
            if (seat != null && s > 0.001f)
                hips.position = Vector3.Lerp(hips.position, seat.position + up * 0.1f + fwd * 0.02f, s);
            if (bounce > 0.001f)
            {
                float hop = armsUp > clap ? Mathf.Abs(Mathf.Sin(t * 7f)) * 0.07f : (1f - ClapOpen(t)) * 0.02f;
                hips.position += up * (hop * bounce * (1f - s));
            }
            if (s > 0.001f)
            {
                foreach (var (thigh, shin, foot, side) in new[] { (lUpLeg, lLeg, lFoot, -1f), (rUpLeg, rLeg, rFoot, 1f) })
                {
                    Aim(thigh, shin, Vector3.Lerp(-up, fwd + right * side * 0.12f, s).normalized, 1f);
                    Aim(shin, foot, Vector3.Lerp(-up, (-up * 0.95f + fwd * 0.22f).normalized, s), 1f);
                }
            }

            // ── Tronco: inclinación al teclear y respiración ──
            if (spine != null)
            {
                float lean = 13f * s * (1f - armsUp) + Mathf.Sin(t * 1.7f) * 1.2f;
                spine.rotation = Quaternion.AngleAxis(lean, right) * spine.rotation;
            }

            // ── Brazos con IK de dos huesos ──
            float armWeight = Mathf.Max(typing, Mathf.Max(armsUp, Mathf.Max(clap, Mathf.Max(handsHead, point))));
            if (armWeight > 0.001f)
            {
                Vector3 chestPos = chest != null ? chest.position : (lArm.position + rArm.position) * 0.5f - up * 0.12f;
                Arm(lArm, lFore, lHand, -1f, t, s, armWeight, up, fwd, right, chestPos, false);
                Arm(rArm, rFore, rHand, 1f, t, s, armWeight, up, fwd, right, chestPos, true);
            }

            // ── Cabeza: mira la pantalla, arriba al celebrar, abajo con tensión; miradas al compañero ──
            if (head != null)
            {
                Vector3 look = head.position + fwd * 6f + up * (1.2f + 1.2f * s);
                if (Time.time < glanceUntil) look = head.position + right * glanceSide * 3f + fwd * 1.5f;
                // Con los brazos arriba miran hacia arriba; al aplaudir siguen mirando la pantalla grande (apenas más alto).
                look = Vector3.Lerp(look, head.position + fwd * 2f + up * 3.5f, Mathf.Max(armsUp * 0.7f, clap * (highClap ? 0.35f : 0.15f)));
                // Asiente al ritmo de sus palmadas.
                look += up * (clap * (1f - ClapOpen(t)) * 0.25f);
                look = Vector3.Lerp(look, head.position + fwd * 2f - up * 1.2f, handsHead * 0.8f);
                Vector3 desired = (look - head.position).normalized;
                Vector3 current = head.rotation * headForwardLocal;
                var delta = Quaternion.FromToRotation(current, desired);
                delta.ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180f) angle -= 360f;
                angle = Mathf.Clamp(angle, -45f, 45f);
                head.rotation = Quaternion.Slerp(head.rotation, Quaternion.AngleAxis(angle, axis) * head.rotation, 0.75f);
            }
        }

        private void Arm(Transform arm, Transform fore, Transform hand, float side, float t, float s, float weight,
                         Vector3 up, Vector3 fwd, Vector3 right, Vector3 chestPos, bool isRight)
        {
            if (arm == null || fore == null || hand == null) return;
            Vector3 sd = right * side;
            Vector3 shoulder = arm.position;

            // Base: manos sobre el teclado (sentado) con pulsaciones alternas.
            Vector3 keyboard = seat != null ? seat.position + fwd * 0.6f + up * 0.33f : shoulder + fwd * 0.4f - up * 0.35f;
            float tap = Mathf.Max(0f, Mathf.Sin(t * 13f + (isRight ? 0f : 1.7f))) * 0.03f;
            Vector3 target = keyboard + sd * 0.12f + up * tap;
            Vector3 pole = shoulder - up * 0.4f + sd * 0.3f - fwd * 0.25f;
            float w = weight * Mathf.Max(typing, 0.0001f);

            // Celebrar con los brazos arriba (puños que suben y bajan).
            if (armsUp > 0.001f)
            {
                Vector3 hi = shoulder + up * 0.52f + sd * 0.14f + fwd * 0.06f + up * 0.06f * Mathf.Sin(t * 8f + side);
                target = Vector3.Lerp(target, hi, armsUp);
                pole = Vector3.Lerp(pole, shoulder + sd * 0.45f - fwd * 0.2f, armsUp);
                w = Mathf.Max(w, armsUp);
            }
            // Aplaudir: palmas enfrentadas que se cierran rápido y se abren más lento, a la altura del pecho o sobre la cabeza.
            if (clap > 0.001f)
            {
                float open = 0.022f + 0.2f * ClapOpen(t);
                Vector3 center = highClap && head != null
                    ? head.position + up * 0.3f + fwd * 0.16f
                    : chestPos + fwd * 0.27f + up * 0.17f;
                target = Vector3.Lerp(target, center + sd * open, clap);
                Vector3 elbowHint = highClap ? shoulder + sd * 0.5f + up * 0.05f - fwd * 0.05f : shoulder - up * 0.38f + sd * 0.42f - fwd * 0.05f;
                pole = Vector3.Lerp(pole, elbowHint, clap);
                w = Mathf.Max(w, clap);
            }
            // Tensión: manos sobre la cabeza.
            if (handsHead > 0.001f && head != null)
            {
                target = Vector3.Lerp(target, head.position + sd * 0.11f + up * 0.07f - fwd * 0.02f, handsHead);
                pole = Vector3.Lerp(pole, shoulder + sd * 0.5f + up * 0.1f, handsHead);
                w = Mathf.Max(w, handsHead);
            }
            // Señalar la pantalla (brazo derecho).
            if (isRight && point > 0.001f)
            {
                target = Vector3.Lerp(target, shoulder + (fwd * 0.88f + up * 0.42f).normalized * 0.62f, point);
                pole = Vector3.Lerp(pole, shoulder - up * 0.4f + sd * 0.2f, point);
                w = Mathf.Max(w, point);
            }
            TwoBoneIK(arm, fore, hand, target, pole, Mathf.Clamp01(w));

            // Al aplaudir, gira la mano sobre su eje para que la palma mire a la otra mano. La normal de la palma se calcula
            // con los dedos índice y meñique (sirve para cualquier esqueleto humanoide, sin suponer ejes de los huesos).
            if (clap > 0.001f)
            {
                Transform index = isRight ? rIndex : lIndex, little = isRight ? rLittle : lLittle;
                if (index != null && little != null)
                {
                    Vector3 along = ((index.position + little.position) * 0.5f - hand.position).normalized;
                    Vector3 across = index.position - little.position;                 // hacia el pulgar
                    Vector3 palm = side * Vector3.Cross(along, across);                 // normal de la palma (lado interno)
                    Vector3 want = -sd;                                                 // hacia la otra mano
                    Vector3 a = Vector3.ProjectOnPlane(palm, along), b = Vector3.ProjectOnPlane(want, along);
                    if (a.sqrMagnitude > 1e-6f && b.sqrMagnitude > 1e-6f)
                        hand.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(a, b), clap) * hand.rotation;
                }
            }
        }

        /// <summary>Apertura de las manos (0 = palmada, 1 = abiertas): cierre rápido y apertura más lenta, como un aplauso real.</summary>
        private float ClapOpen(float t)
        {
            float p = Mathf.Repeat(t * clapRate, 1f);
            return Mathf.Sin(Mathf.PI * Mathf.Pow(p, 0.55f));
        }

        /// <summary>IK analítica de dos huesos: coloca el codo en el plano del vector polar y la mano en el objetivo.</summary>
        private static void TwoBoneIK(Transform a, Transform b, Transform c, Vector3 target, Vector3 pole, float weight)
        {
            if (weight <= 0f) return;
            float la = Vector3.Distance(a.position, b.position), lb = Vector3.Distance(b.position, c.position);
            Vector3 toTarget = target - a.position;
            float d = Mathf.Clamp(toTarget.magnitude, 0.01f, (la + lb) * 0.999f);
            Vector3 dir = toTarget.normalized;
            float cosA = Mathf.Clamp((la * la + d * d - lb * lb) / (2f * la * d), -1f, 1f);
            Vector3 toPole = pole - a.position;
            Vector3 perp = (toPole - Vector3.Dot(toPole, dir) * dir);
            if (perp.sqrMagnitude < 1e-6f) perp = Vector3.Cross(dir, Vector3.up);
            perp.Normalize();
            Vector3 elbow = a.position + dir * (la * cosA) + perp * (la * Mathf.Sqrt(1f - cosA * cosA));
            Aim(a, b, elbow - a.position, weight);
            Aim(b, c, a.position + dir * d - b.position, weight);
        }

        /// <summary>Gira <paramref name="bone"/> para que apunte hacia <paramref name="dir"/> (dirección hueso→hijo).</summary>
        private static void Aim(Transform bone, Transform child, Vector3 dir, float weight)
        {
            if (bone == null || child == null || weight <= 0f) return;
            Vector3 cur = child.position - bone.position;
            if (cur.sqrMagnitude < 1e-8f || dir.sqrMagnitude < 1e-8f) return;
            Quaternion target = Quaternion.FromToRotation(cur, dir) * bone.rotation;
            bone.rotation = Quaternion.Slerp(bone.rotation, target, Mathf.Clamp01(weight));
        }
    }
}
