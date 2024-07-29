using Blastula.VirtualVariables;
using Godot;
using HellSyncer;

namespace HellSyncer.Mechanisms;

/// <summary>
/// This recontextualizes the seconds of a timeline to actually be synced to the music measure, 
/// which is important for overall stage timings.
/// It will only play once, with no looping.
/// </summary>
public partial class MeasuredTimeline : AnimationPlayer
{
	/// <summary>
	/// I am waiting for a music of this name to be played. For anything else, I won't respond.
	/// </summary>
	[Export] public string musicName = "";
	private enum State { NotStarted, Playing, Stopped }
	private State state = State.NotStarted;

	private bool NameMatches() => SyncedMusicManager.mainSynced.currentMusic?.Name == musicName;
	
	private bool TryToStart()
	{
        if (!NameMatches() || state == State.Playing) 
		{
			state = State.Stopped;
			return false; 
		}
		state = State.Playing;
		return true;
	}

	private Callable tryToStartCallable;
	private bool isHooked = false;

    public override void _Ready()
	{
		Pause();
		if (!TryToStart())
		{
			isHooked = true;
			SyncedMusicManager.mainSynced.Connect(
				SyncedMusicManager.SignalName.MusicChangeComplete,
				tryToStartCallable = new Callable(this, MethodName.TryToStart)
			);
		}
	}

    public override void _ExitTree()
    {
        base._ExitTree();
        if (isHooked)
        {
            isHooked = false;
            SyncedMusicManager.mainSynced.Disconnect(
                SyncedMusicManager.SignalName.MusicChangeComplete,
                tryToStartCallable
            );
        }
    }

    public override void _Process(double delta)
	{
		if (Session.main.paused || state != State.Playing) { Pause(); return; }
		float targetTime = SyncedMusicManager.momentaryStreamHead.GetCurrentMeasure()
            + (float)SyncedMusicManager.momentaryStreamHead.GetMeasureProgress();
        Pause();
        if (CurrentAnimationPosition < targetTime)
		{
			Advance(targetTime - CurrentAnimationPosition);
		}
	}
}
