using System;

[Serializable]
public readonly struct BattleTeamId : IEquatable<BattleTeamId>
{
    public int Value { get; }

    public BattleTeamId(int value)
    {
        Value = value;
    }

    public bool Equals(BattleTeamId other) => Value == other.Value;

    public override bool Equals(object obj) => obj is BattleTeamId other && Equals(other);

    public override int GetHashCode() => Value;

    public override string ToString() => Value.ToString();

    public static bool operator ==(BattleTeamId left, BattleTeamId right) => left.Equals(right);

    public static bool operator !=(BattleTeamId left, BattleTeamId right) => !left.Equals(right);
}

public static class BattleTeamIds
{
    public static readonly BattleTeamId Player = new BattleTeamId(1);

    public static readonly BattleTeamId Enemy = new BattleTeamId(2);
}
