using System;

namespace HistorySim;

public sealed class Simulation
{
    private readonly Random _rng = new();
    private readonly Scenario[] _scenarios =
    {
        new("現地報告: 物流の停滞が発生", new Delta { Stability = -0.05, Redundancy = -0.06 }),
        new("記録庫の整理が進展", new Delta { Redundancy = +0.05, Confidence = +0.02 }),
        new("研究チームの士気が向上", new Delta { Stability = +0.04, Confidence = +0.05 }),
        new("未知の依存関係が露呈", new Delta { Correlation = -0.06, Stability = -0.03 }),
        new("歴史的洞察が得られた", new Delta { Confidence = +0.06, Correlation = +0.04 }),
        new("資材不足が深刻化", new Delta { Stability = -0.06, Confidence = -0.03 }),
        new("外部からの支援が到着", new Delta { Redundancy = +0.06, Stability = +0.02 })
    };

    private double _stability;
    private double _redundancy;
    private double _correlation;
    private double _confidence;
    private int _tick;

    public string? LastEvent { get; private set; }

    public Simulation()
    {
        Reset();
    }

    public void Reset()
    {
        _tick = 0;
        _stability = 0.72;
        _redundancy = 0.68;
        _correlation = 0.57;
        _confidence = 0.63;
        LastEvent = null;
    }

    public string Metrics()
        => FormattableString.Invariant($"tick {_tick:000} | 安定性 {Format(_stability)} | 冗長性 {Format(_redundancy)} | 相関 {Format(_correlation)} | 確信 {Format(_confidence)}");

    public bool Tick(out string note)
    {
        _tick++;
        note = string.Empty;

        ApplyDelta(new Delta
        {
            Stability = Next(-0.015, 0.015),
            Redundancy = Next(-0.015, 0.015),
            Correlation = Next(-0.02, 0.02),
            Confidence = Next(-0.012, 0.012)
        });

        if (_rng.NextDouble() < 0.42)
        {
            var scenario = _scenarios[_rng.Next(_scenarios.Length)];
            ApplyDelta(scenario.Adjustment);
            LastEvent = scenario.Message;
            note = scenario.Message;
        }

        ClampMetrics();
        var weakest = WeakestMetric(out var weakestValue);

        if (weakestValue <= 0.05)
        {
            note = $"{weakest} が臨界を下回り、運用不能となりました。";
            return true;
        }

        if (_confidence >= 1.05 && _redundancy >= 1.0)
        {
            note = "記録体系が盤石となり、長期安定が宣言されました。";
            return true;
        }

        if (_tick >= 160)
        {
            if (string.IsNullOrEmpty(note))
            {
                note = "時間切れ。今期のシミュレーションを終了します。";
            }
            return true;
        }

        return false;
    }

    public void ApplyEvent(string title, Delta delta, string detail)
    {
        ApplyDelta(delta);
        ClampMetrics();
        LastEvent = $"{title} - {detail}";
    }

    private void ApplyDelta(Delta delta)
    {
        _stability += delta.Stability;
        _redundancy += delta.Redundancy;
        _correlation += delta.Correlation;
        _confidence += delta.Confidence;
    }

    private void ClampMetrics()
    {
        _stability = Clamp(_stability);
        _redundancy = Clamp(_redundancy);
        _correlation = Clamp(_correlation);
        _confidence = Clamp(_confidence);
    }

    private static double Clamp(double value)
        => Math.Clamp(value, 0.0, 1.2);

    private static string Format(double value)
        => $"{Math.Clamp(value, 0.0, 1.2) * 100:0.0}%";

    private string WeakestMetric(out double value)
    {
        var metrics = new (string Label, double Value)[]
        {
            ("安定性", _stability),
            ("冗長性", _redundancy),
            ("相関", _correlation),
            ("確信", _confidence)
        };

        var weakest = metrics[0];
        foreach (var metric in metrics)
        {
            if (metric.Value < weakest.Value)
            {
                weakest = metric;
            }
        }

        value = weakest.Value;
        return weakest.Label;
    }

    private double Next(double min, double max)
        => min + _rng.NextDouble() * (max - min);

    private readonly record struct Scenario(string Message, Delta Adjustment);
}

public struct Delta
{
    public double Stability { get; set; }
    public double Redundancy { get; set; }
    public double Correlation { get; set; }
    public double Confidence { get; set; }
}
