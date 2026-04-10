using UnityEngine;

[DisallowMultipleComponent]
public sealed class BattleSessionManager : SingletonBehaviour<BattleSessionManager>
{
    private BattleStartPayload _payload;

    public bool HasPayload => _payload != null;

    public void StorePayload(BattleStartPayload payload)
    {
        _payload = payload;
    }

    public bool TryGetPayload(out BattleStartPayload payload)
    {
        payload = _payload;
        return payload != null;
    }

    public void ClearPayload()
    {
        _payload = null;
    }
}
