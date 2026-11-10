using TMPro;

namespace TownOfHost;

/// <summary>プレイヤーの名前表示(TextMeshPro)を取り出す(GuessManager.cs から移動)</summary>
public static class NameTextHelper
{
    public static TextMeshPro NameText(this PlayerControl p) => p.cosmetics.nameText;
    public static TextMeshPro NameText(this PoolablePlayer p) => p.cosmetics.nameText;
}
