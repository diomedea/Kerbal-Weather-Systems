﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProtoBuf;

namespace Database
{
    [ProtoContract]
    public struct CloudData
    {
        [ProtoMember(1)]
        public float dDew; //deposited dew in the cloud, solid state
        [ProtoMember(2)]
        public float cDew; //condensed dew in the cloud, liquid state
        [ProtoMember(3)]
        public float dropletSize; // radius of condensed droplets (also used for snow crystals, though is mass being comparable to droplets). Sign used to store the IsIce bool (- = true). 
        [ProtoMember(4)]
        public UInt16 thickness; //we can make the thickness a Uint16 because 1)thickness is never negative, 2)cloud thickness will certainly never be >65.5km for one cell
        [ProtoMember(5)]
        public UInt16 rainyDuration;
        [ProtoMember(6)]
        public Byte rainyDecay; 
        
        public float getwaterContent()
        {
            return cDew + Math.Abs(dDew);
        }
    }
}
