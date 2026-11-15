using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The Bomb power-up: removes every asteroid on screen in a sweep, nearest first, and pays a flat total. The sweep takes time and
// the pickup is gone the moment it's collected, so it runs here on the player.
public class PlayerBomb : MonoBehaviour
{
    private Score score;

    private void Awake()
    {
        score = GameManager.Instance != null ? GameManager.Instance.Score : null;
    }

    private const float ArriveShare = 0.5f;   // a rock goes off by this share of the time it needs to reach the player

    // Removes the targets as the sweep reaches them, then pays for the ones that were still there.
    private IEnumerator Sweep(List<AsteroidBehavior> targets, float pointsPerRock, float sweepTime)
    {
        float playerZ = transform.position.z;

        // The nearest rock goes off at once and the farthest after sweepTime, the rest in proportion to their distance in between.
        // No rock goes off later than ArriveShare of the time it needs to reach the player, so even at top speed it can't hit first.
        // Worked out now, so the rocks moving toward the player during the sweep don't change the order.
        float[] distance = new float[targets.Count];
        float nearest = float.MaxValue, farthest = 0f;
        for (int i = 0; i < distance.Length; i++)
        {
            distance[i] = Mathf.Abs(targets[i].transform.position.z - playerZ);
            nearest = Mathf.Min(nearest, distance[i]);
            farthest = Mathf.Max(farthest, distance[i]);
        }

        float[] due = new float[targets.Count];
        for (int i = 0; i < due.Length; i++)
        {
            due[i] = farthest > nearest ? sweepTime * (distance[i] - nearest) / (farthest - nearest) : 0f;
            float speed = targets[i].ApproachSpeed;
            if (speed > 0f) due[i] = Mathf.Min(due[i], distance[i] / speed * ArriveShare);
        }

        int removed = 0, remaining = targets.Count;
        float elapsed = 0f;
        while (remaining > 0)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] == null || due[i] > elapsed) continue;
                if (targets[i].Bomb()) removed++;
                targets[i] = null;
                remaining--;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (removed > 0 && pointsPerRock > 0f && score != null)
            score.AddBonusPoints(removed * pointsPerRock, transform.position, removed + " DESTROYED", Score.ScoreSource.Asteroids);
    }

    // Called by PowerUp. Shakes the camera and starts the sweep over every asteroid on screen right now.
    public void Detonate(float pointsPerRock, float sweepTime, float shakeDuration, float shakeMagnitude)
    {
        if (AsteroidBehavior.Active.Count == 0) return;

        if (shakeDuration > 0f) CameraShake.Shake(shakeDuration, shakeMagnitude);
        StartCoroutine(Sweep(new List<AsteroidBehavior>(AsteroidBehavior.Active), pointsPerRock, sweepTime));
    }
}
