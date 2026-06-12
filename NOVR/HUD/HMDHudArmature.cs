namespace NOVR.HUD;


// Always oriented with your head
public class HMDHudArmature : SceneSingleton<HMDHudArmature>
{
    protected override void Awake()
    {
        i = this;
    }

    private void OnDestroy()
    {
        if (i == this) i = null;
    }
}
