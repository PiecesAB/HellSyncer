using Blastula.Sounds;
using Blastula.VirtualVariables;
using Godot;

namespace HellSyncer
{
    /// <summary>
    /// Plays a synced music track with the node name/path determined by the Blastula.Sounds.MusicManager node.
    /// </summary>
    /// <remarks>
    /// Only one synced music track can play at a time.
    /// </remarks>
    [GlobalClass]
    [Icon(Blastula.VirtualVariables.Persistent.NODE_ICON_PATH + "/wolfDark.png")]
    public partial class PlaySyncedMusic : Blastula.Operations.Discrete
	{
        [Export] public string nodeName = "";
        [Export] public string volume = "1";

        public override void Run()
        {
            float volSolved = 0.5f;
            if (volume != null && volume != "")
            {
                volSolved = Solve("volume").AsSingle();
            }
            SyncedMusicManager.PlayImmediate(nodeName);
            if (MusicManager.main != null)
            {
                MusicManager.SetVolumeMultiplier(volSolved);
            }
        }
    }
}
