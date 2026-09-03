using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Party.Ragdoll
{
    /// <summary>
    /// Replicates a whole ragdoll in ONE message.
    ///
    /// WHY NOT TEN NetworkTransforms. That was the first attempt and it is not merely
    /// wasteful, it does not work: Mirror indexes NetworkBehaviours by their component
    /// order at spawn, so components ADDED AT RUNTIME break serialisation outright. The
    /// bones are built at runtime, so a per-bone NetworkTransform cannot be on the prefab
    /// either. Measured, that attempt threw 128,448 NullReferenceExceptions from
    /// NetworkTransformReliable.Update in a thirty second run.
    ///
    /// One component that writes every bone is also much cheaper on the wire: ten messages
    /// with ten headers become one, and rotations compress to four bytes each.
    ///
    /// HOST-AUTHORITATIVE, like everything else here: the server simulates the physics and
    /// clients are told where the bones ended up. Clients never simulate their own ragdoll,
    /// so there is nothing to diverge.
    /// </summary>
    public class RagdollSync : NetworkBehaviour
    {
        [Tooltip("Snapshots per second. Ragdoll poses tolerate a lower rate than a player capsule.")]
        public float rate = 20f;

        readonly List<Transform> _bones = new List<Transform>();
        readonly List<Vector3> _pos = new List<Vector3>();
        readonly List<Quaternion> _rot = new List<Quaternion>();
        double _nextSend;

        public void Bind(RagdollBuilder.Rig rig)
        {
            _bones.Clear();
            // Fixed order on both sides - a dictionary's order is not a contract.
            foreach (Bone b in System.Enum.GetValues(typeof(Bone)))
            {
                Rigidbody rb = rig.Get(b);
                if (rb != null) _bones.Add(rb.transform);
            }
            _pos.Clear(); _rot.Clear();
            for (int i = 0; i < _bones.Count; i++) { _pos.Add(Vector3.zero); _rot.Add(Quaternion.identity); }
            syncInterval = 1f / Mathf.Max(rate, 1f);
        }

        void Update()
        {
            if (isServer && NetworkTime.time >= _nextSend)
            {
                _nextSend = NetworkTime.time + 1f / Mathf.Max(rate, 1f);
                SetDirty();
                return;
            }

            if (isServer || _bones.Count == 0) return;

            // Clients do not simulate - they are told. Interpolating toward the last
            // snapshot keeps it smooth at 20 Hz without any local physics to disagree with.
            for (int i = 0; i < _bones.Count; i++)
            {
                if (_bones[i] == null) continue;
                _bones[i].localPosition = Vector3.Lerp(_bones[i].localPosition, _pos[i], Time.deltaTime * 18f);
                _bones[i].localRotation = Quaternion.Slerp(_bones[i].localRotation, _rot[i], Time.deltaTime * 18f);
            }
        }

        public override void OnSerialize(NetworkWriter writer, bool initialState)
        {
            base.OnSerialize(writer, initialState);
            writer.WriteByte((byte)_bones.Count);
            for (int i = 0; i < _bones.Count; i++)
            {
                Transform t = _bones[i];
                writer.WriteVector3(t != null ? t.localPosition : Vector3.zero);
                // Four bytes instead of sixteen. A ragdoll bone does not need full precision.
                writer.WriteUInt(Compression.CompressQuaternion(t != null ? t.localRotation : Quaternion.identity));
            }
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            base.OnDeserialize(reader, initialState);
            int n = reader.ReadByte();
            for (int i = 0; i < n; i++)
            {
                Vector3 p = reader.ReadVector3();
                Quaternion r = Compression.DecompressQuaternion(reader.ReadUInt());
                if (i < _pos.Count) { _pos[i] = p; _rot[i] = r; }
            }
        }
    }
}
