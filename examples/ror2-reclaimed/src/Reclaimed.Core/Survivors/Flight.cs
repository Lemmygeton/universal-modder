using System;
using System.Runtime.CompilerServices;
using EntityStates;
using RoR2;
using UnityEngine;

namespace Reclaimed.Core.Survivors
{
    /// <summary>
    /// Flight/hover parameters on a body using <see cref="MotorKind.Flying"/> or <see cref="MotorKind.Hover"/>.
    /// On start it grants CharacterMotor flight and anti-gravity through the motor's flight/gravity parameter
    /// providers (the mechanism the game uses for its own flight effects). If that API is unavailable it
    /// falls back to driving velocity directly each tick (<see cref="UsingNativeFlight"/> is false).
    /// </summary>
    public sealed class ReclaimedFlightController : MonoBehaviour
    {
        public MotorKind mode = MotorKind.Flying;
        public float hoverHeight = 3f;
        /// <summary>Extra height reachable by holding jump while hovering.</summary>
        public float hoverRiseHeight = 6f;
        public float verticalSpeedCoefficient = 0.8f;
        /// <summary>0 = forward input stays level, 1 = forward follows the camera pitch.</summary>
        public float pitchInfluence = 1f;

        public bool UsingNativeFlight { get; private set; }
        private CharacterMotor motor;
        private bool granted;

        private void Start()
        {
            motor = GetComponent<CharacterMotor>();
            if (motor == null) return;
            try
            {
                UsingNativeFlight = GrantFlight(motor, 1);
                granted = true;
            }
            catch (Exception e)
            {
                UsingNativeFlight = false;
                Log.WarnOnce("flight.native", "CharacterMotor flight parameters unavailable (" + e.GetType().Name + "); using manual flight.");
            }
        }

        private void OnDestroy()
        {
            if (!granted || motor == null) return;
            try
            {
                GrantFlight(motor, -1);
            }
            catch (Exception)
            {
                // Same failure as in Start; nothing to undo.
            }
        }

        // Separate, non-inlined method: if the game's flight API changed, the MissingMemberException is thrown
        // when this method is compiled and is caught by the caller instead of breaking Start.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool GrantFlight(CharacterMotor motor, int delta)
        {
            ICharacterFlightParameterProvider flight = motor;
            CharacterFlightParameters fp = flight.flightParameters;
            fp.channeledFlightGranterCount += delta;
            flight.flightParameters = fp;
            ICharacterGravityParameterProvider gravity = motor;
            CharacterGravityParameters gp = gravity.gravityParameters;
            gp.channeledAntiGravityGranterCount += delta;
            gravity.gravityParameters = gp;
            return flight.isFlying;
        }

        /// <summary>
        /// The 3D move direction for this tick from input: forward follows the aim pitch, jump climbs; in Hover
        /// mode vertical motion holds the hover height instead.
        /// </summary>
        public Vector3 ComputeMoveDirection(InputBankTest input)
        {
            if (input == null) return Vector3.zero;
            Vector3 move = input.moveVector;
            Vector3 aim = input.aimDirection;
            Vector3 flatAim = new Vector3(aim.x, 0f, aim.z);
            if (flatAim.sqrMagnitude < 0.0001f) flatAim = transform.forward;
            flatAim.Normalize();
            float forward = Vector3.Dot(move, flatAim);
            Vector3 lateral = move - flatAim * forward;
            Vector3 pitched = Vector3.Lerp(flatAim, aim.normalized, pitchInfluence);
            Vector3 dir = lateral + pitched * forward;
            bool climb = input.jump.down;

            if (mode == MotorKind.Hover)
            {
                dir.y = 0f;
                float ground;
                float targetHeight = hoverHeight + (climb ? hoverRiseHeight : 0f);
                if (TryGroundHeight(out ground))
                {
                    float error = ground + targetHeight - transform.position.y;
                    dir.y = Mathf.Clamp(error * 0.5f, -1f, 1f) * verticalSpeedCoefficient;
                }
                else dir.y = -0.5f * verticalSpeedCoefficient;
            }
            else if (climb) dir.y += verticalSpeedCoefficient;
            return dir;
        }

        private bool TryGroundHeight(out float height)
        {
            RaycastHit hit;
            float probe = hoverHeight + hoverRiseHeight + 20f;
            if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out hit, probe, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
            {
                height = hit.point.y;
                return true;
            }
            height = 0f;
            return false;
        }
    }

    /// <summary>
    /// Body main state for flying and hovering survivors: GenericCharacterMain with a 3D move direction from
    /// <see cref="ReclaimedFlightController"/>; jump is used to climb instead of jumping.
    /// </summary>
    public class ReclaimedFlyingMain : GenericCharacterMain
    {
        private ReclaimedFlightController flight;

        public override void OnEnter()
        {
            base.OnEnter();
            flight = GetComponent<ReclaimedFlightController>();
        }

        // Second guard only: the live game also has ProcessJump(bool), which the compile refs cannot override.
        public override void ProcessJump()
        {
            if (flight == null) base.ProcessJump();
        }

        private static bool canClearJumpInput = true;

        public override void HandleMovements()
        {
            // Jump climbs (read from inputBank.jump), so no jump overload may see the press.
            if (flight != null && canClearJumpInput)
            {
                try
                {
                    ClearJumpInput(this);
                }
                catch (MissingMemberException)
                {
                    canClearJumpInput = false;
                    Log.WarnOnce("flight.jumpinput", "GenericCharacterMain.jumpInputReceived is missing; flying bodies rely on the ProcessJump override.");
                }
            }
            base.HandleMovements();
            if (!isAuthority || flight == null || characterMotor == null || inputBank == null) return;
            Vector3 dir = flight.ComputeMoveDirection(inputBank);
            characterMotor.moveDirection = dir;
            if (!flight.UsingNativeFlight)
            {
                // Fallback: steer velocity ourselves and cancel gravity.
                Vector3 target = dir * characterMotor.walkSpeed;
                characterMotor.velocity = Vector3.MoveTowards(characterMotor.velocity, target, characterMotor.acceleration * Time.fixedDeltaTime);
            }
        }

        // Non-inlined so a missing field throws here, inside the caller's try.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ClearJumpInput(GenericCharacterMain state)
        {
            state.jumpInputReceived = false;
        }
    }
}
