using UnityEngine;

[CreateAssetMenu(fileName = "GameFeedbackAudioLibrary", menuName = "PennyBall/Game Feedback Audio Library")]
public class GameFeedbackAudioLibrary : ScriptableObject
{
    public AudioClip buttonClick;
    public AudioClip kickClip;
    public AudioClip wallHitClip;
    public AudioClip coinHitClip;
    public AudioClip goalCelebrationClip;
    public AudioClip enemyGoalCelebrationClip;
    public AudioClip finding;
    public AudioClip backgroundMusicClip;
    public AudioClip horn;
    public AudioClip whistle;
    public AudioClip mainTheme;
    public AudioClip Count1;
    public AudioClip Count2;
    public AudioClip Count3;
}
