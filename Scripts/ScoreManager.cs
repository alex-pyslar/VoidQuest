using Godot;

namespace VoidQuest;

public partial class ScoreManager : Node
{
    public int Score { get; private set; } = 0;

    [Signal]
    public delegate void ScoreChangedEventHandler(int newScore);

    public void Add(int points)
    {
        Score += points;
        EmitSignal(SignalName.ScoreChanged, Score);
    }

    public void Reset()
    {
        Score = 0;
        EmitSignal(SignalName.ScoreChanged, Score);
    }
}
