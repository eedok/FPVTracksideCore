﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Timing.RotorHazard
{
    public struct Version
    {
        public string Major { get; set; }
        public string Minor { get; set; }
    }

    public struct FrequencyDatas
    {
        public FrequencyData[] fdata { get; set; }
    }

    public class FrequencyData
    {
        public string Band { get; set; }
        public string Channel { get; set; }
        public int Frequency { get; set; }

        public override string ToString()
        {
            string output = "";
            if (Band != null) output += Band;
            if (Channel != null) output += Channel + " ";

            output += Frequency + "mhz";

            return output;
        }
    }

    public struct PiTime
    {
        public double pi_time_s { get; set; }
    }

    public struct PiTimeSample
    {
        public TimeSpan Differential { get; set; }
        public TimeSpan Response { get; set; }
    }

    public class NodeData
    {
        //node_data {"node_peak_rssi":[122,51,51,53],"node_nadir_rssi":[40,39,43,47],"pass_peak_rssi":[115,0,0,0],"pass_nadir_rssi":[71,0,0,0],"debug_pass_count":[3,0,0,0]}
        public int[] node_peak_rssi { get; set; }
        public int[] node_nadir_rssi { get; set; }
        public int[] pass_peak_rssi { get; set; }
        public int[] pass_nadir_rssi { get; set; }
        public int[] debug_pass_count { get; set; }
    }

    public struct Heartbeat
    {
        //{[{"current_rssi":[57,57,49,41],"frequency":[5658,5695,5760,5800],"loop_time":[1020,1260,1092,1136],"crossing_flag":[false,false,false,false]}]}
        
        public int[] current_rssi { get; set; }
        public int[] frequency { get; set; }
        public int[] loop_time { get; set; }
        public bool[] crossing_flag { get; set; }

    }

    public struct SetFrequency
    {
        public int node { get; set; }
        public int frequency { get; set; }
    }

    public struct PassRecord
    {
        public int node { get; set; }
        public int frequency { get; set; }
        public double timestamp { get; set; }
    }

    public struct SetSettings
    {
        public int calibration_threshold { get; set; }
        public int calibration_offset { get; set; }
        public int trigger_threshold { get; set; }
        public int filter_ratio { get; set; }
    }

    public class TimeStamp
    {
        public double timestamp { get; set; }
    }

    public struct EnvironmentDataValue
    {
        public double value { get; set; }
        public string units { get; set; }
    }

    public struct EnvironmentDataSensor
    {
        public EnvironmentDataValue temperature { get; set; }
        public EnvironmentDataValue voltage { get; set; }
    }

    public struct EnvironmentData
    {
        public EnvironmentDataSensor Core { get; set; }
    }
}
