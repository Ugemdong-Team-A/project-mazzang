using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Utility
{
    static Dictionary<float, WaitForSeconds> _waitForSecondsList = new();

    public static WaitForSeconds GetWaitForSeconds(float seconds)
    {
        if (_waitForSecondsList.ContainsKey(seconds))
            return _waitForSecondsList[seconds];

        WaitForSeconds waitForSeconds = new WaitForSeconds(seconds);
        _waitForSecondsList.Add(seconds, waitForSeconds);
        return waitForSeconds;
    }

    public static string ToHex(
        Color color,
        bool includeAlpha = false)
    {
        return includeAlpha
            ? "#" + ColorUtility.ToHtmlStringRGBA(color)
            : "#" + ColorUtility.ToHtmlStringRGB(color);
    }

    public static string Colorize(
        string text,
        Color color,
        bool includeAlpha = false)
    {
        string hex =
            ToHex(color, includeAlpha);

        return $"<color={hex}>{text}</color>";
    }

    public static string DecodingNickname(byte[] token)
    {
        string noName = "No Name";
        if (token == null || token.Length == 0)
            return noName;

        string nickname = Encoding.UTF8.GetString(token).Trim();

        if (string.IsNullOrWhiteSpace(nickname))
            return noName;

        const int maxLength = 16;

        if (nickname.Length > maxLength)
            return nickname.Substring(0, maxLength);

        return nickname;
    }

    public static uint Hash(uint value)
    {
        value ^= value >> 16;
        value *= 0x7FEB352Du;
        value ^= value >> 15;
        value *= 0x846CA68Bu;
        value ^= value >> 16;

        return value;
    }

    public static float To01(uint value)
    {
        return (value & 0x00FFFFFFu) /
               (float)0x00FFFFFFu;
    }
}
