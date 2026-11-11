using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TownOfHost.Modules;

/// <summary>
/// ロビーの雪玉遊び(ホストが「/snow」でオン・オフ)<br/>
/// ・雪玉にぶつかると、歩いた向きに転がる。転がるほど大きくなり、大きいほど遅い<br/>
/// ・転がっている雪玉が人に当たると、その人が吹き飛ぶ(大きいほど遠く)。当たった雪玉は砕けて、少したつとまた出てくる<br/>
/// ・雪玉はMODを入れていない人にも見える(<see cref="LobbyNetObject"/>)
/// </summary>
public static class SnowballManager
{
    // ---- 調整用の値 ----
    /// <summary>同時に出す雪玉の数(初期値)</summary>
    const int DefaultCount = 3;
    /// <summary>大きさの段階ごとの、絵の大きさ(%)</summary>
    static readonly int[] SpriteScale = [60, 80, 100, 125];
    /// <summary>大きさの段階ごとの、当たり判定の半径</summary>
    static readonly float[] Radius = [0.25f, 0.33f, 0.42f, 0.52f];
    /// <summary>大きさの段階ごとの、押したときの速さ(大きいほど遅い)</summary>
    static readonly float[] PushSpeed = [6f, 4.5f, 3.2f, 2.2f];
    /// <summary>大きさの段階ごとの、吹き飛ばす距離</summary>
    static readonly float[] KnockDistance = [1.0f, 1.6f, 2.3f, 3.0f];
    /// <summary>この距離を転がると1段階大きくなる</summary>
    const float GrowDistance = 2.5f;
    /// <summary>1秒あたりに遅くなる量</summary>
    const float Friction = 2.5f;
    /// <summary>この速さより速いと、人に当たったときに吹き飛ばす</summary>
    const float HitSpeed = 1.2f;
    /// <summary>プレイヤーの当たり判定の半径</summary>
    const float PlayerRadius = 0.35f;
    /// <summary>砕けてからまた出てくるまでの秒数</summary>
    const float RespawnTime = 5f;

    class Snowball
    {
        public LobbyNetObject Obj;
        public int Level;
        public Vector2 Velocity;
        public float Rolled;
        public byte LastPusher = byte.MaxValue;
        public float LastPushTime;
        public int Frame;
    }

    public static bool Enabled { get; private set; }
    static int count = DefaultCount;
    static readonly List<Snowball> balls = new();
    /// <summary>次に雪玉を足すまでの秒数</summary>
    static float spawnTimer;

    /// <summary>雪玉の絵(丸を色付きの四角で描く)</summary>
    static string Sprite(int level)
    {
        const int n = 8;
        var sb = new StringBuilder($"<size={SpriteScale[level]}%><line-height=97%><cspace=0.16em>");
        for (var y = 0; y < n; y++)
        {
            if (y > 0) sb.Append('\n');
            for (var x = 0; x < n; x++)
            {
                var dx = x - 3.5f;
                var dy = y - 3.5f;
                if (dx * dx + dy * dy > 16f) sb.Append("<#0000>W</color>");
                else sb.Append(dx + dy > 2.5f ? "<mark=#c9dcef>W</mark>" : "<mark=#f4f9ff>W</mark>");//右下を少し影に
            }
        }
        return sb.Append("</size>").ToString();
    }

    /// <summary>ホストのコマンド「/snow」(オン・オフ)、「/snow 数」(数を変えてオン)</summary>
    public static void Command(string[] args)
    {
        if (!GameStates.IsLobby)
        {
            Utils.SendMessage("雪玉はロビーでだけ遊べます", PlayerControl.LocalPlayer.PlayerId);
            return;
        }
        if (args.Length >= 2 && int.TryParse(args[1], out var n))
        {
            count = Mathf.Clamp(n, 1, 5);
            if (!Enabled) Enable(true);
            else Clear();
            spawnTimer = 0.5f;
            Utils.SendMessage($"雪玉を{count}個にしました", PlayerControl.LocalPlayer.PlayerId);
            return;
        }
        Enable(!Enabled);
        Utils.SendMessage(Enabled ? $"雪玉を出しました（{count}個）\nもう一度「/snow」で片付けます" : "雪玉を片付けました", PlayerControl.LocalPlayer.PlayerId);
    }
    static void Enable(bool on)
    {
        Enabled = on;
        Clear();
        spawnTimer = 0.5f;
    }
    /// <summary>全部消す</summary>
    public static void Clear()
    {
        foreach (var b in balls) b.Obj?.Despawn();
        balls.Clear();
    }

    /// <summary>人が入ってきたら、いったん全部消して少しあとに出し直す(入ってきた人の画面がおかしくならないように)</summary>
    public static void OnPlayerJoined()
    {
        if (!Enabled || !AmongUsClient.Instance.AmHost) return;
        Clear();
        spawnTimer = 6f;
    }

