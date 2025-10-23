using System;
using System.Collections.Generic;
using System.Linq;

namespace HistorySim.Game;

public sealed class RogueliteGame
{
    readonly List<string> _messages = new();
    readonly List<PartyMember> _party = new();
    readonly List<Relic> _relics = new();
    readonly List<EncounterOption> _options = new();
    readonly Random _rng;

    EncounterBase? _currentEncounter;
    ActionBar? _actionBar;

    public RogueliteGame(int? seed = null)
    {
        _rng = seed.HasValue ? new Random(seed.Value) : new Random();
        Reset();
    }

    public IReadOnlyList<string> Messages => _messages;
    public IReadOnlyList<PartyMember> Party => _party;
    public IReadOnlyList<Relic> Relics => _relics;
    public IReadOnlyList<EncounterOption> Options => _options;
    public GamePhase Phase { get; private set; }
    public int Level { get; private set; }
    public ActionBar? CurrentActionBar => _actionBar;
    public EncounterBase? CurrentEncounter => _currentEncounter;

    public void Reset()
    {
        _messages.Clear();
        _party.Clear();
        _relics.Clear();
        _options.Clear();

        _party.Add(new PartyMember("Vera", 24));
        _party.Add(new PartyMember("Roland", 22));
        _party.Add(new PartyMember("Mira", 20));

        Level = 1;
        Phase = GamePhase.AwaitingCommand;
        _currentEncounter = null;
        _actionBar = null;
    }

    public void StartNewRun()
    {
        Reset();
        Phase = GamePhase.SelectingEncounter;
        GenerateOptions();
        AddMessage("新しい遠征を開始しました。行き先を選択してください。");
    }

