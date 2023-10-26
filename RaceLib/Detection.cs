﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Timing;
using Tools;

namespace RaceLib
{
    public class Detection : BaseObjectT<DB.Detection>
    {
        public int TimingSystemIndex { get; set; }

        public Channel Channel { get; set; }
        public DateTime Time { get; set; }

        public int Peak { get; set; }

        public TimingSystemType TimingSystemType { get; set; }

        public Pilot Pilot { get; set; }

        public int LapNumber { get; set; }
        public bool Valid { get; set; }
        public enum ValidityTypes
        {
            Auto,
            ManualOverride
        }

        public ValidityTypes ValidityType { get; set; }

        public bool IsLapEnd { get; set; }

        
        public int RaceSector { get { return RaceSectorCalculator(LapNumber, TimingSystemIndex); } }

        
        public int SectorNumber { get { return TimingSystemIndex + 1; } }

        public bool IsHoleshot { get { return Valid && IsLapEnd && LapNumber == 0; } }


        public Detection(DB.Detection obj)
            : base(obj)
        {
            Channel = obj.Channel.Convert<Channel>();
            Pilot = obj.Pilot.Convert<Pilot>();
        }

        internal Detection()
        {
        }

        public Detection(TimingSystemType timingSystemType, int timingSystem, Pilot pilot, Channel channel, DateTime time, int lapNumber, bool isLapEnd, int peak)
        {
            Valid = true;
            Pilot = pilot;
            TimingSystemType = timingSystemType;
            TimingSystemIndex = timingSystem;
            Channel = channel;
            Time = time;
            LapNumber = lapNumber;
            IsLapEnd = isLapEnd;
            Peak = peak;

            ValidityType = ValidityTypes.Auto;
        }

        public override string ToString()
        {
            return "Detection " + Pilot.Name + " L" + LapNumber + " I" + TimingSystemIndex + " RS" + RaceSector + " T" + Time.ToLogFormat();
        }

        public static int RaceSectorCalculator(int lapNumber, int timingSystemIndex)
        {
            return (lapNumber * 100) + timingSystemIndex;
        }

        public override DB.Detection GetDBObject()
        {
            DB.Detection detection = base.GetDBObject();
            detection.Pilot = Pilot.GetDBObject();
            detection.Channel = Channel.GetDBObject();
            return detection;
        }
    }
}
