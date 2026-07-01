using System;
using Godot;

public partial class TrainingData : HBoxContainer
{
    private int _episodeCount = 1;
    private float _time = 0;
    private Label _episodeLabel;
    private Label _timeLabel;
    private Label _actionLabel;
    private Label _xLabel;
    private Label _yLabel;

    public override void _Ready()
    {
        _episodeLabel = GetNode<Label>("Values/EpisodeValue");
        _timeLabel = GetNode<Label>("Values/TimeValue");
        _actionLabel = GetNode<Label>("Values/ActionValue");
        _xLabel = GetNode<Label>("Values/XValue");
        _yLabel = GetNode<Label>("Values/YValue");

        _episodeLabel.Text = $"{_episodeCount}";
        _timeLabel.Text = "0";
        _time = Time.GetTicksMsec() / 1000.0f;

        EventBus.OnBossKilled += () => SetProcess(false);
        EventBus.OnPlayerDied += () => SetProcess(false);

        EventBus.OnRestartEpisode += NewEpisode;
        EventBus.OnActionReceived += UpdateAction;
    }

    public override void _Process(double delta)
    {
        float elapsedTime = Time.GetTicksMsec() / 1000.0f - _time;
        
        if(Engine.GetProcessFrames() % 60 == 0)
        {
            _timeLabel.Text = elapsedTime.ToString("0");
        }
    }

    private void NewEpisode()
    {
        SetProcess(true);
        _episodeCount++;
        _episodeLabel.Text = $"{_episodeCount}";

        _time = Time.GetTicksMsec() / 1000.0f;
        _timeLabel.Text = "0";
        _actionLabel.Text = "None";
        _xLabel.Text = "0.0";
        _yLabel.Text = "0.0";

    }

    private void UpdateAction(AiAction action)
    {
        string type = ((ActionType) action.ActionId).ToString();

        _actionLabel.Text = type;
        _xLabel.Text = action.X.ToString("0.0");
        _yLabel.Text = action.Y.ToString("0.0");
    }
}