    public bool TryChooseOption(int index, out string message)
    {
        message = string.Empty;
        if (Phase != GamePhase.SelectingEncounter)
        {
            message = "現在は進行中の遭遇を完了してください。";
            return false;
        }

        if (index < 0 || index >= _options.Count)
        {
            message = "無効な選択肢です。";
            return false;
        }

        var option = _options[index];
        _options.Clear();

        switch (option.Type)
        {
            case EncounterType.Normal:
            {
                var enemy = CreateEnemy(Level);
                _currentEncounter = EncounterBase.ForEnemy(enemy, EncounterType.Normal);
                AddMessage($"敵遭遇『{enemy.Name}』に突入！");
                BeginCombat();
                break;
            }
            case EncounterType.Elite:
            {
                var enemy = CreateElite(Level);
                _currentEncounter = EncounterBase.ForEnemy(enemy, EncounterType.Elite);
                AddMessage($"エリート遭遇『{enemy.Name}』に挑戦します！");
                BeginCombat();
                break;
            }
            case EncounterType.Event:
                ResolveEventEncounter();
                break;
            case EncounterType.Shop:
                ResolveShopEncounter();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        message = $"「{option.Label}」を選択。";
        return true;
    }

    public bool TryResolveTurn(double? manualPosition, IList<string> output, out bool combatComplete)
    {
        combatComplete = false;
        if (Phase != GamePhase.Combat)
        {
            output.Add("現在は戦闘中ではありません。");
            return false;
        }

        if (_actionBar is null || _currentEncounter is not EncounterBase.EnemyEncounter encounter)
        {
            output.Add("行動バーが準備されていません。");
            return false;
        }

        var marker = manualPosition ?? _rng.NextDouble();
        output.Add($"バー停止位置: {(int)(marker * 100)}%");

        var evaluation = _actionBar.Evaluate(marker);
        foreach (var execution in evaluation.Executions)
        {
            foreach (var action in execution.Actions)
            {
                ResolveHeroAction(execution.Member, action, encounter.Enemy, output);
                if (encounter.Enemy.CurrentHp <= 0)
                {
                    output.Add($"敵『{encounter.Enemy.Name}』を撃破！");
                    GrantVictoryRewards(encounter);
                    combatComplete = true;
                    return true;
                }
            }
        }

        ResolveEnemyTurn(encounter.Enemy, output);

        if (_party.All(p => p.IsDown))
        {
            output.Add("仲間は全滅しました。遠征は失敗です。");
            Phase = GamePhase.GameOver;
        }
        else
        {
            PrepareNextTurn();
        }

        return true;
    }

    public string GetStatus()
    {
        var party = string.Join(", ", _party.Select(p => $"{p.Name} {p.CurrentHp}/{p.MaxHp}"));
        var relics = _relics.Count > 0 ? string.Join(", ", _relics.Select(r => r.Name)) : "なし";
        var encounter = _currentEncounter is EncounterBase.EnemyEncounter enemy
            ? $"{enemy.Enemy.Name} {enemy.Enemy.CurrentHp}/{enemy.Enemy.MaxHp}"
            : Phase == GamePhase.SelectingEncounter ? "未選択" : "イベント処理中";

        return $"Level {Level} | 仲間: {party} | 遭遇: {encounter} | 遺物: {relics}";
    }

    public IEnumerable<string> DescribeOptions()
        => _options.Select((option, index) => $"{index + 1}. [{option.Type}] {option.Label}");

    public IEnumerable<string> DescribeActionBar()
    {
        if (_actionBar is null)
        {
            yield return "行動バーは未生成です。";
            yield break;
        }

        foreach (var track in _actionBar.Tracks)
        {
            var segments = track.Segments
                .Select(segment => $"{segment.Type} {(int)(segment.Start * 100)}%-{(int)(segment.End * 100)}%")
                .ToArray();
            yield return $"{track.Member.Name}: {string.Join(", ", segments)}";
        }
    }

    void BeginCombat()
    {
        Phase = GamePhase.Combat;
        _actionBar = BuildActionBar();
        AddMessage("戦闘開始！ lock コマンドでバーを停止しましょう。");
    }

    void PrepareNextTurn()
    {
        _actionBar = BuildActionBar();
        AddMessage("次のターンを準備しました。");
    }

    void GrantVictoryRewards(EncounterBase.EnemyEncounter encounter)
    {
        Level += encounter.EncounterType == EncounterType.Elite ? 2 : 1;
        AddMessage($"Level {Level} に到達！");

        var relicChance = encounter.EncounterType == EncounterType.Elite ? 0.7 : 0.25;
        if (_rng.NextDouble() < relicChance)
        {
            var relic = RelicLibrary.Roll(_rng);
            _relics.Add(relic);
            AddMessage($"遺物『{relic.Name}』を獲得した！");
        }

        Phase = GamePhase.SelectingEncounter;
        _currentEncounter = null;
        _actionBar = null;
        GenerateOptions();
        AddMessage("次の行き先を選択してください。");
    }

    void ResolveEventEncounter()
    {
        var roll = _rng.Next(3);
        switch (roll)
        {
            case 0:
                var healTarget = _party[_rng.Next(_party.Count)];
                healTarget.Heal(6);
                AddMessage($"イベント: {healTarget.Name} が休息し 6 回復。");
                break;
            case 1:
                var relic = RelicLibrary.Roll(_rng);
                _relics.Add(relic);
                AddMessage($"イベント: 遺物『{relic.Name}』を入手！");
                break;
            default:
                var damageTarget = _party[_rng.Next(_party.Count)];
                damageTarget.TakeDamage(5);
                AddMessage($"イベント: 罠により {damageTarget.Name} が 5 ダメージ！");
                break;
        }

        Phase = GamePhase.SelectingEncounter;
        GenerateOptions();
    }

    void ResolveShopEncounter()
    {
        AddMessage("ショップ: 所持金が無く眺めるだけだった…");
        Phase = GamePhase.SelectingEncounter;
        GenerateOptions();
    }

    void ResolveHeroAction(PartyMember member, ActionType action, Enemy enemy, IList<string> output)
    {
        switch (action)
        {
            case ActionType.Attack:
            {
                var damage = 4 + Level;
                if (_relics.OfType<AttackBoostRelic>().Any())
                {
                    damage += 2;
                }
                enemy.TakeDamage(damage);
                output.Add($"{member.Name} の攻撃！ {enemy.Name} に {damage} ダメージ。");
                break;
            }
            case ActionType.Skill:
            {
                var heal = 3 + Level / 2;
                member.Heal(heal);
                output.Add($"{member.Name} のスキル。自身を {heal} 回復。");
                break;
            }
            case ActionType.Rest:
            {
                var heal = 2;
                member.Heal(heal);
                output.Add($"{member.Name} は休憩し {heal} 回復。");
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }
    }

    void ResolveEnemyTurn(Enemy enemy, IList<string> output)
    {
        if (enemy.CurrentHp <= 0)
        {
            return;
        }

        var alive = _party.Where(p => !p.IsDown).ToList();
        if (alive.Count == 0)
        {
            return;
        }

        var target = alive[_rng.Next(alive.Count)];
        var damage = enemy.BaseAttack + Level;
        target.TakeDamage(damage);
        output.Add($"{enemy.Name} の攻撃！ {target.Name} が {damage} ダメージ（残り {target.CurrentHp}）。");
    }

    void GenerateOptions()
    {
        _options.Clear();
        var candidates = new[]
        {
            EncounterType.Normal,
            EncounterType.Event,
            EncounterType.Shop,
            EncounterType.Elite
        };

        var normalCount = 0;
        while (_options.Count < 3)
        {
            var type = candidates[_rng.Next(candidates.Length)];
            if (type == EncounterType.Normal)
            {
                normalCount++;
            }

            if (_options.Count == 2 && normalCount == 0)
            {
                type = EncounterType.Normal;
            }

            var label = type switch
            {
                EncounterType.Normal => $"野盗団 Lv{Level}",
                EncounterType.Elite => $"守護者 Lv{Level + 1}",
                EncounterType.Event => "謎めいた祭壇",
                EncounterType.Shop => "旅の商人",
                _ => "未知"
            };

            _options.Add(new EncounterOption(type, label));
        }
    }

    ActionBar BuildActionBar()
    {
        var tracks = new List<ActionTrack>(_party.Count);
        foreach (var member in _party)
        {
            var segments = GenerateSegments();
            foreach (var relic in _relics)
            {
                relic.ModifySegments(member, segments);
            }
            tracks.Add(new ActionTrack(member, segments));
        }

        return new ActionBar(tracks);
    }

    List<ActionSegment> GenerateSegments()
    {
        var cuts = new[] { _rng.NextDouble(), _rng.NextDouble() };
        Array.Sort(cuts);
        var points = new[] { 0.0, cuts[0], cuts[1], 1.0 };

        var actions = new List<ActionType> { ActionType.Attack, ActionType.Skill, ActionType.Rest };
        Shuffle(actions);

        var segments = new List<ActionSegment>(3);
        for (var i = 0; i < 3; i++)
        {
            var start = points[i];
            var end = points[i + 1];
            if (end - start < 0.05)
            {
                end = start + 0.05;
            }
            if (end > 1.0)
            {
                end = 1.0;
            }
            segments.Add(new ActionSegment(start, end, actions[i], 1));
        }

        return segments
            .OrderBy(segment => segment.Start)
            .ToList();
    }

    Enemy CreateEnemy(int level)
    {
        var hp = 18 + level * 2;
        var attack = 5 + level;
        return new Enemy("野盗斥候", hp, attack);
    }

    Enemy CreateElite(int level)
    {
        var hp = 28 + level * 3;
        var attack = 8 + level;
        return new Enemy("鉄甲の守護者", hp, attack);
    }

    void AddMessage(string message) => _messages.Add(message);

    void Shuffle<T>(IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = _rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    #region Nested types

    public enum GamePhase
    {
        AwaitingCommand,
        SelectingEncounter,
        Combat,
        GameOver
    }

    public enum EncounterType
    {
        Normal,
        Elite,
        Event,
        Shop
    }

    public readonly record struct EncounterOption(EncounterType Type, string Label);

    public abstract class EncounterBase
    {
        protected EncounterBase(string name, EncounterType type)
        {
            Name = name;
            EncounterType = type;
        }

        public string Name { get; }
        public EncounterType EncounterType { get; }

        public sealed class EnemyEncounter : EncounterBase
        {
            public EnemyEncounter(string name, EncounterType type, Enemy enemy) : base(name, type)
            {
                Enemy = enemy;
            }

            public Enemy Enemy { get; }
        }

        public static EnemyEncounter ForEnemy(Enemy enemy, EncounterType type)
            => new(enemy.Name, type, enemy);
    }

    public class Enemy
    {
        public Enemy(string name, int maxHp, int baseAttack)
        {
            Name = name;
            MaxHp = maxHp;
            BaseAttack = baseAttack;
            CurrentHp = maxHp;
        }

        public string Name { get; }
        public int MaxHp { get; }
        public int BaseAttack { get; }
        public int CurrentHp { get; private set; }

        public void TakeDamage(int amount)
        {
            CurrentHp = Math.Max(0, CurrentHp - amount);
        }
    }

    public sealed class PartyMember
    {
        public PartyMember(string name, int maxHp)
        {
            Name = name;
            MaxHp = maxHp;
            CurrentHp = maxHp;
        }

        public string Name { get; }
        public int MaxHp { get; }
        public int CurrentHp { get; private set; }
        public bool IsDown => CurrentHp <= 0;

        public void TakeDamage(int amount)
        {
            CurrentHp = Math.Max(0, CurrentHp - amount);
        }

        public void Heal(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            CurrentHp = Math.Min(MaxHp, CurrentHp + amount);
        }
    }

    public enum ActionType
    {
        Attack,
        Skill,
        Rest
    }

    public readonly record struct ActionSegment(double Start, double End, ActionType Type, int Stack)
    {
        public bool Contains(double position) => position >= Start && position <= End;
    }

    public sealed class ActionTrack
    {
        public ActionTrack(PartyMember member, IReadOnlyList<ActionSegment> segments)
        {
            Member = member;
            Segments = segments;
        }

        public PartyMember Member { get; }
        public IReadOnlyList<ActionSegment> Segments { get; }
    }

    public sealed class ActionBar
    {
        public ActionBar(IReadOnlyList<ActionTrack> tracks)
        {
            Tracks = tracks;
        }

        public IReadOnlyList<ActionTrack> Tracks { get; }

        public ActionEvaluation Evaluate(double position)
        {
            position = Math.Clamp(position, 0, 1);
            var executions = new List<ActionExecution>(Tracks.Count);

            foreach (var track in Tracks)
            {
                var actions = track.Segments
                    .Where(segment => segment.Contains(position))
                    .SelectMany(segment => Enumerable.Repeat(segment.Type, Math.Max(1, segment.Stack)))
                    .ToArray();

                if (actions.Length == 0)
                {
                    actions = new[] { ActionType.Rest };
                }

                executions.Add(new ActionExecution(track.Member, actions));
            }

            return new ActionEvaluation(position, executions);
        }
    }

    public readonly record struct ActionEvaluation(double Position, IReadOnlyList<ActionExecution> Executions);

    public readonly record struct ActionExecution(PartyMember Member, IReadOnlyList<ActionType> Actions);

    public abstract class Relic
    {
        protected Relic(string name, string description)
        {
            Name = name;
            Description = description;
        }

        public string Name { get; }
        public string Description { get; }

        public virtual void ModifySegments(PartyMember member, List<ActionSegment> segments)
        {
        }
    }

    public sealed class AttackBoostRelic : Relic
    {
        public AttackBoostRelic() : base("鋭刃の紋章", "攻撃力がわずかに上昇する")
        {
        }
    }

    public sealed class OverlapCharmRelic : Relic
    {
        public OverlapCharmRelic() : base("時間の風車", "攻撃セグメントを広げ、重なりを生み出す")
        {
        }

        public override void ModifySegments(PartyMember member, List<ActionSegment> segments)
        {
            for (var i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                if (segment.Type != ActionType.Attack)
                {
                    continue;
                }

                var extended = new ActionSegment(
                    Start: Math.Max(0, segment.Start - 0.05),
                    End: Math.Min(1, segment.End + 0.08),
                    Type: segment.Type,
                    Stack: segment.Stack + 1);

                segments[i] = extended;
                break;
            }
        }
    }

    static class RelicLibrary
    {
        public static Relic Roll(Random rng)
        {
            var roll = rng.NextDouble();
            return roll < 0.6 ? new AttackBoostRelic() : new OverlapCharmRelic();
        }
    }

    #endregion
}
