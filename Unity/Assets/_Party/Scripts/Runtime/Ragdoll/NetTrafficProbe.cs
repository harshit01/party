using Mirror;
using UnityEngine;

namespace Party.Ragdoll
{
    /// <summary>
    /// Measures ACTUAL bytes on the wire, via Mirror's own diagnostics.
    ///
    /// The netcode cost of the ragdoll has been the standing unknown: one synced transform
    /// per player became ten, and "80 rigidbodies over Steam P2P" was an arithmetic guess
    /// rather than a number. Guessing at bandwidth is how you discover the problem in a
    /// playtest with four friends waiting.
    ///
    /// NetworkDiagnostics.OutMessageEvent fires for every outgoing message with its real
    /// serialised size, so this is measured rather than modelled.
    /// </summary>
    public class NetTrafficProbe : MonoBehaviour
    {
        long _bytes;
        int  _messages;
        float _started = -1f;
        float _nextReport;

        bool _hooked;

        void OnDisable()
        {
            if (_hooked) NetworkDiagnostics.OutMessageEvent -= OnOut;
            _hooked = false;
        }

        /// <summary>
        /// SUBSCRIBE AFTER THE SERVER IS UP, not in OnEnable.
        ///
        /// Mirror's ResetStatics() sets OutMessageEvent = null, and it runs when the server
        /// starts - so a handler attached at scene load is silently unhooked before a single
        /// byte flows. It reported a confident 0.0 KB/s with eight ragdoll players
        /// connected, which looks exactly like "the ragdoll is free" and is not.
        /// </summary>
        void Hook()
        {
            if (_hooked) return;
            NetworkDiagnostics.OutMessageEvent += OnOut;
            _hooked = true;
        }

        void OnOut(NetworkDiagnostics.MessageInfo info)
        {
            // Ignore everything before the world is actually up, or the spawn burst
            // dominates the average and flatters the steady-state number.
            if (_started < 0f) return;
            _bytes += info.bytes;
            _messages++;
        }

        void Update()
        {
            if (!NetworkServer.active) return;

            Hook();

            if (_started < 0f && Time.time > 6f)
            {
                _started = Time.time;
                _bytes = 0; _messages = 0;
                Debug.Log("[Net] measuring steady state from here");
            }
            if (_started < 0f || Time.time < _nextReport) return;

            _nextReport = Time.time + 5f;
            float secs = Mathf.Max(Time.time - _started, 0.001f);
            int players = 0;
            foreach (PartyPlayer p in Object.FindObjectsByType<PartyPlayer>(FindObjectsSortMode.None))
                players++;

            float kbs = _bytes / secs / 1024f;
            Debug.Log($"[Net] players={players} bodies_synced={CountSyncedTransforms()} " +
                      $"out={kbs:F1} KB/s msgs={_messages / secs:F0}/s " +
                      $"per_player={(players > 0 ? kbs / players : 0f):F2} KB/s");
        }

        static int CountSyncedTransforms()
        {
            int n = 0;
            foreach (NetworkTransformBase t in
                     Object.FindObjectsByType<NetworkTransformBase>(FindObjectsSortMode.None)) n++;
            return n;
        }
    }
}