    /// <summary>毎フレーム(ホストの FixedUpdate から)</summary>
    public static void FixedUpdate()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Enabled) return;
        //ロビー以外・開始のカウントダウン中は片付ける
        if (!GameStates.IsLobby || GameStates.IsCountDown || AmongUsClient.Instance.IsGameStarted)
        {
            if (balls.Count > 0) Clear();
            if (!GameStates.IsLobby) Enabled = false;
            return;
        }
        var dt = Time.fixedDeltaTime;

        //足りなければ足す
        if (balls.Count < count)
        {
            spawnTimer -= dt;
            if (spawnTimer <= 0)
            {
                SpawnOne();
                spawnTimer = balls.Count < count ? 0.5f : RespawnTime;
            }
        }

        var players = PlayerCatch.AllPlayerControls.Where(pc => pc.Data != null && !pc.Data.Disconnected).ToArray();
        foreach (var b in balls.ToArray())
        {
            if (b.Obj == null || !b.Obj.Ready) continue;
            var moving = b.Velocity.magnitude > 0.01f;
            if (moving) Move(b, dt);

            //プレイヤーとの当たり
            foreach (var pc in players)
            {
                var pos = pc.GetTruePosition();
                var diff = b.Obj.Position - pos;
                if (diff.magnitude > Radius[b.Level] + PlayerRadius) continue;
                var justPushed = pc.PlayerId == b.LastPusher && Time.time - b.LastPushTime < 0.6f;
                if (b.Velocity.magnitude >= HitSpeed && !justPushed)
                {
                    //吹き飛ばす
                    Knockback(pc, b.Velocity.normalized, KnockDistance[b.Level]);
                    Logger.Info($"雪玉(大きさ{b.Level})が {pc.Data.GetLogPlayerName()} に当たった", "Snowball");
                    b.Obj.Despawn();
                    balls.Remove(b);
                    spawnTimer = RespawnTime;
                    goto next;
                }
                //押す(歩いてきた向きに転がす)
                var dir = diff.magnitude < 0.01f ? Vector2.up : diff.normalized;
                b.Velocity = dir * PushSpeed[b.Level];
                b.LastPusher = pc.PlayerId;
                b.LastPushTime = Time.time;
                moving = true;
            }

            //位置を送る(動いているときは多め、止まっているときは少なめ)
            if (++b.Frame >= (moving ? 4 : 30))
            {
                b.Frame = 0;
                b.Obj.SendPosition();
            }
        next:;
        }
    }

    static void Move(Snowball b, float dt)
    {
        var speed = b.Velocity.magnitude;
        var dir = b.Velocity / speed;
        var step = speed * dt;
        //壁に当たったら跳ね返る(少し遅くなる)
        if (PhysicsHelpers.AnyNonTriggersBetween(b.Obj.Position, dir, step + Radius[b.Level], Constants.ShipAndObjectsMask))
        {
            b.Velocity = -b.Velocity * 0.5f;
            return;
        }
        b.Obj.Position += dir * step;
        b.Rolled += step;
        //転がると大きくなる
        if (b.Rolled >= GrowDistance && b.Level < SpriteScale.Length - 1)
        {
            b.Rolled = 0;
            b.Level++;
            b.Obj.SetSprite(Sprite(b.Level));
        }
        //だんだん遅くなる
        speed = Mathf.Max(0, speed - Friction * dt);
        b.Velocity = dir * speed;
    }

    /// <summary>吹き飛ばす(壁の手前まで、少しずつ動かす)</summary>
    static void Knockback(PlayerControl pc, Vector2 dir, float distance)
    {
        var from = pc.GetTruePosition();
        while (distance > 0.1f && PhysicsHelpers.AnyNonTriggersBetween(from, dir, distance + 0.3f, Constants.ShipAndObjectsMask))
            distance -= 0.25f;
        if (distance <= 0.1f) return;
        //GetTruePosition は足元なので、transform の位置からずらす
        var start = (Vector2)pc.transform.position;
        const int steps = 4;
        for (var i = 1; i <= steps; i++)
        {
            var to = start + dir * (distance * i / steps);
            _ = new LateTask(() =>
            {
                if (pc == null || !GameStates.IsLobby) return;
                pc.RpcSnapToForced(to);
            }, 0.05f * (i - 1), "Snowball.Knockback", true);
        }
        RPC.PlaySoundRPC(pc.PlayerId, Sounds.KillSound);
    }

    /// <summary>雪玉を1つ出す(誰かの近くの、壁のない場所)</summary>
    static void SpawnOne()
    {
        var players = PlayerCatch.AllPlayerControls.Where(pc => pc.Data != null && !pc.Data.Disconnected).ToArray();
        if (players.Length == 0) return;
        for (var tries = 0; tries < 10; tries++)
        {
            var center = players[IRandom.Instance.Next(players.Length)].GetTruePosition();
            var angle = IRandom.Instance.Next(360) * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var dist = 1f + IRandom.Instance.Next(10) * 0.1f;
            if (PhysicsHelpers.AnyNonTriggersBetween(center, dir, dist + 0.6f, Constants.ShipAndObjectsMask)) continue;
            //人と重ならないように
            var pos = center + dir * dist;
            if (players.Any(p => Vector2.Distance(p.GetTruePosition(), pos) < Radius[0] + PlayerRadius + 0.2f)) continue;
            var obj = new LobbyNetObject(Sprite(0), pos);
            if (obj.playerControl == null) return;//出せなかった
            balls.Add(new Snowball { Obj = obj });
            return;
        }
    }
}
