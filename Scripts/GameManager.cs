using Godot;

namespace VoidQuest;

public enum GameState { Menu, Playing, Paused, GameOver }

/// <summary>
/// Global game state manager. Autoloaded singleton.
/// </summary>
public partial class GameManager : Node
{
    public static GameManager Instance { get; private set; }
    public GameState State { get; private set; } = GameState.Menu;

    [Signal] public delegate void GameStateChangedEventHandler(int newState);
    [Signal] public delegate void GameOverEventHandler(int finalScore);

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        DataLoader.EnsureLoaded();
    }

    public void StartGame()
    {
        GetNode<ScoreManager>("/root/ScoreManager")?.Reset();
        SetState(GameState.Playing);
        GetTree().ChangeSceneToFile("res://Scenes/World.tscn");
    }

    public void TogglePause()
    {
        if (State == GameState.Playing)
            SetState(GameState.Paused);
        else if (State == GameState.Paused)
            SetState(GameState.Playing);
    }

    public void OnPlayerDied()
    {
        int score = GetNode<ScoreManager>("/root/ScoreManager")?.Score ?? 0;
        SetState(GameState.GameOver);
        EmitSignal(SignalName.GameOver, score);
    }

    public void ReturnToMenu()
    {
        GetTree().Paused = false;
        SetState(GameState.Menu);
        GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
    }

    public void SetMenuState() => SetState(GameState.Menu);

    private void SetState(GameState newState)
    {
        State = newState;
        GetTree().Paused = (newState == GameState.Paused);

        Input.MouseMode = newState == GameState.Playing
            ? Input.MouseModeEnum.Captured
            : Input.MouseModeEnum.Visible;

        EmitSignal(SignalName.GameStateChanged, (int)newState);
    }
}
