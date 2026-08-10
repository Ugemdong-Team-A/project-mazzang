using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    menuName = "Game/Character Catalog",
    fileName = "CharacterCatalog")]
public sealed class CharacterCatalog : ScriptableObject
{
    [SerializeField]
    private CharacterData[] characters;

    public IReadOnlyList<CharacterData> Characters =>
        characters;

    public bool ContainsId(
        int characterId)
    {
        return TryGetById(
            characterId,
            out _);
    }

    public CharacterData GetById(
        int characterId)
    {
        TryGetById(
            characterId,
            out CharacterData data);

        return data;
    }

    public bool TryGetById(
        int characterId,
        out CharacterData data)
    {
        if (characters != null)
        {
            foreach (CharacterData character
                     in characters)
            {
                if (character == null)
                    continue;

                if (character.CharacterId !=
                    characterId)
                {
                    continue;
                }

                data = character;
                return true;
            }
        }

        data = null;
        return false;
    }

#if UNITY_EDITOR

    private void OnValidate()
    {
        if (characters == null)
            return;

        HashSet<int> ids = new();

        foreach (CharacterData character
                 in characters)
        {
            if (character == null)
                continue;

            if (ids.Add(
                    character.CharacterId))
            {
                continue;
            }

            Debug.LogError(
                $"CharacterCatalog에 중복 CharacterId가 있습니다: " +
                $"{character.CharacterId}",
                this);
        }
    }

#endif
}
