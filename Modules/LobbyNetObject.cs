using System.Collections.Generic;
using System.Linq;
using AmongUs.InnerNet.GameDataMessages;
using Hazel;
using InnerNet;
using UnityEngine;

namespace TownOfHost.Modules;

/// <summary>
/// MODを入れていない人にも見える「物」<br/>
/// 見えないプレイヤーを1人出して、その名前の部分に絵(文字)を表示する。位置はホストが動かす<br/>
/// EndlessHostRoles の CustomNetObject (GPL-3.0) を参考にしています。<br/>
/// https://github.com/Gurge44/EndlessHostRoles/blob/main/Modules/CustomNetObject.cs<br/>
/// (元は Rabek009 さんの MoreGamemodes のコード https://github.com/Rabek009/MoreGamemodes)
/// </summary>
public class LobbyNetObject
{
    public static readonly List<LobbyNetObject> All = new();

    public PlayerControl playerControl;
    public Vector2 Position;
    string sprite;
    /// <summary>出し終わって、動かせる状態か</summary>
    public bool Ready { get; private set; }
    bool despawned;

    /// <summary>出す(ホストだけ)。少したってから Ready になる</summary>
    public LobbyNetObject(string sprite, Vector2 position)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        this.sprite = sprite;
        Position = position;
        All.Add(this);
        try
        {
            playerControl = Object.Instantiate(AmongUsClient.Instance.PlayerPrefab, Vector2.zero, Quaternion.identity);
            playerControl.PlayerId = 254;
            playerControl.isNew = false;
            playerControl.notRealPlayer = true;
            try { playerControl.NetTransform.SnapTo(new Vector2(50f, 50f)); } catch { }

            AmongUsClient.Instance.NetIdCnt += 1U;
            var msg = MessageWriter.Get(SendOption.Reliable);
            msg.StartMessage(5);
            msg.Write(AmongUsClient.Instance.GameId);
            msg.StartMessage(4);
            SpawnGameDataMessage item = AmongUsClient.Instance.CreateSpawnMessage(playerControl, -2, SpawnFlags.None);
            item.SerializeValues(msg);
            msg.EndMessage();
            //バニラのサーバーでは、部品の分も送る
            if (Utils.IsRestriction())
            {
                for (uint i = 1; i <= 3; ++i)
                {
                    msg.StartMessage(4);
                    msg.WritePacked(2U);
                    msg.WritePacked(-2);
                    msg.Write((byte)SpawnFlags.None);
                    msg.WritePacked(1);
                    msg.WritePacked(AmongUsClient.Instance.NetIdCnt - i);
                    msg.StartMessage(1);
                    msg.EndMessage();
                    msg.EndMessage();
                }
            }
            msg.EndMessage();
            AmongUsClient.Instance.SendOrDisconnect(msg);
            msg.Recycle();

            if (PlayerControl.AllPlayerControls.Contains(playerControl))
                PlayerControl.AllPlayerControls.Remove(playerControl);
            try { playerControl.cosmetics.currentBodySprite.BodySprite.color = Color.clear; } catch { }
            try { playerControl.cosmetics.colorBlindText.color = Color.clear; } catch { }
        }
        catch (System.Exception ex)
        {
            Logger.Error(ex.ToString(), "LobbyNetObject");
            All.Remove(this);
            return;
        }

