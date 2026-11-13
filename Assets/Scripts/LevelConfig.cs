using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelConfig", menuName = "Return of the Hamster/Level Config")]
public class LevelConfig : ScriptableObject
{
    [Header("Timing")]
    [Tooltip("Seconds the level takes if the player's Speed stays at 0.")]
    public float levelTime = 180f;

    [Header("Finish Planet")]
    [Tooltip("The material on the planet that arrives at the end of the level.")]
    public Material planetMaterial;
    [Tooltip("Where the planet starts, in the Planet Camera's local space. The level moves it toward the camera from here.")]
    public Vector3 planetPosition = new Vector3(0f, -1f, 990f);
    [Tooltip("The planet's rotation in degrees. Changes which side faces the player.")]
    public Vector3 planetRotation = new Vector3(0f, 300f, 0f);
    [Tooltip("The planet's size on each axis. 100 on all three is a round planet.")]
    public Vector3 planetScale = new Vector3(100f, 100f, 100f);

    [Header("Waves")]
    [Tooltip("The waves this level plays, in order. Each has a pattern (the prefab to spawn) and a delayAfter.")]
    public List<ObjectSpawner.Wave> waves = new List<ObjectSpawner.Wave>();
    [Tooltip("Sequential plays the list top to bottom. Random shuffles it each time through.")]
    public ObjectSpawner.WaveOrder order = ObjectSpawner.WaveOrder.Sequential;
    [Tooltip("Start the list over after the last wave. If off, it plays through once and stops.")]
    public bool loop = true;
    [Tooltip("Seconds before the first wave.")]
    public float startDelay = 1f;
}
