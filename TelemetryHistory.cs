using System;
using Brutal.Numerics;
using KSA;
using KittenEngineerRedux.Analysis;

namespace KittenEngineerRedux.Flight;

internal enum TelemetryMetric
{
    Altitude,
    SurfaceSpeed,
    Twr,
    Thrust,
    Acceleration,
    Mass
}

internal static class TelemetryHistory
{
    private const int Capacity = 180;
    private const double SampleInterval = 0.25;

    private static readonly float[][] _samples =
    {
        new float[Capacity],
        new float[Capacity],
        new float[Capacity],
        new float[Capacity],
        new float[Capacity],
        new float[Capacity]
    };

    private static int _count;
    private static double _lastSampleTime = double.NegativeInfinity;
    private static string? _vehicleId;

    public static int Count => _count;

    public static void Record(Vehicle vehicle, ActualEnginePerformanceInfo engines, float actualTwr)
    {
        if (_vehicleId != vehicle.Id)
        {
            Clear();
            _vehicleId = vehicle.Id;
        }

        double playerTime = Program.GetPlayerTime();
        if (playerTime - _lastSampleTime < SampleInterval)
            return;
        _lastSampleTime = playerTime;

        int index;
        if (_count < Capacity)
        {
            index = _count++;
        }
        else
        {
            for (int i = 0; i < _samples.Length; i++)
                Array.Copy(_samples[i], 1, _samples[i], 0, Capacity - 1);
            index = Capacity - 1;
        }

        _samples[(int)TelemetryMetric.Altitude][index] = (float)vehicle.GetBarometricAltitude();
        _samples[(int)TelemetryMetric.SurfaceSpeed][index] = (float)vehicle.GetSurfaceSpeed();
        _samples[(int)TelemetryMetric.Twr][index] = actualTwr;
        _samples[(int)TelemetryMetric.Thrust][index] = engines.TotalThrust / 1000f;
        _samples[(int)TelemetryMetric.Acceleration][index] =
            (float)(vehicle.AccelerationBody.Length() / Constants.STANDARD_GRAVITY);
        _samples[(int)TelemetryMetric.Mass][index] = vehicle.TotalMass;
    }

    public static ReadOnlySpan<float> GetSamples(TelemetryMetric metric)
    {
        return _samples[(int)metric].AsSpan(0, _count);
    }

    public static (float Min, float Max) GetRange(ReadOnlySpan<float> values)
    {
        if (values.IsEmpty)
            return (0f, 1f);

        float min = values[0];
        float max = values[0];
        for (int i = 1; i < values.Length; i++)
        {
            min = Math.Min(min, values[i]);
            max = Math.Max(max, values[i]);
        }
        if (max - min < 0.001f)
        {
            min -= 1f;
            max += 1f;
        }
        else
        {
            float padding = (max - min) * 0.05f;
            min -= padding;
            max += padding;
        }
        return (min, max);
    }

    public static void Clear()
    {
        _count = 0;
        _lastSampleTime = double.NegativeInfinity;
    }
}