        _ = new LateTask(SendFakeKill, 0.1f, "LobbyNetObject.Spawn", true);
    }

    /// <summary>ほかの人の画面で「プレイヤー」として扱われないようにする</summary>
    void SendFakeKill()
    {
        if (despawned || !playerControl) return;
        try
        {
            var others = PlayerCatch.AllPlayerControls.Where(pc => pc && !pc.AmOwner && pc.PlayerId < 254).ToList();
            if (others.Count > 0)
            {
                var messages = 0;
                var stream = MessageWriter.Get(SendOption.Reliable);
                stream.StartMessage(26);
                stream.WritePacked(AmongUsClient.Instance.GameId);
                foreach (var pc in others)
                {
                    if (stream.Length > 500 || messages + 3 > AmongUsClient.Instance.GetMaxMessagePackingLimit())
                    {
                        stream.EndMessage();
                        AmongUsClient.Instance.SendOrDisconnect(stream);
                        messages = 0;
                        stream.Clear(SendOption.Reliable);
                        stream.StartMessage(26);
                        stream.WritePacked(AmongUsClient.Instance.GameId);
                    }
                    stream.StartMessage(6);
                    stream.Write(AmongUsClient.Instance.GameId);
                    stream.WritePacked(pc.OwnerId);
                    stream.StartMessage(1);
                    stream.WritePacked(playerControl.NetId);
                    stream.Write(pc.PlayerId);
                    stream.EndMessage();
                    stream.StartMessage(2);
                    stream.WritePacked(playerControl.NetId);
                    stream.Write((byte)RpcCalls.MurderPlayer);
                    stream.WriteNetObject(playerControl);
                    stream.Write((int)MurderResultFlags.FailedError);
                    stream.EndMessage();
                    stream.StartMessage(1);
                    stream.WritePacked(playerControl.NetId);
                    stream.Write((byte)254);
                    stream.EndMessage();
                    stream.EndMessage();
                    messages += 3;
                }
                stream.EndMessage();
                AmongUsClient.Instance.SendOrDisconnect(stream);
                stream.Recycle();
            }
            playerControl.CachedPlayerData = PlayerControl.LocalPlayer.Data;
        }
        catch (System.Exception ex) { Logger.Error(ex.ToString(), "LobbyNetObject"); }

        _ = new LateTask(() =>
        {
            if (despawned || !playerControl) return;
            SetSprite(sprite);
            Ready = true;
        }, 0.15f, "LobbyNetObject.SetSprite", true);
    }

    /// <summary>
    /// 絵を変える<br/>
    /// ホストの見た目を一瞬だけ絵に変えて、見えないプレイヤーをホストに変身させる
    /// </summary>
    public void SetSprite(string newSprite)
    {
        if (!AmongUsClient.Instance.AmHost || despawned || !playerControl) return;
        sprite = newSprite;
        var host = PlayerControl.LocalPlayer;
        var outfit = host.Data.Outfits[PlayerOutfitType.Default];
        var name = outfit.PlayerName;
        var colorId = outfit.ColorId;
        var hatId = outfit.HatId;
        var skinId = outfit.SkinId;
        var petId = outfit.PetId;
        var visorId = outfit.VisorId;
        try
        {
            var w = MessageWriter.Get(SendOption.Reliable);
            w.StartMessage(5);
            w.Write(AmongUsClient.Instance.GameId);

            outfit.PlayerName = "<size=14><br></size>" + newSprite;
            outfit.ColorId = 0;
            outfit.HatId = "";
            outfit.SkinId = "";
            outfit.PetId = "";
            outfit.VisorId = "";
            w.StartMessage(1);
            w.WritePacked(host.Data.NetId);
            host.Data.Serialize(w, false);
            w.EndMessage();

            try { playerControl.Shapeshift(host, false); } catch { }
            w.StartMessage(2);
            w.WritePacked(playerControl.NetId);
            w.Write((byte)RpcCalls.Shapeshift);
            w.WriteNetObject(host);
            w.Write(false);
            w.EndMessage();

            outfit.PlayerName = name;
            outfit.ColorId = colorId;
            outfit.HatId = hatId;
            outfit.SkinId = skinId;
            outfit.PetId = petId;
            outfit.VisorId = visorId;
            w.StartMessage(1);
            w.WritePacked(host.Data.NetId);
            host.Data.Serialize(w, false);
            w.EndMessage();

            try { playerControl.NetTransform.SnapTo(Position); } catch { }
            w.StartMessage(2);
            w.WritePacked(playerControl.NetTransform.NetId);
            w.Write((byte)RpcCalls.SnapTo);
            NetHelpers.WriteVector2(Position, w);
            w.Write(playerControl.NetTransform.lastSequenceId);
            w.EndMessage();

            w.EndMessage();
            AmongUsClient.Instance.SendOrDisconnect(w);
            w.Recycle();
        }
        catch (System.Exception ex)
        {
            outfit.PlayerName = name;
            outfit.ColorId = colorId;
            outfit.HatId = hatId;
            outfit.SkinId = skinId;
            outfit.PetId = petId;
            outfit.VisorId = visorId;
            Logger.Error(ex.ToString(), "LobbyNetObject");
        }
        //ホストの名前の上の表示(ランクなど)が消えるので、送り直す
        RequestHostNameRefresh();
    }

    static bool refreshQueued;
    static void RequestHostNameRefresh()
    {
        if (refreshQueued) return;
        refreshQueued = true;
        _ = new LateTask(() =>
        {
            refreshQueued = false;
            if (GameStates.IsLobby && !GameStates.IsCountDown) Utils.ApplySuffix(null, force: true);
        }, 0.4f, "LobbyNetObject.RefreshName", true);
    }

    /// <summary>今の位置を全員に送る</summary>
    public void SendPosition()
    {
        if (!AmongUsClient.Instance.AmHost || despawned || !playerControl || !Ready) return;
        try
        {
            var net = playerControl.NetTransform;
            try { net.SnapTo(Position, (ushort)(net.lastSequenceId + 1U)); } catch { }
            var num = (ushort)(net.lastSequenceId + 2U);
            var w = AmongUsClient.Instance.StartRpcImmediately(net.NetId, (byte)RpcCalls.SnapTo, SendOption.None);
            NetHelpers.WriteVector2(Position, w);
            w.Write(num);
            AmongUsClient.Instance.FinishRpcImmediately(w);
        }
        catch { }
    }

    /// <summary>消す</summary>
    public void Despawn()
    {
        if (despawned) return;
        despawned = true;
        Ready = false;
        All.Remove(this);
        if (!AmongUsClient.Instance.AmHost || !playerControl) return;
        try
        {
            var w = MessageWriter.Get(SendOption.Reliable);
            w.StartMessage(5);
            w.Write(AmongUsClient.Instance.GameId);
            w.StartMessage(5);
            w.WritePacked(playerControl.NetId);
            w.EndMessage();
            w.EndMessage();
            AmongUsClient.Instance.SendOrDisconnect(w);
            w.Recycle();
            AmongUsClient.Instance.RemoveNetObject(playerControl);
            Object.Destroy(playerControl.gameObject);
        }
        catch (System.Exception ex) { Logger.Error(ex.ToString(), "LobbyNetObject"); }
    }

    public static void DespawnAll()
    {
        foreach (var obj in All.ToArray()) obj.Despawn();
        All.Clear();
    }
}
