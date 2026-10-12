using System;
using System.Collections;
using UnityEngine;

namespace VehicleSystem
{
    public enum PickupType
    {
        Battery,    // +1 battery for BOOST
        RepairKit   // +1 manual recovery (teleport to the last safe place, restores some health)
    }

    /// <summary>
    /// Something on the road the player drives into. Needs a trigger collider (added for you). Batteries and repair kits
    /// belong to the current match only: nothing is saved to the profile.
    ///
    /// A pickup is only collected when the vehicle has room for it (carry limit). It then disappears, or comes back after
    /// "Respawn Time" seconds (handy for testing; leave 0 on the road, where the road generator removes it).
    ///
    /// "Local Player Only": in multiplayer every phone has its own copy of the road's pickups, so only the vehicle the
    /// local player drives may take them (not remote players' vehicles or bots).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Pickup : MonoBehaviour
    {
        public PickupType type = PickupType.Battery;
        [Min(1)] public int amount = 1;
        [Tooltip("Only the local player's vehicle can collect it.")]
        public bool localPlayerOnly = true;
        [Tooltip("Seconds until it comes back after being collected. 0 = it is gone for good.")]
        public float respawnTime = 0f;

        [Header("Look")]
        [Tooltip("The model. It spins and bobs, and is hidden while the pickup is collected.")]
        public Transform visual;
        public float spinSpeed = 90f;
        public float bobHeight = 0.15f;
        public float bobSpeed = 2.5f;

        /// <summary>(pickup, the vehicle that took it): effects, sounds and later scoring hook in here.</summary>
        public event Action<Pickup, VehicleController> Collected;

        Collider trigger;
        Vector3 visualStart;
        bool available = true;

        void Reset()
        {
            var c = GetComponent<Collider>();
            if (c != null) c.isTrigger = true;
        }

        void Awake()
        {
            trigger = GetComponent<Collider>();
            trigger.isTrigger = true;
            if (visual != null) visualStart = visual.localPosition;
        }

        void Update()
        {
            if (!available || visual == null) return;

            visual.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);
            visual.localPosition = visualStart + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
        }

        void OnTriggerEnter(Collider other)
        {
            if (!available) return;

            Rigidbody body = other.attachedRigidbody;
            if (body == null || !body.TryGetComponent(out VehicleController car)) return;

            if (localPlayerOnly && !(car.TryGetComponent(out VehicleDriver driver) && driver.Source is LocalPlayerInput)) return;
            if (!TryGive(car)) return;

            Collected?.Invoke(this, car);
            StartCoroutine(Hide());
        }

        bool TryGive(VehicleController car)
        {
            switch (type)
            {
                case PickupType.Battery:
                    return car.TryGetComponent(out VehicleBoost boost) && boost.AddBattery(amount);
                case PickupType.RepairKit:
                    return car.TryGetComponent(out VehicleRecovery recovery) && recovery.AddManualUse(amount);
                default:
                    return false;
            }
        }

        IEnumerator Hide()
        {
            available = false;
            trigger.enabled = false;
            if (visual != null) visual.gameObject.SetActive(false);

            if (respawnTime <= 0f)
            {
                gameObject.SetActive(false);
                yield break;
            }

            yield return new WaitForSeconds(respawnTime);

            if (visual != null) { visual.gameObject.SetActive(true); visual.localPosition = visualStart; }
            trigger.enabled = true;
            available = true;
        }
    }
}
