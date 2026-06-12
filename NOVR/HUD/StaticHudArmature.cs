namespace NOVR.HUD;



// Always oriented with your aircraft
public class StaticHudArmature : SceneSingleton<StaticHudArmature>
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
