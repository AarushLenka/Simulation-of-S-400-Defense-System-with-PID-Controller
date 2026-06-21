/// <summary>
/// Identifies which physical interceptor variant a MissileController represents.
/// Used by InterceptorConfig and FireControlSystem for routing decisions.
/// </summary>
public enum MissileType
{
    Standard_48N6DM,   // long-range — ballistic missiles, stealth fighters, bombers
    Agile_9M96E        // short-range high-agility — drones and cruise missiles
}
