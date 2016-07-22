﻿using System;
using Database;
using Proto;
using ProtoBuf;
using UnityEngine;

namespace Database
{
    [ProtoContract]
    public struct WeatherCell
    {
        [ProtoMember(1)]
        public float temperature { get; internal set; }
        [ProtoMember(2)]
        public float pressure { get; internal set; }
        [ProtoMember(3)]
        public float relativeHumidity { get; internal set; }
        [ProtoMember(4)]
        public float CCN { get; internal set;}
        [ProtoMember(5)]
        public float N_Dew { get; internal set; }//20B
        [ProtoMember(6)]
        public CloudData cloud { get; internal set; } //15B
        [ProtoMember(7)]
        public ProtoVector3 windVector { get; internal set; } //12B IS LOCAL TO THE CELL, NOT WORLD COORDS
        public float flowPChange { get; internal set; }
        
        public float getDropletSize()
        {
            return Math.Abs(cloud.dropletSize);
        }
        public bool getIsIce()
        {
            return Convert.ToBoolean(Math.Sign(cloud.dropletSize));
        }
    }
}
