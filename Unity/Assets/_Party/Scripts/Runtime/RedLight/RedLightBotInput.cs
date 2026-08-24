using UnityEngine;

namespace Party.RedLight
{
    /// <summary>
    /// Bot policy for Red Light: push for the line on GO, freeze on STOP - imperfectly.
    ///
    /// Bots that froze instantly and perfectly would be unbeatable and joyless to play
    /// against, and they would never give Barnaby anything to react to. So each bot has
    /// its own reaction delay and a small twitch chance: they get caught, they get
    /// called out, and they are part of the show.
    ///
    /// Bot behaviour is per-minigame (HANDOFF.md). This is the first of twelve, and that
    /// cost should weigh on which four get cut.
    /// </summary>
    public sealed class RedLightBotInput : IMoveInput
    {
        readonly Transform _self;
        readonly float _reaction;    // how slow this bot is to stop
        readonly float _twitchOdds;  // chance per stop of creeping anyway
        readonly float _drift;

        RoundPhase _lastPhase = RoundPhase.Waiting;
        float _freezeAt = -1f;
        bool  _twitchingThisStop;

        // Unsticking. Bots drive straight up the lane, so a pillar or a piston pins them
        // and they push into it forever. Observed in play AND headless: four of five
        // eliminated, one survivor wedged against a pillar, and the round never ended.
        float _lastZ = float.MinValue;
        float _stuckSince;
        float _dodgeUntil;
        float _dodgeDir;

        public RedLightBotInput(Transform self)
        {
            _self       = self;
            _reaction   = Random.Range(0.10f, 0.55f);
            _twitchOdds = Random.Range(0.05f, 0.30f);
            _drift      = Random.Range(-0.35f, 0.35f);
        }

        public Vector2 Move
        {
            get
            {
                RedLightDirector d = RedLightDirector.Instance;
                if (d == null || _self == null) return Vector2.zero;

                if (d.phase != _lastPhase)
                {
                    _lastPhase = d.phase;
                    if (d.MustFreeze)
                    {
                        _freezeAt = Time.time + _reaction;
                        _twitchingThisStop = Random.value < _twitchOdds;
                    }
                }

                if (d.phase == RoundPhase.Go)
                {
                    float z = _self.position.z;

                    // Making progress? Reset the stuck timer.
                    if (z > _lastZ + 0.05f) { _lastZ = z; _stuckSince = Time.time; }
                    else if (Time.time - _stuckSince > 0.9f && Time.time > _dodgeUntil)
                    {
                        // Pinned. Commit to a side for a moment and slide off the obstacle.
                        _dodgeDir = Random.value < 0.5f ? -1f : 1f;
                        _dodgeUntil = Time.time + Random.Range(0.5f, 1.1f);
                        _stuckSince = Time.time;
                    }

                    if (Time.time < _dodgeUntil)
                        return Vector2.ClampMagnitude(new Vector2(_dodgeDir, 0.35f), 1f);

                    return Vector2.ClampMagnitude(new Vector2(_drift, 1f), 1f);
                }

                if (d.MustFreeze)
                {
                    // Still coasting to a halt - their reaction time has not elapsed.
                    if (Time.time < _freezeAt)
                        return Vector2.ClampMagnitude(new Vector2(_drift, 1f), 1f) * 0.8f;

                    // Or they simply cannot help themselves.
                    if (_twitchingThisStop && Random.value < 0.02f)
                        return new Vector2(0f, 0.5f);
                }

                return Vector2.zero;
            }
        }
    }
}
