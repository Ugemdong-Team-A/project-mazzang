using System;
using Fusion;

public static class PlayerNicknamePolicy
{
    public const int MinLength = 2;
    public const int MaxLength = 16;

    public static bool TryNormalize(
        string input,
        out string nickname)
    {
        nickname = input?.Trim();

        if (string.IsNullOrWhiteSpace(nickname))
            return false;

        if (nickname.Length < MinLength ||
            nickname.Length > MaxLength)
        {
            return false;
        }

        foreach (char c in nickname)
        {
            if (char.IsControl(c))
                return false;
        }

        return true;
    }

    public static string CreateFallback(
        PlayerRef player)
    {
        return $"Player{player.PlayerId + 1}";
    }

    public static string ClampForSuffix(
        string nickname,
        string suffix)
    {
        int allowedLength =
            MaxLength - suffix.Length;

        if (nickname.Length <= allowedLength)
            return nickname;

        return nickname.Substring(
            0,
            Math.Max(1, allowedLength));
    }
}