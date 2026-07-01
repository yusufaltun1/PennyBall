using UnityEngine;

[CreateAssetMenu(fileName = "GameFeedbackAudioLibrary", menuName = "PennyBall/Game Feedback Audio Library")]
public class GameFeedbackAudioLibrary : ScriptableObject
{
    public AudioClip kickClip;
    public AudioClip wallHitClip;
    public AudioClip coinHitClip;
    public AudioClip goalCelebrationClip;
    public AudioClip enemyGoalCelebrationClip;
    public AudioClip backgroundMusicClip;
}
