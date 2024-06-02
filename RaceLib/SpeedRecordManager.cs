﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Timing;
using Tools;

namespace RaceLib
{
    public enum Units
    {
        Metric,
        Imperial
    }

    public class SpeedRecordManager
    {
        public TimingSystemManager TimingSystemManager { get; private set; }
        public RaceManager RaceManager { get; private set; }
        public EventManager EventManager { get; private set; }

        private Dictionary<int, float> timingSystemIndexDistance;

        private Dictionary<Pilot, SpeedRecord> speedRecords;
        private float maxSpeed;

        public bool HasDistance { get; private set; }

        public event Action<Split, float> OnNewPersonalBest;
        public event Action<Split, float> OnNewOveralBest;
        public event Action<Split, float> OnSpeedCalculated;

        const int MaxValidSpeed = 200; // m/s

        public SpeedRecordManager(RaceManager raceManager)
        {
            EventManager = raceManager.EventManager;
            timingSystemIndexDistance = null;
            RaceManager = raceManager;

            TimingSystemManager = raceManager.TimingSystemManager;
            TimingSystemManager.OnInitialise += Initialize;
            raceManager.OnSplitDetection += RaceManager_OnSplitDetection;
            raceManager.OnLapDetected += RaceManager_OnLapDetected;
            raceManager.OnDetectionDisqualified += RaceManager_OnDetectionDisqualified;

            speedRecords = new Dictionary<Pilot, SpeedRecord>();
        }


        public void Update()
        {
            maxSpeed = 0;
            if (!HasDistance)
                return;

            foreach (Race race in RaceManager.GetRaces(r => r.Started))
            {
                foreach (Pilot pilot in race.Pilots)
                {
                    foreach (Split split in race.GetSplits(pilot))
                    {
                        CheckSplit(split);
                    }
                }
            }
        }

        public void UpdatePilot(Pilot pi)
        {
            if (!HasDistance)
                return;

            speedRecords.Remove(pi);

            foreach (Race race in RaceManager.GetRaces(r => r.Started && r.HasPilot(pi)))
            {
                foreach (Pilot pilot in race.Pilots)
                {
                    foreach (Split split in race.GetSplits(pilot))
                    {
                        CheckSplit(split);
                    }
                }
            }

            maxSpeed = speedRecords.Values.Select(r => r.Speed).Max();
        }

        private void RaceManager_OnDetectionDisqualified(Race race, Detection detection)
        {
            SpeedRecord speedRecord;
            if (speedRecords.TryGetValue(detection.Pilot, out speedRecord))
            {
                if (speedRecord.Split.Detection == detection)
                {
                    UpdatePilot(detection.Pilot);
                }
            }
        }

        private void RaceManager_OnLapDetected(Lap lap)
        {
            if (!HasDistance)
                return;

            if (lap != null && lap.Race != null)
            {
                Split split = lap.Race.GetSplit(lap.Detection);
                if (split != null)
                {
                    CheckSplit(split);
                }
            }
        }

        private void RaceManager_OnSplitDetection(Detection detection)
        {
            if (!HasDistance)
                return;

            Race race = RaceManager.CurrentRace;
            if (detection != null && race != null)
            {
                Split split = race.GetSplit(detection);
                CheckSplit( split);
            }
        }

        private void CheckSplit(Split split)
        {
            if (split == null)
                return;
            
            Pilot pilot = split.Detection.Pilot;
            float newSpeed = GetSpeed(split);

            if (newSpeed == 0)
                return;

            if (newSpeed > MaxValidSpeed)
            {
                Logger.TimingLog.Log(this, "Speed over max limit: " + newSpeed + " m/s");
                return;
            }

            SpeedRecord speedRecord;
            if (!speedRecords.TryGetValue(pilot, out speedRecord))
            {
                speedRecord = new SpeedRecord() { Split = split, Speed = 0.0f };
            }

            OnSpeedCalculated?.Invoke(split, newSpeed);

            if (newSpeed > speedRecord.Speed)
            {
                speedRecord.Speed = newSpeed;
                speedRecords[pilot] = speedRecord;

                OnNewPersonalBest?.Invoke(split, newSpeed);

                if (newSpeed > maxSpeed)
                {
                    maxSpeed = newSpeed;
                    OnNewOveralBest?.Invoke(split, newSpeed);
                }
            }
        }

        public void Initialize()
        {
            Initialize(RaceManager.EventManager.FlightPath);
        }

        public void Initialize(TrackFlightPath trackFlightPath)
        {
            Dictionary<int, float> indexDistance2 = new Dictionary<int, float>();

            ITimingSystem[] timingSystems = TimingSystemManager.TimingSystemsSectorOrder.ToArray();
            Sector[] sectors = trackFlightPath.Sectors;

            int max = Math.Min(timingSystems.Length, sectors.Length);

            for (int i = 0; i < max; i++)
            {
                ITimingSystem timingSystem = timingSystems[i];
                Sector sector = sectors[i];

                int index = TimingSystemManager.GetIndex(timingSystem);
                indexDistance2.Add(index, sector.Length);
            }

            HasDistance = indexDistance2.Values.Any(a => a > 0);

            timingSystemIndexDistance = indexDistance2;

            Update();
        }

        public float GetSpeed(Race race, Detection detection)
        {
            if (!HasDistance)
                return 0;

            Split split = race.GetSplit(detection);
            return GetSpeed(split);
        }

        public float GetSpeed(Split split)
        {
            if (!HasDistance)
                return 0;

            if (split == null)
                return 0;

            if (timingSystemIndexDistance == null)
                return 0;

            if (split.Time == TimeSpan.Zero)
                return 0;

            if (timingSystemIndexDistance.TryGetValue(split.Detection.TimingSystemIndex, out float distance))
            {
                if (distance > 0)
                {
                    float metersPerSecond = (float)(distance / split.Time.TotalSeconds);

                    if (metersPerSecond > MaxValidSpeed)
                    {
                        return 0;
                    }

                    return metersPerSecond;
                }
            }
            return 0;
        }

        public IEnumerable<float> GetSpeeds(IEnumerable<Split> splits)
        {
            foreach (Split split in splits)
            {
                yield return GetSpeed(split);
            }
        }

        public int SpeedToUnit(float speed, Units unit)
        {
            switch (unit)
            {
                case Units.Imperial:
                    float miles = speed * 2.237f;
                    return (int)miles;

                case Units.Metric:
                default:
                    float km = speed * 3.6f;
                    return (int)km;
            }
        }

        public string SpeedToString(float speed, Units unit)
        {
            float unitized = SpeedToUnit(speed, unit);

            switch (unit)
            {
                case Units.Imperial:
                    return unitized.ToString("0") + " mph";

                case Units.Metric:
                default:
                    return unitized.ToString("0") + "km/h";
            }
        }

        public bool GetBestSpeed(Pilot pilot, out Split split, out float speed, out bool overalBest)
        {
            SpeedRecord speedRecord;
            if (HasDistance && speedRecords.TryGetValue(pilot, out speedRecord))
            {
                split = speedRecord.Split;
                speed = speedRecord.Speed;

                overalBest = maxSpeed == speed;

                return true;
            }
            speed = 0.0f;
            split = null;
            overalBest = false;

            return false;
        }


        private struct SpeedRecord
        {
            public float Speed;
            public Split Split;

            public override string ToString()
            {
                return Split.ToString() + " " + Speed.ToString();
            }
        }

    }
}
