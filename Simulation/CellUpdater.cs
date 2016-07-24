﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GeodesicGrid;
using KerbalWeatherSystems;
using Database;

namespace Simulation
{
    public class CellUpdater
    {
        public static float UGC = 8.3144621f;
        public static float D_DIFF = 0.01f;
        public static float K_DIVG = 600f;
        public static float Suth_S = 110.4f;
        public static float Suth_B = 0.000001458f;
        internal static float K_CCN = 0.0001f;
        internal static float K_Prec = 0.005f;
        internal static float K_DROP = 180f;
        internal static float K_DROP2 = 100f;
        internal static float K_THICK = 1000.0f;
        internal static double K_N_DROP = 1.6E15;
        public static readonly float FLT_Epsilon = (float)Math.Pow(2, -24);  // machine epsilon for single-precision floating point numbers
        public static readonly double DBL_Epsilon = Math.Pow(2, -53);  // machine epsilon for double-precision floating point numbers
        
        internal static long run = 0;
        public static bool KWSerror = false;


        //Debug data for storing a list of a cell's data
        //to add data: debugData.Add("DataIwantoadd: " + datatoadd);
        internal static List<String> debugData = new List<String>();
        //example
        //debugData.Add("Temp: " + wcell.temperature); = "Temp: 317.221"


        public static void UpdateCell(PlanetData PD, Cell cell)
        {
            long cycle = run;
            List<String> debugLines = debugData;


            //Logger("Updating cell...");

            float latitude = WeatherFunctions.GetCellLatitude(cell);
            byte sideCount = (byte)(cell.IsPentagon ? 5 : 6);
            //float Q = 0f;
            float SWT = 0f;

            
            //28B

            int layerCount = PD.LiveMap.Count;
            int stratoCount = PD.LiveStratoMap.Count;

            float[] ReflFunc = new float[layerCount];
            float SoilReflection = 0f;

            float[] LF = new float[layerCount]; //layer fraction for air
            float[] LFStrato = new float[stratoCount];

            float[] thermalCap = new float[layerCount]; //thermal capacity includes soil and strato
            float thermalCapSoil = 0f;
            float[] thermalCapStrato = new float[stratoCount];

            float[] Q = new float[layerCount]; //total heat energy, includes soil and strato
            float QSoil = 0f;
            float[] QStrato = new float[stratoCount];

            float[] Cl_SWR = new float[layerCount]; //cloud reflectance
            float[] Cl_SWA = new float[layerCount]; //cloud absorbed

            float[] CCN = new float[layerCount]; //Cloud condensation nuclei, includes soil, but no strato
            float[] dropletsAsCCN = new float[layerCount];  // droplets and ice crystals in clouds acting as CCN for condensation
            //float CCNSoil = 1f;

            float[] Cl_IRA = new float[layerCount]; //cloud IR Absorbed
            float[] Cl_IRR = new float[layerCount]; //cloud IR Reflected

            float[] SWA_Mult = new float[layerCount];  // SW absorb factor
            float[] IRA_Mult = new float[layerCount];  // IR absorb factor
            float[] SWAStrato_Mult = new float[stratoCount];
            float[] IRAStrato_Mult = new float[stratoCount];

            float[] SWR = new float[layerCount]; //SW reflect, includes soil and strato
            float SWRSoil = 0f;
            float[] SWRStrato = new float[stratoCount];

            float[] SWA = new float[layerCount]; //SW absorb
            float SWASoil = 0f;
            float[] SWAStrato = new float[stratoCount];

            float[] SWX = new float[layerCount]; //SW Transmit, no soil, strato

            float[] SWXStrato = new float[stratoCount];

            float[] IRG = new float[layerCount]; //IR Radiate
            float IRGSoil = 0f;
            float[] IRGStrato = new float[stratoCount];

            float[] IRAU = new float[layerCount]; //IR Absorb
            float IRAUSoil = 0f;
            float[] IRAUStrato = new float[stratoCount];

            float[] IRR = new float[layerCount]; //IR Reflect
            float IRRSoil = 0f;
            float[] IRRStrato = new float[stratoCount];

            float[] IRAD = new float[layerCount]; //IR Downward absorb
            float IRADSoil = 0f;
            float[] IRADStrato = new float[stratoCount];

            float[] Q_cond = new float[layerCount]; //Latent heat released, soil, no strato
            float Q_cond_Soil = 0f;
            float[] Q_Prec = new float[layerCount]; // heat exchanged due to precipitations

            float[] N_cond = new float[layerCount]; //Water condensed, no soil, no strato
            float[] N_sscond = new float[layerCount]; //super saturated water condensed, no soil, no strato

            float[] depositedDew = new float[layerCount];  // amount of dew in solid state (e.g. Ice) in clouds
            float[] condensedDew = new float[layerCount];  // amount of dew in liquid state (e.g. water) in clouds
 
            float[] N_dew = new float[layerCount]; //vapor density amount, soil, no strato
            double[] Flux = new double[layerCount]; // diffusion flux

            float[] D_dew = new float[layerCount]; //density of the dew, no strato
            float[] D_dry = new float[layerCount]; //density of dry air, no strato
            float[] D_wet = new float[layerCount]; //density of wet air, soil, no strato?

            float[] RH = new float[layerCount]; //updated relative humidity, no soil
            float[] AH = new float[layerCount]; //absolute humidity, no soil
            float[] AHDP = new float[layerCount]; //Absolute Humidity at Dew Point, no soil
            float[] ew_eq = new float[layerCount]; //equilibrium vapor pressure
            float[] ew = new float[layerCount]; //Partial vapor pressure

            //precipitation vars
            float[] N_Prec = new float[layerCount]; //precipitation amount
            float[] N_Prec_p = new float[layerCount]; //precipitation amount percent
            double[] DI_V = new double[layerCount]; //droplet volume
            float[] DI_S = new float[layerCount]; //droplet terminal velocity
            ushort[] RainyDuration = new ushort[layerCount];  // number of cycles significant droplets are generated with cloud

            //fronts vars
            float[] H_adv_S = new float[layerCount];
            float[] H_disp = new float[layerCount];
            float[] V_disp = new float[layerCount];
            float[] T_adv_S = new float[layerCount];
            float[] T_disp = new float[layerCount];
            float[] ALR = new float[layerCount];
            float[] Z_sat = new float[layerCount];
            float[][] DP = new float[layerCount][];
            float[] Ws_V_ana = new float[layerCount];
            float[] dynPressure = new float[layerCount];  // dynamic Pressure coming from relative movement of airmasses (Bernoulli's_principle)
            uint[] neighbors = new uint[sideCount]; //a debug value only so we can see the neighbors in teh debugger
            double[] direction = new double[sideCount];
            float[] WetPC = new float[layerCount];
            float[] wsN = new float[layerCount]; //northsouth component of wind
            float[] wsE = new float[layerCount]; //eastwest component of wind
            Vector3d[] tensStr = new Vector3d[layerCount];  // external airflow tension stress
            Vector3d[] rotrStr = new Vector3d[layerCount];  // external airflow rotation stress
            float[] Divg = new float[layerCount]; //average divergance of pressure
            double[] wsDiv = new double[layerCount];  // Total divergence wind
            double[] buoyancy = new double[layerCount]; //buoyancy stuff
            double[] DP_V = new double[layerCount]; // vertical pressure gradient
            float[] Total_N = new float[layerCount];
            float[] Total_E = new float[layerCount];
            float[] SH = new float[layerCount];

            float k_ad = PD.atmoData.specificHeatGas / (PD.atmoData.specificHeatGas - UGC / PD.atmoData.M); // k_ad = cg/(cg - UGC/M); should also be used for the adiabatic temperature increase

            double DeltaTime = WeatherFunctions.GetDeltaTime(PD.index);  
            double DeltaAltitude = WeatherFunctions.GetDeltaLayerAltitude(PD.index, cell);
            double[] DeltaDistance_Avg = new double[layerCount];
         

            {
                int neighborIndex = 0;
                foreach (Cell neighbor in cell.GetNeighbors(PD.gridLevel))
                {
                    neighbors[neighborIndex] = neighbor.Index;
                    direction[neighborIndex] = WeatherFunctions.GetDirectionBetweenCells(PD.LiveSoilMap[cell].centroid, PD.LiveSoilMap[neighbor].centroid);
                    neighborIndex++;
                }

            }
            
            // Logger("Var init complete");
            #region CloudsIR+SW
            //-------------------------CLOUDS IR AND SW------------------------\\

            //cloud ir and sw
            //we start at 0 here because we're going through teh air maps, not including soil or strato
            for (int i = 0; i < layerCount; i++)
            {
                
                // actual cloud albedo equations
                WeatherCell wCell = PD.LiveMap[i][cell];
                float G_co = 0f;
                double tau = 0f;
                // double AverageDropletSize16 = WeatherFunctions.AverageDropletSize16(wCell.cloud.dropletSize, wCell.cloud.rainyDecay, wCell.cloud.rainyDuration);
                double AverageDropletSize = WeatherFunctions.AverageDropletSize(wCell.cloud.dropletSize, wCell.cloud.rainyDecay, wCell.cloud.rainyDuration);
                if ((wCell.cloud.thickness < FLT_Epsilon)
                    || (wCell.cloud.getwaterContent() < FLT_Epsilon)
                    || ((AverageDropletSize > 0.003) && (wCell.cloud.getwaterContent() < 0.08f)))  // NOTE: dropletSize > 30000 is assumed out of admissible range
                {
                    Cl_SWR[i] = 0;
                    Cl_SWA[i] = 0;
                    Cl_IRR[i] = 0;
                    Cl_IRA[i] = 0;
                    ReflFunc[i] = 0;
                }
                else if ((AverageDropletSize < 5E-7) && (wCell.cloud.getwaterContent() > FLT_Epsilon))
                {
                    Cl_SWR[i] = 1.0f;
                    Cl_SWA[i] = 0;
                    Cl_IRR[i] = 1.0f;
                    Cl_IRA[i] = 0;
                    ReflFunc[i] = 1.0f;
                }
                else if ((Math.Abs(AverageDropletSize) > 0.003) && (wCell.cloud.getwaterContent() >= 0.08f) 
                    && (wCell.cloud.thickness > DeltaAltitude))
                {
                    Cl_SWR[i] = 0;
                    Cl_SWA[i] = 1.0f;
                    Cl_IRR[i] = 0;
                    Cl_IRA[i] = 1.0f;
                    ReflFunc[i] = 0;
                }
                else
                {
                    float lambda = 9E-7f; // mean wavelength of incident light
                    float Bn = wCell.cloud.dropletSize < 0 ? 1.8f : 5.0f / 3.0f;  // spectral dependance
                    float Cv = wCell.cloud.getwaterContent() / PD.dewData.Dl;  // volumetric concentration of droplets
                    double sigma_ext = 3d / 2d * Cv / AverageDropletSize; // extinction coefficient for light
                    if (Math.Abs(wCell.cloud.dropletSize) > 4)
                    {
                        sigma_ext *= (1 + (1.1 / Math.Pow((2 * Math.PI * AverageDropletSize / lambda), 2.0f / 3.0f)));
                    }
                    G_co = (wCell.cloud.dropletSize < 0) ? 0.26f : 0.13f; // co-asymmetry parameter
                    tau = sigma_ext * wCell.cloud.thickness;  // optical distance in cloud

                    float Xi = 0.0001689855f; // average refractive index in visible band (SW)
                    double alpha = 4d * Math.PI * Xi / lambda;  // absorption coefficient for water
                    double sigma_abs = Bn * alpha * Cv;  // absorption coefficient for light
                    double omega_0 = 1 - (sigma_abs / sigma_ext); // single-scattering albedo
                    double x = tau * Math.Sqrt(3 * (1 - omega_0) * (G_co));  // radiative transfer parameter 'x'
                    double y = 4d * Math.Sqrt((1 - omega_0) / (3d * (G_co)));  // radiative transfer parameter 'y'
                    do
                    {
                        Cl_SWR[i] = (float)(Math.Sinh(x) / Math.Sinh(x + y));
                        Cl_SWA[i] = (float)((Math.Sinh(x + y) - Math.Sinh(x) - Math.Sinh(y)) / Math.Sinh(x + y));
                        x /= 2d;
                    } while (float.IsNaN(Cl_SWR[i]) || float.IsNaN(Cl_SWA[i]));
                    if (float.IsNaN(Cl_SWR[i]) || float.IsNaN(Cl_SWA[i]))
                    {
                        KWSerror = true;
                        Logger("NaN in cloud albedo eq." + " @ cell: " + cell.Index);
                    }
                    
                    Xi = 0.0006094134f; // average refractive index in IR band
                    alpha = 4d * Math.PI * Xi / lambda;
                    sigma_abs = Bn * alpha * Cv;
                    omega_0 = 1 - (sigma_abs / sigma_ext);
                    x = tau * Math.Sqrt(3d * (1 - omega_0) * (G_co));
                    y = 4d * Math.Sqrt((1 - omega_0) / (3d * (G_co)));
                    do
                    {
                        Cl_IRR[i] = (float)(Math.Sinh(x) / Math.Sinh(x + y));
                        Cl_IRA[i] = (float)((Math.Sinh(x + y) - Math.Sinh(x) - Math.Sinh(y)) / Math.Sinh(x + y));
                        x /= 2d;
                    } while (float.IsNaN(Cl_IRR[i]) || float.IsNaN(Cl_IRA[i]));
                    if (float.IsNaN(Cl_IRR[i]) || float.IsNaN(Cl_IRA[i]))
                    {
                        KWSerror = true;
                        Logger("NaN in cloud albedo eq." + " @ cell: " + cell.Index);
                    }
                    // improved ReflFunc
                    if (wCell.cloud.thickness < FLT_Epsilon)
                    {
                        ReflFunc[i] = 0f;
                    }
                    float K0_mu = (3f + 6f * WeatherFunctions.GetSunriseFactor(PD.index, cell)) / 7f;  // Henyey & Greenstein phase function for solar angle
                    float K0_mu0 = 9f / 7f;  // phase function for observer looking directly at cloud
                    float p_theta = (float)((1 - (1 - G_co) * (1 - G_co)) / (4f * Math.PI) / (Math.Pow((1 + (1 - G_co) * (1 - G_co) + 2f * (1 - G_co) * WeatherFunctions.GetSunriseFactor(PD.index, cell)), (3f / 2f))));
                    float t_tau = (float)(1 / (3f / 4f * tau * (1 - WeatherFunctions.GetSunriseFactor(PD.index, cell)) + 15f / 14f));
                    float R0 = (1.48f + 7.76f * WeatherFunctions.GetSunriseFactor(PD.index, cell) + p_theta) / (4f * (WeatherFunctions.GetSunriseFactor(PD.index, cell) + 1f));
                    ReflFunc[i] = R0 - t_tau * K0_mu * K0_mu0;
                    if (ReflFunc[i] > 1.0f)
                    {
                        ReflFunc[i] = 1.0f;
                    }
                    if (ReflFunc[i] < 0f)
                    {
                        ReflFunc[i] = 0f;
                    }
                    if (float.IsNaN(ReflFunc[i]))
                    {
                        KWSerror = true;
                        Logger("ReflFunc gone mad" + " @ cell: " + cell.Index);
                    }
                }
            }


            #endregion CloudsIR+SW
            //Logger("Cloud ir sw complete");

            #region Humidity
            //----------------------- HUMIDITY ---------------------------\\

            //Density of dry air, equilibrium vapor pressure, partial vapor pressure, absolute humidity at dew point
            for (int i = 0; i < layerCount; i++)
            {
                WeatherCell wCellLive = PD.LiveMap[i][cell];
                N_cond[i] = 0;
                D_dry[i] = (wCellLive.pressure / wCellLive.temperature * PD.atmoData.M) / UGC;
                ew_eq[i] = WeatherFunctions.getEwEq(PD.index, wCellLive.temperature);
                
                if (ew_eq[i] == 0)
                {
                    KWSerror = true;
                    Logger("eweq is ZERO!" + " @ cell: " + cell.Index);
                    Logger("Temp: " + wCellLive.temperature);
                }

                float RH_Live = wCellLive.relativeHumidity;
                if ((i==0) && (RH_Live >= 1.0f))
                {
                    // Foggy!
                    N_cond[i] = (RH_Live - 0.999f) * PD.dewData.M / UGC / wCellLive.temperature;
                    RH_Live = 0.999f;
                }

                ew[i] = ew_eq[i] * RH_Live;

                if (float.IsNaN(ew[i]))
                {
                    KWSerror = true;
                    Logger("ew is NaN" + " @ cell: " + cell.Index);
                    Logger("eweq: " + ew_eq[i]);
                    Logger("RH: " + wCellLive.relativeHumidity);
                }
                AH[i] = getAH(ew[i], PD.dewData.M, wCellLive.temperature);
                AHDP[i] = getAH(ew_eq[i], PD.dewData.M, wCellLive.temperature);

                D_wet[i] = ((wCellLive.pressure - ew[i]) * PD.atmoData.M + ew[i] * PD.dewData.M) / (UGC * wCellLive.temperature);

                if (float.IsInfinity(D_wet[i]))
                {
                    KWSerror = true;
                    Logger("DWet is infinity" + " @ cell: " + cell.Index);
                }
                if (float.IsNaN(D_wet[i]))
                {
                    KWSerror = true;
                    Logger("DWet is NaN" + " @ cell: " + cell.Index);
                }

            }
            #endregion Humidity
            //Logger("Humidity complete");
            float SPH = AH[0] / D_dry[0];
            float SPHeq = AHDP[0] / D_dry[0];
            float K_evap = 25 + 19 * getWsH(PD.LiveMap[0][cell]); //evaporation with the horizontal wind in m/s
            double Evap = K_evap * (SPHeq - SPH) / 3600 * PD.biomeDatas[WeatherFunctions.GetBiome(PD.index, cell)].FLC; //divide by 3600 because seconds to hours

            if (Evap < 0)
            {
                KWSerror = true;
                Logger("Evap is negative!" + " @ cell: " + cell.Index);
            }
            
            //Logger("Starting LF calcs");
            #region LayerFractionCalcs
            //-----------------LAYER FRACTION CALCS----------------\\

            //Layer Fraction calcs
            LFStrato[stratoCount - 1] = 1.0f;
            for (int i = 0; i < layerCount; i++) //bottom up one for Layer Fractions
            {
                WeatherCell wCellLive = PD.LiveMap[i][cell];
                if (i == layerCount - 1)
                {
                    LF[i] = (wCellLive.pressure - PD.LiveStratoMap[0][cell].pressure) / PD.LiveMap[0][cell].pressure;
                }
                else
                {
                    LF[i] = (wCellLive.pressure - PD.LiveMap[i + 1][cell].pressure) / PD.LiveMap[0][cell].pressure;
                }
                LFStrato[stratoCount - 1] -= LF[i];

            }
            //Do strato calc
            for (int AltLayer = 0; AltLayer < stratoCount; AltLayer++)
            {
                WeatherCell wCellLive = PD.LiveStratoMap[AltLayer][cell];
                if (AltLayer != stratoCount-1)
                {
                    LFStrato[AltLayer] -= (wCellLive.pressure - PD.LiveStratoMap[AltLayer+1][cell].pressure) / PD.LiveMap[0][cell].pressure;
                    LFStrato[stratoCount - 1] -= LFStrato[AltLayer];
                }
            }
            // Compute the SWA, IRA Factors (Note: compliant with Temp_dynamic model sheet)
            //Note: SWA Factors average ~= 0.27; IRA Factors average = 0.55 (averages in accordance with real Earth atmospheric models: http://agsys.cra-cin.it/tools/solarradiation/help/Clear_sky_transmissivity.html, https://commons.wikimedia.org/wiki/File:Atmospheric.transmittance.IR.jpg); but lower layers have larger factors
            float temp = 0;
            for (int AltLayer = stratoCount-1; AltLayer >= 0; AltLayer--)
            {
                temp += LFStrato[AltLayer];
                IRAStrato_Mult[AltLayer] = temp;
                SWAStrato_Mult[AltLayer] = temp / 2;
            }
            for (int AltLayer = layerCount-1; AltLayer >=0; AltLayer--)
            {
                temp += LF[AltLayer];
                IRA_Mult[AltLayer] = temp;
                SWA_Mult[AltLayer] = temp / 2;
            }

            #endregion
            //Logger("LF complete");
            #region Thermal SW + IR calcs

            //SW calcs
            float SunriseFactor = WeatherFunctions.GetSunriseFactor(PD.index, cell);
            SWT = PD.irradiance * SunriseFactor;
            
            for (int AltLayer = stratoCount - 1; AltLayer >= 0; AltLayer--) //assumed 1 strato layer currently
            {
                thermalCapStrato[AltLayer] = PD.atmoData.specificHeatGas * PD.LiveMap[0][cell].pressure * LFStrato[AltLayer] / (float)CellUpdater.G(PD.index, ((float)((AltLayer+layerCount)*DeltaAltitude))) * WeatherSettings.SD.AtmoThCapMult;

                SWRStrato[AltLayer] = 0;
                SWAStrato[AltLayer] = SWT * LFStrato[AltLayer] * SWAStrato_Mult[AltLayer];
                SWXStrato[AltLayer] = SWT - SWAStrato[AltLayer];
                if (thermalCapStrato[AltLayer] == 0)
                {
                    KWSerror = true;
                    Logger("ThermalCap is 0" + " @ cell: " + cell.Index);
                }
            }
            for (int AltLayer = layerCount - 1; AltLayer >= 0; AltLayer--) //top down for SW stuff
            {
                WeatherCell wCellLive = PD.LiveMap[AltLayer][cell];

                thermalCap[AltLayer] = (PD.atmoData.specificHeatGas * (1 - wCellLive.relativeHumidity) +
                        PD.dewData.cg * wCellLive.relativeHumidity) * PD.LiveMap[0][cell].pressure * LF[AltLayer] / (float)CellUpdater.G(PD.index, ((float)(AltLayer * DeltaAltitude))) * WeatherSettings.SD.AtmoThCapMult;
                if (AltLayer == layerCount - 1)
                {
                    SWR[AltLayer] = SWXStrato[0] * Cl_SWR[AltLayer] * ReflFunc[AltLayer];
                    SWA[AltLayer] = (SWXStrato[0] - SWR[AltLayer]) * (LF[AltLayer] * SWA_Mult[AltLayer] + Cl_SWA[AltLayer]);
                    SWX[AltLayer] = SWXStrato[0] - SWR[AltLayer] - SWA[AltLayer];
                }
                else
                {
                    SWR[AltLayer] = SWX[AltLayer + 1] * Cl_SWR[AltLayer] * ReflFunc[AltLayer];
                    SWA[AltLayer] = (SWX[AltLayer + 1] - SWR[AltLayer]) * (LF[AltLayer] * SWA_Mult[AltLayer] + Cl_SWA[AltLayer]);
                    SWX[AltLayer] = SWX[AltLayer + 1] - SWR[AltLayer] - SWA[AltLayer];
                }

                if (thermalCap[AltLayer] == 0)
                {
                    KWSerror = true;
                    Logger("ThermalCap is 0" + " @ cell: " + cell.Index);
                }
            }
            //Soil thermal layer calc
            {
                thermalCapSoil = PD.biomeDatas[WeatherFunctions.GetBiome(PD.index, cell)].SoilThermalCap * WeatherSettings.SD.SoilThCapMult;
                // LiquidSoil Reflection (Fresnel Law, incident light is partly reflected out in air, partly refracted within water)
                SoilReflection = 0f;
                if (PD.biomeDatas[WeatherFunctions.GetBiome(PD.index, cell)].FLC > 0.9) // biome must be very shiny for reflection, liquid surfaces do
                {
                    SoilReflection = calcSoilRefractiveIndex(PD.index, cell, PD.atmoData.n1, PD.dewData.n1);
                }
                SWRSoil = SWX[0] * Math.Max(PD.biomeDatas[WeatherFunctions.GetBiome(PD.index, cell)].Albedo, SoilReflection);
                SWASoil = SWX[0] - SWRSoil;
                if (thermalCapSoil == 0)
                {
                    KWSerror = true;
                    Logger("ThermalCap is 0" + " @ cell: " + cell.Index);
                }
            }

            //Longwave calcs
            //bottom up
            //soil IR calcs
            {
                SoilCell soilCell = PD.LiveSoilMap[cell];
                IRGSoil = (float)PhysicsGlobals.StefanBoltzmanConstant * ToTheFourth(soilCell.temperature) * WeatherSettings.SD.SoilIRGFactor;
                IRAUSoil = 0;
                IRRSoil = 0;
                
            }
            float IRGtemp = IRGSoil, IRAUtemp = 0f, IRRtemp = 0f;
            for (int AltLayer = 0; AltLayer < layerCount; AltLayer++)
            {
                WeatherCell wCellLive = PD.LiveMap[AltLayer][cell];
                IRG[AltLayer] = (float)PhysicsGlobals.StefanBoltzmanConstant * ToTheFourth(wCellLive.temperature) * LF[AltLayer] * WeatherSettings.SD.AtmoIRGFactor;
                
                if (AltLayer == 0)
                {
                    IRAU[AltLayer] = IRGtemp * LF[AltLayer] * (IRA_Mult[AltLayer] + Cl_IRA[AltLayer]);
                }
                else
                {
                    IRAU[AltLayer] = (IRGtemp - IRAUtemp) * LF[AltLayer] * (IRA_Mult[AltLayer] + Cl_IRA[AltLayer]);
                }
                IRAUtemp += IRAU[AltLayer];
                IRR[AltLayer] = (IRGtemp - IRAUtemp - IRRtemp) * Cl_IRR[AltLayer];
                IRGtemp += IRG[AltLayer];
                IRRtemp += IRR[AltLayer];

                if (float.IsNaN(IRG[AltLayer]))
                {
                    KWSerror = true;
                    Logger("IRG is NaN" + " @ cell: " + cell.Index);
                }
                if (float.IsInfinity(IRG[AltLayer]))
                {
                    KWSerror = true;
                    Logger("IRG is Infinity" + " @ cell: " + cell.Index);
                }
                if (float.IsNaN(IRAU[AltLayer]))
                {
                    KWSerror = true;
                    Logger("IRAU is NaN" + " @ cell: " + cell.Index);
                }
                if (float.IsInfinity(IRAU[AltLayer]))
                {
                    KWSerror = true;
                    Logger("IRAU is Infinity" + " @ cell: " + cell.Index);
                }
            }
            //Strato layers IR calcs
            for (int AltLayer = 0; AltLayer < stratoCount; AltLayer++)
            {
                WeatherCell wCellLive = PD.LiveStratoMap[AltLayer][cell];
                IRGStrato[AltLayer] = (float)PhysicsGlobals.StefanBoltzmanConstant * ToTheFourth(wCellLive.temperature) * LFStrato[AltLayer] * WeatherSettings.SD.AtmoIRGFactor;
                IRAUStrato[AltLayer] = (IRGtemp - IRAUtemp) * LFStrato[AltLayer] * (IRAStrato_Mult[AltLayer]);
                IRAUtemp += IRAUStrato[AltLayer];
                IRRStrato[AltLayer] = 0f; 
                IRGtemp += IRGStrato[AltLayer];
                IRRtemp += IRRStrato[AltLayer];
                
                if (float.IsNaN(IRGStrato[AltLayer]))
                {
                    KWSerror = true;
                    Logger("IRG is NaN" + " @ cell: " + cell.Index);
                }
                if (float.IsInfinity(IRGStrato[AltLayer]))
                {
                    KWSerror = true;
                    Logger("IRG is Infinity" + " @ cell: " + cell.Index);
                }
                if (float.IsNaN(IRAUStrato[AltLayer]))
                {
                    KWSerror = true;
                    Logger("IRAU is NaN" + " @ cell: " + cell.Index);
                }
                if (float.IsInfinity(IRAUStrato[AltLayer]))
                {
                    KWSerror = true;
                    Logger("IRAU is Infinity" + " @ cell: " + cell.Index);
                }
            }
            //more longwave stuff, Infrared absorbed from downward cycle
            //top down
            float IRADtemp = 0f;
            for (int AltLayer = stratoCount - 1; AltLayer >= 0; AltLayer--) //strato layers
            {
                IRADStrato[AltLayer] = 0;
            }
            for (int Altlayer = 0; Altlayer < layerCount; Altlayer++) //tropo layers
            {
                IRRtemp = 0f;
                for (int i = Altlayer + 1; i < PD.LiveMap.Count; i++) { IRRtemp += IRR[i]; }
                IRAD[Altlayer] = IRRtemp * (LF[Altlayer] * (IRA_Mult[Altlayer] + Cl_IRA[Altlayer]));
                IRADtemp += IRAD[Altlayer];
            }
            {//soil layer
                IRADSoil = IRADtemp - IRRtemp;
            }
            #endregion
            //Logger("Thermal done");

            #region Dew Density
            //calc the density of the dew

            for (int AltLayer = 0; AltLayer < layerCount; AltLayer++)
            {

                WeatherCell wCellLive = PD.LiveMap[AltLayer][cell];
                D_dew[AltLayer] = wCellLive.pressure * PD.dewData.M / wCellLive.temperature / UGC;

            }
            #endregion
            //Logger("Dew density done");
            //get Z_dew_dT
            float Z_dew_dT = 0;
            if (Evap > DBL_Epsilon) 
            {
                Z_dew_dT = (float)Math.Sqrt((1 / D_dew[0] - 1 / D_dry[0]) * Evap * 90000f * (DeltaTime) * CellUpdater.G(PD.index, 0));
            }
            if (float.IsNaN(Z_dew_dT))
            {
                KWSerror = true;
                Logger("ZDewT is NaN" + " @ cell: " + cell.Index);
            }
            if (float.IsInfinity(Z_dew_dT))
            {
                KWSerror = true;
                Logger("ZDewT is Infinite" + " @ cell: " + cell.Index);
            }

            //get the correctional evap
            float evap_corr = 0;
            if (Z_dew_dT > 0)
            {
                double result = 0;
                double AHthing = 0;
                double bottomThing = 0;
                double evapThing = 0;
                if (AHDP[0] > AH[0])  // would otherwise be 0/0, though the limit of evap_corr for AH -> AHDP still is 0
                {
                    AHthing = AHDP[0] - AH[0];
                    bottomThing = 2.0 * Z_dew_dT * (AHthing);
                    evapThing = 1.0 - Evap * DeltaTime/ bottomThing;
                    result = Evap * evapThing;
                    evap_corr = (float)result;
                }
                if (evap_corr < -FLT_Epsilon)
                {
                    KWSerror = true;
                    Logger("EvapCorr is too small" + " @ cell: " + cell.Index);
                }
                if (float.IsInfinity(evap_corr))
                {
                    KWSerror = true;
                    Logger("EvapCorr is Infinity" + " @ cell: " + cell.Index);
                }
                if (float.IsNaN(evap_corr))
                {
                    KWSerror = true;
                    Logger("EvapCorr is NaN" + " @ cell: " + cell.Index);
                }
            }
            double Q_evap = evap_corr * PD.dewData.he; //heat required for evap

            #region N_dew, N_cond, N_sscond
            //Get Ws_evap, N_dew, N_sscond, N_Prec_p, N_Prec
            for (int AltLayer = 0; AltLayer < layerCount; AltLayer++)
            {
                // Ws_evap[AltLayer] = (D_dry[AltLayer] - D_wet[AltLayer]) * G * (float)(DeltaTime);  // deprecated
                //TODO: for further accuracy, include Diffusion of water molecules in the N_dew equations (Diffusion is ~ 5E-4 the amount of N_dew change due to buoyancy, already included)
                /* Diffusion (https://en.wikipedia.org/wiki/Diffusion, https://en.wikipedia.org/wiki/Flux#Chemical_diffusion)
                 * double particleDensity = D_dry[AltLayer] * UGC / PhysicsGlobals.BoltzmannConstant;
                 * double collisionCrossSection = 6.93963681724221E-022 // this is for water vapor in air (take it or not, had to be computed from tabled data but is invariant; other gases will require separate calc)
                 * double FreeMeanPath = 2d/3d/particleDensity/collisionCrossSection;
                 * MeanVelocity = Math.sqrt(PhysicsGlobals.BoltzmannConstant * wCellLive.temperature / Math.PI / PD.dewData.M * PhysicsGlobals.BoltzmannConstant / UGC);
                 * Diffusivity = FreeMeanPath * MeanVelocity;
                 * concentration = AH[AltLayer]/PD.dewData.M;
                 * Flux[AltLayer] = - Diffusivity * concentration/ DeltaAltitude* PD.dewData.M;
                 * NOTE: Flux is out of each layer. Net Flux in each layer boundary (i, j) comes from Flux[i]-Flux[j]
                 * Each layer N_dew[i] = N_dew[i] - NetFlux[i,j]* DeltaTime / DeltaAltitude;
                */
                double FreeMeanPath = 2.0 * PhysicsGlobals.BoltzmannConstant / (3.0 * D_wet[AltLayer] * UGC * 6.93963681724221E-022);
                double MeanVelocity = Math.Sqrt(PD.LiveMap[AltLayer][cell].temperature / Math.PI / PD.dewData.M / UGC); //TODO: review equation, works but may not be correct
                Flux[AltLayer] = -FreeMeanPath * MeanVelocity * AH[AltLayer] / DeltaAltitude;
            }
            for (int AltLayer = 0; AltLayer < layerCount; AltLayer++)
            {
                WeatherCell wCellLive = PD.LiveMap[AltLayer][cell];
                
                // computing H_adv_V
                float wsV_below = (AltLayer > 0) ? (PD.LiveMap[AltLayer - 1][cell].windVector.y + wCellLive.windVector.y) / 2 : 0;
                float wsV_above = (AltLayer < layerCount-1) ? (PD.LiveMap[AltLayer + 1][cell].windVector.y + wCellLive.windVector.y) / 2 : 0;
                double H_adv_V = 0;

                if (wsV_above != 0)
                {
                    double TimeIntegralH = 1 - Math.Exp(-Math.Abs(wsV_above) * DeltaTime / DeltaAltitude);
                    H_adv_V = TimeIntegralH * (AH[AltLayer + 1] - AH[AltLayer]);
                }
                if (wsV_below != 0)
                {
                    double TimeIntegralH = 1 - Math.Exp(-Math.Abs(wsV_below) * DeltaTime / DeltaAltitude);
                    H_adv_V += TimeIntegralH * (AH[AltLayer - 1] - AH[AltLayer]);
                }

                if (wCellLive.cloud.dropletSize != 0)  // dropletsAsCCN allows to include the surface of droplets and ice crystals in clouds for condensation to occur
                {
                    double avgDropSize = (WeatherFunctions.AverageDropletSize(wCellLive.cloud.dropletSize, wCellLive.cloud.rainyDecay, wCellLive.cloud.rainyDuration));
                    if(avgDropSize == 0)
                        dropletsAsCCN[AltLayer] = 0;
                    else
                        dropletsAsCCN[AltLayer] = (float)Math.Max(1.0f, (Math.Abs(wCellLive.cloud.cDew) + Math.Abs(wCellLive.cloud.dDew) * 10f) / avgDropSize / 3.0f / K_N_DROP);
                }
                if (AltLayer == layerCount-1)
                {
                    // N_dew[AltLayer] = (float)(AH[AltLayer] + (AH[AltLayer - 1] / D_dry[AltLayer - 1]) * CellUpdater.G(PD.index, AltLayer*(float)DeltaAltitude) * (PD.atmoData.M - PD.dewData.M) / DeltaAltitude * DeltaTime);
                    N_dew[AltLayer] = (float)(AH[AltLayer] + (H_adv_V + Flux[AltLayer] - Flux[AltLayer - 1]) / DeltaAltitude * DeltaTime);

                    N_cond[AltLayer] = Mathf.Max(0, (N_dew[AltLayer] - AHDP[AltLayer]) * (wCellLive.temperature > PD.dewData.T_fr ? 
                        Math.Min(1.0f, wCellLive.CCN + dropletsAsCCN[AltLayer]) : 1));

                    if (float.IsNaN(N_cond[AltLayer]))
                    {
                        KWSerror = true;
                        Logger("N_Cond is NaN" + " @ cell: " + cell.Index);
                    }

                    N_dew[AltLayer] = N_dew[AltLayer] - N_cond[AltLayer];
                    if (N_dew[AltLayer] < 0)
                    {
                        KWSerror = true;
                        Logger("N_Dew < 0" + " @ cell: " + cell.Index);
                    }

                    N_sscond[AltLayer] = Mathf.Max(0, N_dew[AltLayer] - 4 * AHDP[AltLayer]);
                    if (float.IsNaN(N_sscond[AltLayer]))
                    {
                        KWSerror = true;
                        Logger("N_ssCond is NaN" + " @ cell: " + cell.Index);
                    }
                }
                else if (AltLayer != 0) //middle layer
                {
                    // N_dew[AltLayer] = (float)(AH[AltLayer] + (AH[AltLayer-1] / D_dry[AltLayer - 1] - AH[AltLayer+1] / D_dry[AltLayer+1]) * CellUpdater.G(PD.index, AltLayer * (float)DeltaAltitude) * (PD.atmoData.M - PD.dewData.M) / DeltaAltitude * DeltaTime);
                    N_dew[AltLayer] = (float)(AH[AltLayer] + (H_adv_V + 2 * Flux[AltLayer] - Flux[AltLayer - 1] - Flux[AltLayer+1]) / DeltaAltitude * DeltaTime);

                    if (N_dew[AltLayer] < 0)
                    {
                        KWSerror = true;
                        Logger("N_Dew is negative!" + " @ cell: " + cell.Index);
                    }
                    if (float.IsNaN(N_dew[AltLayer]))
                    {
                        KWSerror = true;
                        Logger("NDew is NaN!" + " @ cell: " + cell.Index);
                    }
                    if (float.IsInfinity(N_dew[AltLayer]))
                    {
                        KWSerror = true;
                        Logger("N_Dew is Infinity" + " @ cell: " + cell.Index);
                    }

                    N_cond[AltLayer] = Mathf.Max(0, (N_dew[AltLayer] - AHDP[AltLayer]) * (wCellLive.temperature > PD.dewData.T_fr ? Math.Min(1.0f, wCellLive.CCN + dropletsAsCCN[AltLayer]) : 1));

                    if (float.IsNaN(N_cond[AltLayer]))
                    {
                        KWSerror = true;
                        Logger("N_Cond is NaN" + " @ cell: " + cell.Index);
                    }

                    N_dew[AltLayer] = N_dew[AltLayer] - N_cond[AltLayer];
                    if(N_dew[AltLayer] < 0)
                    {
                        KWSerror = true;
                        Logger("N_Dew < 0" + " @ cell: " + cell.Index);
                    }

                    N_sscond[AltLayer] = Mathf.Max(0, N_dew[AltLayer] - 4 * AHDP[AltLayer]);
                    if (float.IsNaN(N_sscond[AltLayer]))
                    {
                        Logger("N_ssCond is NaN" + " @ cell: " + cell.Index);
                    }
                }
                else //bottom layer
                {

                    // N_dew[AltLayer] = (float)(AH[AltLayer] + (evap_corr - AH[AltLayer + 1] / D_dry[AltLayer + 1] * CellUpdater.G(PD.index, AltLayer * (float)DeltaAltitude) * (PD.atmoData.M - PD.dewData.M)) * DeltaTime / DeltaAltitude);
                    N_dew[AltLayer] = (float)(AH[AltLayer] + (evap_corr + H_adv_V + Flux[AltLayer] - Flux[AltLayer + 1]) / DeltaAltitude * DeltaTime);

                    if (N_dew[AltLayer] < 0)
                    {
                        KWSerror = true;
                        Logger("N_Dew is negative!" + " @ cell: " + cell.Index);
                    }
                    if (float.IsNaN(N_dew[AltLayer]))
                    {
                        KWSerror = true;
                        Logger("N_Dew is NaN" + " @ cell: " + cell.Index);
                    }

                    N_cond[AltLayer] += Mathf.Max(0, (N_dew[AltLayer] - AHDP[AltLayer]));

                    if (float.IsNaN(N_cond[AltLayer]))
                    {
                        KWSerror = true;
                        Logger("N_Cond is NaN" + " @ cell: " + cell.Index);
                    }

                    N_dew[AltLayer] = N_dew[AltLayer] - N_cond[AltLayer];
                    if (N_dew[AltLayer] > AHDP[AltLayer])
                    {
                        if (N_dew[AltLayer] - AHDP[AltLayer] < FLT_Epsilon)
                        {
                            N_dew[AltLayer] = AHDP[AltLayer]; //FP error compensation
                        }
                        else
                        {
                            KWSerror = true;
                            Logger("NDew > AHDP" + " @ cell: " + cell.Index);
                        }
                    }

                    N_sscond[AltLayer] = 0;
                    if (float.IsNaN(N_sscond[AltLayer]))
                    {
                        KWSerror = true;
                        Logger("N_ssCond is NaN" + " @ cell: " + cell.Index);
                    }
                }

                N_dew[AltLayer] = N_dew[AltLayer] - N_sscond[AltLayer];
                if (N_dew[AltLayer] > AHDP[AltLayer] && AltLayer == 0)
                {
                    KWSerror = true;
                    Logger("N_Dew > AHDP" + " @ cell: " + cell.Index);
                }
                else if (N_dew[AltLayer] > AHDP[AltLayer] * 4)
                {
                    KWSerror = true;
                    Logger("N_Dew > AHDP*4" + " @ cell: " + cell.Index);
                }
                if (N_dew[AltLayer] < 0)
                {
                    KWSerror = true;
                    Logger("N_Dew is negative" + " @ cell: " + cell.Index);
                }
            }
            #endregion

            //update ew
            for (int layer = 0; layer < layerCount; layer++)
            {
                WeatherCell wCell = PD.LiveMap[layer][cell];
                ew[layer] = N_dew[layer] * wCell.temperature * UGC / PD.dewData.M;
                if (float.IsNaN(ew[layer]))
                {
                    KWSerror = true;
                    Logger("ew is NaN" + " @ cell: " + cell.Index);
                }
            }


            #region Q_cond
            //Get Q_cond
            for (int AltLayer = 0; AltLayer < layerCount; AltLayer++)
            {
                WeatherCell wCellLive = PD.LiveMap[AltLayer][cell];

                Q_cond[AltLayer] = wCellLive.temperature > PD.dewData.T_m ? PD.dewData.he * (N_cond[AltLayer] + N_sscond[AltLayer])
                    : (PD.dewData.he + PD.dewData.hm) * (N_cond[AltLayer] + N_sscond[AltLayer]);

                if (float.IsNaN(Q_cond[AltLayer]))
                {
                    KWSerror = true;
                    Logger("Q_Cond is NaN" + " @ cell: " + cell.Index);
                }
            }
            #endregion
            //Logger("Q_cond done");



            #region TEMP
            //temperature = Math.Max(0, Layer(n).Heat / Layer(n).ThermalCap)
            //heat = thermalCap * cell.temperature + (SWA + IRAU - IRG + Q_cond) * dT
            //Q = thermalCap[AltLayer] * wCellLive.temperature + (SWA[AltLayer]);
            //float tempTemperature = Mathf.Max(0, Q / thermalCap[AltLayer]);
            {//soil temperature
                SoilCell wCellLive = PD.LiveSoilMap[cell];
                QSoil = (float)(thermalCapSoil * wCellLive.temperature + (SWASoil + IRAUSoil - IRGSoil + IRADSoil - Q_evap + Q_cond_Soil) * DeltaTime);
            }
            for (int AltLayer = 0; AltLayer < layerCount; AltLayer++)
            {
                WeatherCell wCellLive = PD.LiveMap[AltLayer][cell];
                Q[AltLayer] = thermalCap[AltLayer] * wCellLive.temperature + (SWA[AltLayer] + IRAU[AltLayer] - IRG[AltLayer] + IRAD[AltLayer] + Q_cond[AltLayer])
                    * (float)DeltaTime;
            }
            for (int AltLayer = 0; AltLayer < stratoCount; AltLayer++)
            {
                WeatherCell wCellLive = PD.LiveStratoMap[AltLayer][cell];
                QStrato[AltLayer] = thermalCapStrato[AltLayer] * wCellLive.temperature + (SWAStrato[AltLayer] + IRAUStrato[AltLayer]
                    - IRGStrato[AltLayer] + IRADStrato[AltLayer]) * (float)DeltaTime;
            }
            //Logger("Q done");

            //FINALLY GET THE TEMP
            {//soil temp
                SoilCell wCell = PD.BufferSoilMap[cell];
                wCell.temperature = Mathf.Max(2.725f, QSoil / thermalCapSoil);
                if (float.IsNaN(wCell.temperature))
                {
                    KWSerror = true;
                    Logger("Temp is NaN" + " @ cell: " + cell.Index);
                }
                if (wCell.temperature < 0)
                {
                    KWSerror = true;
                    Logger("It's mighty chilling out here Frank" + " @ cell: " + cell.Index);
                }
                PD.BufferSoilMap[cell] = wCell;
            }
            for (int AltLayer = 0; AltLayer < layerCount; AltLayer++)
            {
                WeatherCell wCell = PD.BufferMap[AltLayer][cell];
                wCell.temperature = Q[AltLayer] / thermalCap[AltLayer];
                if (wCell.temperature < 2.725)
                {
                    KWSerror = true;
                    Logger("It's mighty chilling out here Frank" + " @ cell: " + cell.Index);
                }
                if (float.IsNaN(wCell.temperature))
                {
                    KWSerror = true;
                    Logger("Temp is NaN" + " @ cell: " + cell.Index);
                }

                PD.BufferMap[AltLayer][cell] = wCell;
                /*
                if (cell.Index == 6664 && AltLayer == 0)
                {
                    Logger("KSC Temp: " + wCell.temperature);
                }
                */
            }
            for (int AltLayer = 0; AltLayer < stratoCount; AltLayer++)
            {
                WeatherCell wCell = PD.BufferStratoMap[AltLayer][cell];
                wCell.temperature = Mathf.Max(2.725f, QStrato[AltLayer] / thermalCapStrato[AltLayer]);
                if (float.IsNaN(wCell.temperature))
                {
                    KWSerror = true;
                    Logger("Temp is NaN" + " @ cell: " + cell.Index);
                }
                if (float.IsInfinity(wCell.temperature))
                {
                    KWSerror = true;
                    Logger("Temp is infinity" + " @ cell: " + cell.Index);
                }
                if (wCell.temperature < 2.725)
                {
                    KWSerror = true;
                    Logger("It's mighty chilling out here Frank" + " @ cell: " + cell.Index);
                }
                PD.BufferStratoMap[AltLayer][cell] = wCell;
            }
            #endregion
                        
            //Calc wind vector
            #region windCalcs

            for (int layer = 0; layer < layerCount; layer++)
            {
                WeatherCell wCell = PD.LiveMap[layer][cell];

                // Horizontal wind
                wsDiv[layer] = 0;
                int n = 0;
                DeltaDistance_Avg[layer] = 0.0;
                float Pressure_eq = 0;
                float cell_Z = WeatherFunctions.GetCellAltitude(PD.index, layer, cell);
                DP[layer] = new float[cell.GetNeighbors(PD.gridLevel).ToList().Count];
                foreach (Cell neighbor in cell.GetNeighbors(PD.gridLevel))
                {
                    WeatherCell wCellNeighbor = PD.LiveMap[layer][neighbor];
                    // double DeltaDistance = WeatherFunctions.GetDistanceBetweenCells(PD.index, cell, neighbor, WeatherFunctions.GetCellAltitude(PD.index, layer, cell));
                    double DeltaDistance = WeatherFunctions.GetDistanceBetweenCells(PD.index, PD.LiveSoilMap[cell].centroid, PD.LiveSoilMap[neighbor].centroid, WeatherFunctions.GetCellAltitude(PD.index, layer, cell));
                    DeltaDistance_Avg[layer] += DeltaDistance;
                    double CosDir = Math.Cos(direction[n]);
                    double SinDir = Math.Sin(direction[n]);
                    float neigh_Z = WeatherFunctions.GetCellAltitude(PD.index, layer, neighbor);
                    Pressure_eq = 0;
                    if (layer == 0)
                    {
                        Pressure_eq = wCellNeighbor.pressure;
                    }
                    else
                    {
                        Pressure_eq = (float)(wCellNeighbor.pressure * Math.Exp( (neigh_Z - cell_Z) 
                            / WeatherFunctions.SH(PD.index, layer * (float)DeltaAltitude, PD.LiveMap[layer - 1][neighbor].temperature)));  // all pressures need be reduced at the same altitude (that of the current cell, layer) before comparing them
                    }
                    float deltaPressure = (float)((wCell.pressure - Pressure_eq) / DeltaDistance);

                    DP[layer][n] = deltaPressure;
                    Divg[layer] += deltaPressure;

                    wsN[layer] += (float)(deltaPressure * CosDir);
                    wsE[layer] += (float)(deltaPressure * SinDir);

                    wsDiv[layer] += wCellNeighbor.windVector.x * CosDir + wCellNeighbor.windVector.z * SinDir;  //NOTE: wsDiv is positive for wind exiting the cell
                    rotrStr[layer].y += (wCellNeighbor.windVector.y - wCell.windVector.y) / DeltaDistance;
                    rotrStr[layer].x -= wCellNeighbor.windVector.x * SinDir / DeltaDistance;  // + = CW; - = CCW
                    rotrStr[layer].z += wCellNeighbor.windVector.z * CosDir / DeltaDistance;
                    tensStr[layer].x += (wCellNeighbor.windVector.x - wCell.windVector.x) * Math.Abs(CosDir) / DeltaDistance;  // + = Northbound; - = Southbound 
                    tensStr[layer].z += (wCellNeighbor.windVector.z - wCell.windVector.z) * Math.Abs(SinDir) / DeltaDistance;  // + = Eastbound; - = Westbound

                    n += 1;

                }
                Divg[layer] /= n;
                wsN[layer] /= n;
                wsE[layer] /= n;
                DeltaDistance_Avg[layer] /= n;

                // tensor components on the x-z local plane due to airflow from adjacent cells
                tensStr[layer].x *= Math.Abs(wCell.windVector.x) * DeltaTime / n;
                tensStr[layer].z *= Math.Abs(wCell.windVector.z) * DeltaTime / n;  
                rotrStr[layer].x *= Math.Abs(wCell.windVector.x) * DeltaTime / n;
                rotrStr[layer].z *= Math.Abs(wCell.windVector.z) * DeltaTime / n;
                rotrStr[layer].y *= Math.Abs(wCell.windVector.y) * DeltaTime / n;  // rotrStr.y is used to smooth vertical wind differences; does not produce rotation


                Total_N[layer] = wCell.windVector.x;
                Total_E[layer] = wCell.windVector.z;

                // windspeed increase due to Coriolis forces
                float Cor_N = (float)(2 * Total_E[layer] * PD.body.angularV * Mathf.Sin(latitude * Mathf.Deg2Rad) * DeltaTime);  
                float Cor_E = (float)(-2 * Total_N[layer] * PD.body.angularV * Mathf.Sin(latitude * Mathf.Deg2Rad) * DeltaTime);

                // windspeed increase due to air viscosity (decrease, but takes sign opposite to the current wind)
                float Mu = Suth_B / (wCell.temperature + Suth_S) * Mathf.Pow(wCell.temperature, 3f / 2f);
                float Mu_dec_N = 0;  
                float Mu_dec_E = 0;
                if (layer == 0)
                {
                    Mu_dec_N = (float)(-Total_N[layer] * DeltaTime * Mu / D_wet[layer]);
                    Mu_dec_E = (float)(-Total_E[layer] * DeltaTime * Mu / D_wet[layer]);
                }
                else
                {
                    Mu_dec_N = (float)(-(Total_N[layer] - Total_N[layer - 1]) * (Total_N[layer] - Total_N[layer - 1]) * (Total_N[layer] - Total_N[layer - 1]) * DeltaTime * Mu / D_wet[layer]);
                    Mu_dec_E = (float)(-(Total_E[layer] - Total_N[layer - 1]) * (Total_E[layer] - Total_N[layer - 1]) * (Total_E[layer] - Total_N[layer - 1]) * DeltaTime * Mu / D_wet[layer]);
                }
                // windspeed increase due to pressure gradient
                float W_N_P = wsN[layer] * (float)DeltaTime / 2f / D_wet[layer];
                float W_E_P = wsE[layer] * (float)DeltaTime / 2f / D_wet[layer];

                // sum all the windspeed increases to the previous windspeed value
                Total_N[layer] += W_N_P + Cor_N + Mu_dec_N + (float)tensStr[layer].x - (float)rotrStr[layer].z;
                Total_E[layer] += W_E_P + Cor_E + Mu_dec_E + (float)tensStr[layer].z + (float)rotrStr[layer].x;

                // vertical wind
                // Buoyancy
                if (layer == 0)
                {
                    //buoyancy[0] = (D_dry[layer] - 2 * D_wet[layer] + D_wet[layer + 1] * wCell.pressure / PD.BufferMap[layer + 1][cell].pressure) * G / D_wet[layer];
                    buoyancy[0] = (D_wet[1]* wCell.pressure / PD.LiveMap[1][cell].pressure *PD.LiveMap[1][cell].temperature /wCell.temperature
                        - D_wet[0]);
                }
                else if (layer != layerCount - 1)
                {
                    //buoyancy[layer] = (D_dry[layer] - D_wet[layer] + D_wet[layer + 1] * wCell.pressure /PD.BufferMap[layer + 1][cell].pressure) * G / D_wet[layer];
                    buoyancy[layer] = ((D_wet[layer + 1] / PD.LiveMap[layer + 1][cell].pressure*PD.LiveMap[layer+1][cell].temperature
                        + D_wet[layer - 1] / PD.LiveMap[layer - 1][cell].pressure * PD.LiveMap[layer-1][cell].temperature) 
                        * wCell.pressure / wCell.temperature - 2 * D_wet[layer]);
                }
                else
                {
                    //buoyancy[layer] = (D_dry[layer] - D_wet[layer - 1] * wCell.pressure /PD.BufferMap[layer - 1][cell].pressure) * G / D_wet[layer];
                    float D_dry_strato = PD.LiveStratoMap[0][cell].pressure * PD.atmoData.M / UGC / PD.LiveStratoMap[0][cell].temperature;
                    D_dry_strato *= wCell.pressure / PD.LiveStratoMap[0][cell].pressure * PD.LiveStratoMap[0][cell].temperature / wCell.temperature;
                    buoyancy[layer] = (D_dry_strato + D_wet[layer - 1] * wCell.pressure / PD.LiveMap[layer - 1][cell].pressure 
                        * PD.LiveMap[layer-1][cell].temperature/wCell.temperature - 2* D_wet[layer]);
                }
                buoyancy[layer] *= CellUpdater.G(PD.index, layer*(float)DeltaAltitude) / D_wet[layer] / DeltaAltitude;

                // vertical pressure gradient (∇P)
                Pressure_eq = 0; // all pressures need be reduced at the same altitude (that of the current cell, layer) before comparing them
                DP_V[layer] = 0;
                // do layer below
                if (layer > 0)
                {
                    Pressure_eq = (float)(PD.LiveMap[layer-1][cell].pressure * Math.Exp((- DeltaAltitude)
                        / WeatherFunctions.SH(PD.index, layer * (float)DeltaAltitude, PD.LiveMap[layer - 1][cell].temperature)));  
                    DP_V[layer] = (float)((Pressure_eq - wCell.pressure) / (D_wet[layer - 1] * wCell.pressure 
                        / PD.LiveMap[layer - 1][cell].pressure + D_wet[layer]) / 2 / DeltaAltitude);
                }
                // do layer above
                if (layer < layerCount-1)
                {
                    Pressure_eq = (float)(PD.LiveMap[layer + 1][cell].pressure * Math.Exp((DeltaAltitude) / WeatherFunctions.SH(PD.index, (layer+1) * (float)DeltaAltitude, wCell.temperature)));  
                    DP_V[layer] += (float)((wCell.pressure - Pressure_eq) / (D_wet[layer + 1] * wCell.pressure 
                        / PD.LiveMap[layer + 1][cell].pressure + D_wet[layer]) / 2 / DeltaAltitude);
                }
                if (layer == layerCount - 1) // layer above when Strato
                {
                    float D_dry_strato = PD.LiveStratoMap[0][cell].pressure * PD.atmoData.M / UGC / PD.LiveStratoMap[0][cell].temperature;
                    Pressure_eq = (float)(PD.LiveStratoMap[0][cell].pressure * Math.Exp((DeltaAltitude) / WeatherFunctions.SH(PD.index, (layer + 1) * (float)DeltaAltitude, wCell.temperature)));
                    DP_V[layer] += (float)((wCell.pressure - Pressure_eq)/ (D_dry_strato * wCell.pressure 
                        / PD.LiveStratoMap[0][cell].pressure + D_wet[layer]) / 2 / DeltaAltitude);
                }

                Ws_V_ana[layer] = 0;  // TODO: here is where to compute anabatic wind (after orography)
                double wsVdiff = ((layer < layerCount - 1 ? (wCell.windVector.y - PD.LiveMap[layer + 1][cell].windVector.y) : 0) +
                    (layer > 0 ? (wCell.windVector.y - PD.LiveMap[layer - 1][cell].windVector.y) : 0));
                tensStr[layer].y = Math.Abs(wCell.windVector.y) / DeltaAltitude * wsVdiff; //wind * tensor gradient
                double TCtens = (wsVdiff == 0) ? 0 : (1.0 - Math.Exp(-Math.Abs(DeltaTime * tensStr[layer].y / wCell.windVector.y)));
                float wsV = (float)(wCell.windVector.y + (buoyancy[layer] + DP_V[layer] - (wCell.windVector.y * wCell.windVector.y * wCell.windVector.y
                    * Mu / D_wet[layer])) * DeltaTime / 2 - wsVdiff / 2 * TCtens + (float)rotrStr[layer].y + Ws_V_ana[layer]);

                WeatherCell wCellBuffer = PD.BufferMap[layer][cell];
                wCellBuffer.windVector = new Vector3(Total_N[layer], wsV, Total_E[layer]);
                PD.BufferMap[layer][cell] = wCellBuffer;
            }
            #endregion

            //calc the vertical divergence
            /*
            float posFactor = 0;
            for (int i = Divg.Length - 1; i >= 0; i--)
            {
                if (i >= Divg.Length / 2)
                {
                    posFactor += (i + 1 - Divg.Length / 2) * Divg[i];
                }
                else
                {
                    posFactor += (i - Divg.Length / 2) * Divg[i];
                }
            }
            float ws_V_div = K_DIVG * posFactor;
            */
            #region dynamicPressure
            for (int layer = 0; layer < layerCount; layer++)
            {
                WeatherCell wCellBuffer = PD.BufferMap[layer][cell];
                float wsV = wCellBuffer.windVector.y;
                float staticPressureChange = 0;
                float dynPressureAbove = 0;
                float dynPressureBelow = 0;
                float dynPressureLayer = 0;

                if(DeltaAltitude == 0 || double.IsNaN(DeltaAltitude))
                {
                    KWSerror = true;
                    Logger("DeltaAltitude went very wrong" + " @ cell: " + cell.Index);
                }
                if(double.IsNaN(DeltaTime))
                {
                    KWSerror = true;
                    Logger("DeltaTime went wrong" + " @ cell: " + cell.Index);
                }
                if(float.IsNaN(wsV))
                {
                    KWSerror = true;
                    Logger("wsV went wrong" + " @ cell: " + cell.Index);
                }
                double TimeChargeV = Math.Sign(wsV)*(1.0 - Math.Exp(-Math.Abs(DeltaTime * wsV / DeltaAltitude)));  // needed to stabilize V_disp from variance in DeltaTime
                double TimeChargeH = Math.Sign(wsDiv[layer])*(1.0 - Math.Exp((float)-Math.Abs(DeltaTime * wsDiv[layer] / DeltaDistance_Avg[layer])));  // needed to stabilize H_disp from variance in DeltaTime
                
                dynPressure[layer] = (float)(0.5f * D_wet[layer] * -wsDiv[layer] * Math.Abs(wsDiv[layer])); // horizontal dynamicPressure
                staticPressureChange = (float)(-TimeChargeH);  // static pressure change (%) due to horizontal flow
                
                if (layer > 0)  // go with layer below
                {
                    float wsV_below = PD.BufferMap[layer - 1][cell].windVector.y;
                    float wsV_avg = (wsV + wsV_below) / 2.0f;
                    dynPressureBelow = wsV_avg>0 ? 0.5f * D_wet[layer - 1] * wsV_avg * wsV_avg : 0; // dynamic pressure due to wind with layer below
                    double D_variance = TimeChargeV < 0 ? 1.0f : (float)((D_wet[layer] + D_wet[layer - 1] * TimeChargeV) / D_wet[layer]);
                    if (D_variance < 0)
                    {
                        KWSerror = true;
                        Logger("D_Variance went negative" + " @ cell: " + cell.Index);
                    }
                    staticPressureChange += (float)(Math.Pow(D_variance, k_ad) - 1.0f);  // static pressure change (%) due to wind with layer below
                    if (float.IsNaN(staticPressureChange))
                    {
                        KWSerror = true;
                        Logger("Static Pressure Change is NaN" + " @ cell: " + cell.Index);
                    }
                }
                if (layer < layerCount-1)  // go with layer above
                {
                    float wsV_above = PD.BufferMap[layer + 1][cell].windVector.y;
                    float wsV_avg = (wsV + wsV_above) / 2.0f;
                    dynPressureAbove = (wsV_avg<0 ? (0.5f * D_wet[layer + 1] * wsV_avg * wsV_avg) : 0);  // dynamic pressure due to wind with layer above
                    double D_variance = TimeChargeV > 0 ? 1.0f : (float)((D_wet[layer] - D_wet[layer + 1] * TimeChargeV) / D_wet[layer]);
                    if (D_variance < 0)
                    {
                        KWSerror = true;
                        Logger("D_Variance went negative" + " @ cell: " + cell.Index);
                    }
                    staticPressureChange += (float)(Math.Pow(D_variance, 1/k_ad) - 1.0f);  // static pressure change (%) due to wind with layer above
                    if (float.IsNaN(staticPressureChange))
                    {
                        KWSerror = true;
                        Logger("Static Pressure Change is NaN" + " @ cell: " + cell.Index);
                    }
                }
                if (layer == layerCount -1) // layer above when Strato
                {
                    double error = 0;
                    float D_dryStrato = (float)(WeatherFunctions.VdW(PD.atmoData, PD.LiveStratoMap[0][cell].pressure, PD.LiveStratoMap[0][cell].temperature, out error));
                    dynPressureAbove = (wsV < 0 ? (0.5f * D_dryStrato * wsV * wsV) : 0);  // dynamic pressure due to wind with layer above
                    double D_variance = TimeChargeV > 0 ? 1.0f : (float)((D_wet[layer] - D_dryStrato * TimeChargeV) / D_wet[layer]);
                    if (D_variance < 0)
                    {
                        KWSerror = true;
                        Logger("D_Variance went negative" + " @ cell: " + cell.Index);
                    }
                    staticPressureChange += (float)(Math.Pow(D_variance, 1/k_ad) - 1.0f);  // static pressure change (%) due to wind with layer above
                    if (float.IsNaN(staticPressureChange))
                    {
                        KWSerror = true;
                        Logger("Static Pressure Change is NaN" + " @ cell: " + cell.Index);
                    }
                }
                // go with this layer
                dynPressureLayer = (wsV > 0 || layer > 0) ? (0.5f * D_wet[layer] * wsV * wsV) : 0;
                staticPressureChange = (float)(PD.LiveMap[layer][cell].pressure * (staticPressureChange - ((wsV > 0 || layer > 0) ? Math.Abs(TimeChargeV) : 0)) / DeltaTime);
                float flowPChangeKept = 0.0f;  //TODO: find the correct value for any DeltaTime to keep wind from ever-increasing and from oscillating (0.76 = best for DT = 8.2s)
                wCellBuffer.flowPChange = wCellBuffer.flowPChange * flowPChangeKept + staticPressureChange;  
                dynPressure[layer] += dynPressureAbove + dynPressureBelow - dynPressureLayer + wCellBuffer.flowPChange;
                if (float.IsNaN(dynPressure[layer]))
                {
                    KWSerror = true;
                    Logger(" dynamicPressure went NaN" + " @ cell: " + cell.Index);
                }
                
                //calc V_disp
                float V_disp_limit = (float)(TimeChargeV / staticPressureChange * (wsV < 0 ? dynPressureAbove : dynPressureBelow));

                V_disp[layer] = (layer == 0 ? 0 : Mathf.Max(-V_disp_limit, Mathf.Min(V_disp_limit, (float)(wsV * DeltaTime / DeltaAltitude))));

                if (float.IsNaN(V_disp[layer]))
                {
                    KWSerror = true;
                    Logger("V_disp is NaN" + " @ cell: " + cell.Index);
                }
                PD.BufferMap[layer][cell] = wCellBuffer;
            }
            #endregion

            #region FRONTS
            //--------------------FRONTS----------------------\\


            for (int layer = 0; layer < layerCount; layer++)
            {
                WeatherCell wCell = PD.BufferMap[layer][cell];
                //foreach neighbors
                foreach (Cell neighbor in cell.GetNeighbors(PD.gridLevel))
                {
                    WeatherCell neighborWCell = PD.LiveMap[layer][neighbor];
                    // double DeltaDistance = WeatherFunctions.GetDistanceBetweenCells(PD.index, cell, neighbor, WeatherFunctions.GetCellAltitude(PD.index, layer, cell));
                    double DeltaDistance = WeatherFunctions.GetDistanceBetweenCells(PD.index, PD.LiveSoilMap[cell].centroid, PD.LiveSoilMap[neighbor].centroid, WeatherFunctions.GetCellAltitude(PD.index, layer, cell));
                    float H_adv = 0;
                    float T_adv = 0;
                    //we need to find the negative dot product of the two vectors: cellVectors, neighborWindVector
                    // Vector3 cellVector = cell.Position - neighbor.Position;
                    Vector3 cellVector = PD.LiveSoilMap[cell].centroid - PD.LiveSoilMap[neighbor].centroid;
                    float Ws_wC = -Vector3.Dot(neighborWCell.windVector, cellVector); //the amount fo wind coming to us

                    //calc D_wet_Diff
                    float Neighborew_eq = WeatherFunctions.getEwEq(PD.index, neighborWCell.temperature);
                    float neighborEw = Neighborew_eq * neighborWCell.relativeHumidity; //neighborWCell.N_Dew * neighborWCell.temperature * UGC / PD.dewData.M;
                    float neighborD_Wet = ((neighborWCell.pressure - neighborEw)
                        * PD.atmoData.M + neighborEw * PD.dewData.M) / (UGC * neighborWCell.temperature);


                    float D_wet_Diff = (neighborD_Wet - D_wet[layer]) / D_wet[layer];


                    if (Ws_wC > 0 && Mathf.Abs(D_wet_Diff) > D_DIFF)  // D_wet_Diff > D_DIFF means different enough airmasses to make a front
                    {
                        H_adv = Ws_wC * (neighborWCell.N_Dew - N_dew[layer]) * 0.8f; //0.8 because cold front moves faster
                        T_adv = Ws_wC * (neighborWCell.temperature - wCell.temperature) * 0.8f;
                        if (D_wet_Diff < 0) //warm front, takes the 0.8 and halves it essentially
                        {
                            H_adv /= 2;
                            T_adv /= 2;
                        }
                        H_adv_S[layer] += (float)(H_adv * DeltaTime / DeltaDistance);
                        T_adv_S[layer] += (float)(T_adv * DeltaTime / DeltaDistance);
                    }

                    if (Ws_wC > 0 && Mathf.Abs(D_wet_Diff) <= D_DIFF)  // D_wet_Diff <= D_DIFF means air is similar, normal advection
                    {
                        double TimeChargeH = Math.Sign(Ws_wC) * (1.0 - Math.Exp((float)-Math.Abs(DeltaTime * Ws_wC / DeltaDistance)));
                        H_adv_S[layer] += (float)(((neighborWCell.N_Dew * TimeChargeH) + N_dew[layer]) / (1 + TimeChargeH)) - N_dew[layer];
                        T_adv_S[layer] += (float)(((neighborWCell.temperature * TimeChargeH) + wCell.temperature) / (1 + TimeChargeH)) - wCell.temperature;
                    }
                }
            }


            //calc H_disp (horizontal displacement, advection rate due to incoming airmass)
            // TODO: introduce H_disp limit = dynPressure / staticPressureChange * TimeChargeH; dynPressure = 0.5f * D_wet[layer] * wsH * Math.Abs(wsH);
            for (int layer = 0; layer < layerCount; layer++)
            {
                if (layer == layerCount - 1) //top layer
                {
                    H_disp[layer] = N_dew[layer] * (1 - V_disp[layer]) + V_disp[layer] *
                        (N_dew[layer - 1] * (V_disp[layer] > 0 ? 1 : 0));
                }
                else if (layer == 0) //bottom layer
                {
                    H_disp[layer] = N_dew[layer] * (1 - V_disp[layer]) + V_disp[layer] *
                        (N_dew[layer + 1] * (V_disp[layer] > 0 ? 1 : 0));
                }
                else //middle layers
                {
                    H_disp[layer] = N_dew[layer] * (1 - V_disp[layer]) + V_disp[layer] * (N_dew[layer - 1]
                    * (V_disp[layer] > 0 ? 1 : 0) + N_dew[layer + 1] * (V_disp[layer] < 0 ? 1 : 0));
                }
            }

            //update N_Dew due to advection
            for (int layer = 0; layer < layerCount; layer++)
            {
                N_dew[layer] += H_adv_S[layer];

                if (N_dew[layer] < 0)
                {
                    KWSerror = true;
                    Logger("NDew is negative" + " @ cell: " + cell.Index);
                }
                if (float.IsNaN(N_dew[layer]))
                {
                    KWSerror = true;
                    Logger("NDew is NaN" + " @ cell: " + cell.Index);
                }

                //assign N_Dew to the cell
                WeatherCell wCell = PD.BufferMap[layer][cell];
                wCell.N_Dew = N_dew[layer];
                PD.BufferMap[layer][cell] = wCell;
            }
            // need for checks with ALR and Delta_T equations, to precalc updated RH since N_dew changed
            for (int i = 0; i < layerCount; i++)
            {
                RH[i] = (N_dew[i] - Math.Max(0, N_dew[i] - AHDP[i]) * (PD.LiveMap[i][cell].CCN + dropletsAsCCN[i])) / AHDP[i];
                if (PD.BufferMap[i][cell].temperature < 233f) { RH[i] = Mathf.Min(RH[i], 1f); }
                RH[i] = Mathf.Min(RH[i], 4f);
            }

            for (int layer = 0; layer < layerCount; layer++)
            {
                WeatherCell wCell = PD.BufferMap[layer][cell];


                if (RH[layer] <= 1.0f)
                {
                    ALR[layer] = -(float)CellUpdater.G(PD.index, ((float)(layer * DeltaAltitude))) / PD.atmoData.specificHeatGas; //Dry adiabatic lapse rate
                }
                else
                {
                    ALR[layer] = -(float)CellUpdater.G(PD.index, ((float)(layer * DeltaAltitude))) * (1 + PD.dewData.he * N_dew[layer] / D_dry[layer] / UGC * PD.atmoData.M / wCell.temperature)
                     / (PD.atmoData.specificHeatGas + (PD.dewData.he * PD.dewData.he * N_dew[layer] / D_dry[layer] / UGC * PD.dewData.M / wCell.temperature / wCell.temperature));
                    //Saturated (moist) Adiabatic Lapse Rate
                }
            }


            for (int layer = layerCount - 1; layer >= 0; layer--)
            {
                float Delta_T = 0;
                WeatherCell wCell = PD.BufferMap[layer][cell];
                float altitude = WeatherFunctions.GetCellAltitude(PD.index, layer, cell);

                if (layer == layerCount - 1) //if top layer
                {
                    if (RH[layer] > 1.0f)
                    {
                        Z_sat[layer] = (float)(altitude + DeltaAltitude * (1.0f - RH[layer]) / (0f - RH[layer]));

                        float ALR_sat = (float)(ALR[layer] - (CellUpdater.G(PD.index, layer*(float)DeltaAltitude) / PD.atmoData.specificHeatGas + ALR[layer])
                            * (Z_sat[layer] - altitude / DeltaAltitude));

                        Delta_T = (float)(((Z_sat[layer] - altitude) * ALR[layer])
                            + ((altitude + DeltaAltitude - Z_sat[layer])
                            * (ALR_sat - CellUpdater.G(PD.index, layer * (float)DeltaAltitude) / PD.atmoData.specificHeatGas) / 2f));
                    }
                    else
                    {
                        Delta_T = (ALR[layer] - (float)CellUpdater.G(PD.index, ((float)(layer * DeltaAltitude))) / PD.atmoData.specificHeatGas) / 2f;
                    }
                }
                else
                {
                    if ((RH[layer] > 1.0f) ^ (RH[layer+1] > 1.0f))//XOR because one or the other, but not both
                    {
                        Z_sat[layer] = (float)(altitude + DeltaAltitude *
                            (1.0f - RH[layer+1]) / (RH[layer] - RH[layer+1]));
                        float ALR_sat = (float)(ALR[layer] + (ALR[layer + 1] - ALR[layer]) * (Z_sat[layer] - altitude) / DeltaAltitude);
                        float ALR_btm = 0;
                        float ALR_up = 0;
                        if (RH[layer] < 1)
                        {
                            ALR_btm = ALR[layer];
                            ALR_up = (ALR[layer] + ALR_sat) / 2f;
                        }
                        else
                        {
                            ALR_btm = (ALR[layer] + ALR_sat) / 2f;
                            ALR_up = ALR[layer + 1];
                        }
                        Delta_T = (Z_sat[layer] - altitude) * ALR_btm
                            + (WeatherFunctions.GetCellAltitude(PD.index, layer + 1, cell) - Z_sat[layer]) * ALR_up;
                    }
                    else
                    {
                        Delta_T = (ALR[layer] + ALR[layer + 1]) / 2f;
                    }
                }
                float L = (216.65f - 288.15f) / PD.meanTropoHeight;
                Delta_T = (float) ((Delta_T/ DeltaAltitude - L) * PD.LiveMap[layer][cell].windVector.y * WeatherFunctions.GetDeltaTime(PD.index) / WeatherFunctions.GetDeltaLayerAltitude(PD.index, cell));
                

                /*
                  // TODO: enable D_wet_adb if needed
                  // in case Delta_T still doesn't provide the required vertical stability, D_wet has to be changed with adiabatics
                  // we compute D_wet_adb to be used to correct D_wet with the layer
                  // first law of thermodynamics, applied to the ideal gas law, brings to P/(D^(k)) = constant, where k = Cp/Cv ]
                  // Mayer relation gives Cp(spec) = Cv(spec) + nR(spec)
                  float H = PD.SHF * wCell.temperature;
                  float pressure_adb = (float)(PD.BufferMap[0][cell].pressure * Math.Exp(-(WeatherFunctions.GetCellAltitude(PD.index, layer, cell)+ PD.LiveMap[layer][cell].windVector.y) / H));
                  float D_wet_adb = (float)(D_wet[layer] * Math.Pow(pressure_adb/PD.LiveMap[layer][cell].pressure, k_ad));
                  D_wet[layer] += D_wet_adb * V_disp[layer]);  // this is where D_wet is updated due to adiabatic compression/expansion, if required
                */
                
                // update temperature due to advection
                T_disp[layer] = (Delta_T * V_disp[layer]);

                wCell.temperature += T_adv_S[layer] + T_disp[layer];
                if (float.IsNaN(wCell.temperature))
                {
                    KWSerror = true;
                    Logger("Temperature is NaN at T_Disp" + " @ cell: " + cell.Index);
                }
                if (wCell.temperature < 0)
                {
                    KWSerror = true;
                    Logger("Me fingahs are turnin blue" + " @ cell: " + cell.Index);
                }
                PD.BufferMap[layer][cell] = wCell; //reassign new temp
            }

            #endregion

            //Logger("Temp done");
            //and also update pressure and windvectors and humidities
            //also this may seem inefficient, and it may be, but we use the updated temp values for these calcs

            #region PressureUpdate
            for (int layer = 0; layer < layerCount; layer++)
            {
                WeatherCell wCell = PD.BufferMap[layer][cell];

                //RWEt = UGC * ((N_Dry / (N_Dry + N_Dew) / M_Air + (N_Dew / (N_Dry + N_dew) / M_Water)))
                //N_dry is the number of moles from pure air remaining in the air parcel, that is by differnce of the normal number of moles for a parcel - N_dew
                //UGC is the Universal Gas Constant
                //n_dry = N_dry in moles
                //n_dew = N_Dew in moles
                //n_total is the total number of moles in the parcel
                if (layer == 0)
                {

                    float ewSum = 0;  // TODO: re-evaluate ew contribution against temperature. High temp cells are the most affected, so where pressure changes most
                    for (int i = 0; i < layerCount; i++)
                    {
                        ewSum += ew[i]*LF[i];
                    }
                    float D_WetAvg = (float)(((FlightGlobals.getStaticPressure(0, PD.body) * 1000 - ewSum) * PD.atmoData.M + ewSum * PD.dewData.M) / (UGC * wCell.temperature));
                    float R_Air = UGC / PD.atmoData.M;
                    wCell.pressure = wCell.temperature * D_WetAvg * R_Air + dynPressure[layer];

                    if (float.IsNaN(wCell.pressure))
                    {
                        KWSerror = true;
                        Logger("Pressure is NaN" + " @ cell: " + cell.Index);
                    }
                    if (wCell.pressure < (0.9 * FlightGlobals.getStaticPressure(0, PD.body) * 1000))
                    {
                        KWSerror = true;
                        Logger("Pressure is too low" + " @ cell: " + cell.Index);
                    }
                }
                else
                {
                    SH[layer - 1] = (float)WeatherFunctions.SH(PD.index, (float)((layer) * DeltaAltitude), PD.BufferMap[layer - 1][cell].temperature);
                    wCell.pressure = (float)((PD.BufferMap[layer - 1][cell].pressure - dynPressure[layer-1]) * Math.Exp(-DeltaAltitude / SH[layer - 1])) + dynPressure[layer];
                    if (float.IsNaN(wCell.pressure))
                    {
                        KWSerror = true;
                        Logger("Pressure is NaN" + " @ cell: " + cell.Index);
                    }
                }
                if (wCell.pressure < 0)
                {
                    KWSerror = true;
                    Logger("Pressure is negative!" + " @ cell: " + cell.Index);
                    
                }

                PD.BufferMap[layer][cell] = wCell;
            }
            for (int layer = 0; layer < stratoCount; layer++)
            {
                WeatherCell wCell = PD.BufferStratoMap[layer][cell];
                if (layer == 0)
                {
                    double SH_strato = WeatherFunctions.SH(PD.index, (float)((layerCount + layer) * DeltaAltitude), PD.BufferMap[layerCount-1][cell].temperature);
                    wCell.pressure = (float)((PD.BufferMap[layerCount - 1][cell].pressure - dynPressure[layerCount - 1]) * Math.Exp(-DeltaAltitude / SH_strato));
                }
                else
                {
                    double SH_strato = WeatherFunctions.SH(PD.index, (float)((layerCount + layer) * DeltaAltitude), PD.BufferStratoMap[layer - 1][cell].temperature);
                    wCell.pressure = (float)(PD.BufferStratoMap[layer - 1][cell].pressure * Math.Exp(-DeltaAltitude / SH_strato));
                }
                if (float.IsNaN(wCell.pressure))
                {
                    KWSerror = true;
                    Logger("Strato Pressure is NaN" + " @ cell: " + cell.Index);
                }
                if (wCell.pressure < 0)
                {
                    KWSerror = true;
                    Logger("Strato Pressure is negative!" + " @ cell: " + cell.Index);

                }
                PD.BufferStratoMap[layer][cell] = wCell;
            }
            #endregion

            #region RedoHumidityCalcs
            //Humidity

            for (int AltLayer = 0; AltLayer < layerCount; AltLayer++)
            {

                float new_cond = Math.Max(0, N_dew[AltLayer] - AHDP[AltLayer]) * (PD.BufferMap[AltLayer][cell].temperature > PD.dewData.T_fr ? 
                    Math.Min(1.0f, PD.LiveMap[AltLayer][cell].CCN + dropletsAsCCN[AltLayer]) : 1);
                if (float.IsNaN(new_cond))
                {
                    KWSerror = true;
                    Logger("N_Cond2 is NaN" + " @ cell: " + cell.Index);
                }
                N_dew[AltLayer] = N_dew[AltLayer] - new_cond;
                N_cond[AltLayer] += new_cond;

                new_cond = Mathf.Max(0, N_dew[AltLayer] - 4 * AHDP[AltLayer]);
                N_sscond[AltLayer] += new_cond;

                if (float.IsNaN(N_sscond[AltLayer]))
                {
                    KWSerror = true;
                    Logger("N_ssCond is NaN" + " @ cell: " + cell.Index);
                }


                WeatherCell wCell = PD.BufferMap[AltLayer][cell];
                //re-update N_dew
                N_dew[AltLayer] = N_dew[AltLayer] - new_cond;
                
                RH[AltLayer] = N_dew[AltLayer] * wCell.temperature * UGC / PD.dewData.M / ew_eq[AltLayer];
                if (float.IsNaN(RH[AltLayer]))
                {
                    KWSerror = true;
                    Logger("Relative Humidity is NaN" + " @ cell: " + cell.Index);
                }
                if (RH[AltLayer] < 0)
                {
                    KWSerror = true;
                    Logger("Negative RH" + " @ cell: " + cell.Index);
                }
                if (RH[AltLayer] > 1.001 && AltLayer == 0)
                {
                    KWSerror = true;
                    Logger("RH at soil > 100%" + " @ cell: " + cell.Index);
                }
                else if (RH[AltLayer] > 1.0 && AltLayer == 0)
                {
                    RH[AltLayer] = 0.9999f; //this is because floating point errors cause RH to be juuuust above 100%
                }
                wCell.relativeHumidity = RH[AltLayer];
                
                wCell.N_Dew = N_dew[AltLayer];
                PD.BufferMap[AltLayer][cell] = wCell;
            }

            #endregion

            #region NewTemps
            //Calc new temps with the updated humdity stuff
            for (int AltLayer = 0; AltLayer < layerCount; AltLayer++)
            {
                WeatherCell wCell = PD.BufferMap[AltLayer][cell];

                Q_cond[AltLayer] = wCell.temperature > PD.dewData.T_m ? PD.dewData.he * (N_cond[AltLayer] + N_sscond[AltLayer])
                    : (PD.dewData.he + PD.dewData.hm) * (N_cond[AltLayer] + N_sscond[AltLayer]);

                if (float.IsNaN(Q_cond[AltLayer]))
                {
                    KWSerror = true;
                    Logger("Q_Cond is NaN" + " @ cell: " + cell.Index);
                }
            }

            for (int AltLayer = 0; AltLayer < layerCount; AltLayer++)
            {
                WeatherCell wCell = PD.LiveMap[AltLayer][cell];
                Q[AltLayer] = thermalCap[AltLayer] * wCell.temperature + (SWA[AltLayer] + IRAU[AltLayer] - IRG[AltLayer] + IRAD[AltLayer] + Q_cond[AltLayer])
                    * (float)(DeltaTime);
            }

            for (int AltLayer = 0; AltLayer < layerCount; AltLayer++)
            {
                WeatherCell wCell = PD.BufferMap[AltLayer][cell];
                wCell.temperature = Q[AltLayer] / thermalCap[AltLayer];

                if (float.IsNaN(wCell.temperature))
                {
                    KWSerror = true;
                    Logger("Temp is NaN" + " @ cell: " + cell.Index);
                }
                if (wCell.temperature < 2.725)
                {
                    KWSerror = true;
                    Logger("It's mighty chilling out here Frank" + " @ cell: " + cell.Index);
                }

                PD.BufferMap[AltLayer][cell] = wCell;
                /*
                if (cell.Index == 6664 && AltLayer == 0)
                {
                    Logger("KSC newTemp: " + wCell.temperature);
                }
                */
            }

            #endregion


            //Logger("Pressure and wind done");
            //Now we move on to precipitation

            //-----------------PRECIPITATION--------------------\\

            //calc condensed water
            for (int layer = 0; layer < layerCount; layer++)
            {
                WeatherCell wCell = PD.BufferMap[layer][cell];
                WeatherCell wCellLive = PD.LiveMap[layer][cell];
                CloudData cloud = wCell.cloud;
                CloudData cloudLive = wCellLive.cloud;
                if ((cloudLive.getwaterContent() > 1E-4f) && (cloud.getwaterContent() < 1E-5f))//TODO: delete after fixing precipitations
                {
                    KWSerror = false;
                }

                double newDew = K_N_DROP * (N_cond[layer] + N_sscond[layer]) * WeatherFunctions.SphereSize2Volume(0.000001f);  // newDew = amount of Dew coalescing in new droplets
                if (newDew < N_cond[layer])   // drop the newDew used for coalescence of new droplets
                {
                    N_cond[layer] -= (float)newDew;
                }
                else
                {
                    N_sscond[layer] -= (float)newDew;
                }

                if (cloudLive.rainyDuration < 1) { cloud.rainyDecay = 0; }
                else if (cloudLive.getwaterContent() > 0)
                {  // rainyDecay is the average change of droplets volume, for droplets generated each cycle (droplets population samples)
                    float rainyDecay = Math.Abs(1 - (float)newDew * cloudLive.rainyDuration / cloudLive.getwaterContent())
                        + ((cloudLive.rainyDuration > 1) ? (cloudLive.rainyDecay/255f) / (cloudLive.rainyDuration - 1) : 0) / cloudLive.rainyDuration;
                    if (float.IsNaN(rainyDecay))
                    {
                        KWSerror = true;
                        Logger("rainyDecay is NaN" + " @ cell: " + cell.Index);
                    }
                    cloud.rainyDecay = (byte)(rainyDecay*255);
                }
                RainyDuration[layer] = cloudLive.rainyDuration;
                
                if (newDew > FLT_Epsilon) { RainyDuration[layer]++; } 
                depositedDew[layer] = (float)(Math.Abs(cloudLive.dDew) + newDew * (wCell.temperature < PD.dewData.T_m ? 1 : 0));
                condensedDew[layer] = (float)(cloudLive.cDew + newDew * (wCell.temperature >= PD.dewData.T_m ? 1 : 0));

                // change of state (liquid/solid) of dew condensed/deposited
                if (wCell.temperature - PD.dewData.T_m > FLT_Epsilon)
                {
                    // TimeMelt = time to freeze/melt = Q_melt/Power
                    // float Power = (float)((wCell.temperature - PD.dewData.T_m) * atmo_ks * 4f * Math.PI * cloud.getDropletSize * cloud.getDropletSize);
                    // float Q_melt = (float)(PD.dewData.hm * PD.dewData.Ds * (4f / 3f * Math.PI * Math.Pow(cloud.getDropletSize, 3)));

                    // NOTE: tiny droplets freeze/melt very fast; raindrops, snowflakes take much longer; hail takes still longer. 
                    //This routine will also come useful to determine what's actually falling to the ground
                    float N_Melt = 1.0f;
                    if (Math.Abs(cloudLive.dropletSize) > 4E-7f)
                    {
                        float TimeMelt = (float)Math.Abs(PD.dewData.hm * PD.dewData.Ds / 3.0f / (wCell.temperature - PD.dewData.T_m)
                            / PD.atmoData.ks * wCell.getDropletSize());
                        // amount frozen/melted in cycle
                        if (float.IsNaN(TimeMelt))
                        {
                            KWSerror = true;
                            Logger("TimeMelt is NaN" + " @ cell: " + cell.Index);
                        }
                        N_Melt = (Mathf.Clamp((float)(DeltaTime / TimeMelt), 0, 1.0f));
                    }
                    if (wCell.temperature > PD.dewData.T_m)
                    {
                        N_Melt *= depositedDew[layer];
                        condensedDew[layer] += N_Melt;
                        depositedDew[layer] -= N_Melt;
                    }
                    else if (wCell.temperature < PD.dewData.T_m)
                    {
                        // Bergeron Process: condensedDew is turned into depositedDew increasingly fast the lower the temperature
                        N_Melt = (float)Math.Min(1.0f, N_Melt * Math.Pow((PD.dewData.T_m / wCell.temperature), 2.5f));
                        N_Melt *= condensedDew[layer];
                        depositedDew[layer] += N_Melt;
                        condensedDew[layer] -= N_Melt;
                    }
                }
                
                if ((condensedDew[layer] <0f) || (depositedDew[layer]<0f))
                {
                    KWSerror = true;
                    Logger("oh, unwanted evaporation/sublimation ?" + " @ cell: " + cell.Index);
                }
                if (float.IsNaN(depositedDew[layer]) || float.IsNaN(condensedDew[layer])) 
                {
                    KWSerror = true;
                    Logger("condensedDew/depositedDew is NaN" + " @ cell: " + cell.Index);
                }
                if (RainyDuration[layer] > 1023)
                {
                    RainyDuration[layer] = 1023;
                    depositedDew[layer] = -Math.Abs(depositedDew[layer]);  // dDew sign is used to mean duration in excess of 1023 cycles
                }
                if (RainyDuration[layer] < 1023)
                {
                    depositedDew[layer] = Math.Abs(depositedDew[layer]);  // dDew sign reset when duration is lower than 1023 cycles
                }
                cloud.dDew = depositedDew[layer];
                cloud.cDew = condensedDew[layer];
                cloud.dropletSize = cloudLive.dropletSize;
                cloud.rainyDuration = RainyDuration[layer];
                cloud.thickness = (ushort)(cloud.getwaterContent()*DeltaAltitude / D_wet[layer]* K_THICK);  //TODO: K_THICK to be balanced
                if ((cloud.getwaterContent() > 0) & (cloud.dropletSize ==0))
                {
                    cloud.dropletSize = 1E-6f;  // initial nucleation of droplets, can't be 0 if condensed water exists 
                }
                wCell.cloud = cloud; //reassign because struct
                PD.BufferMap[layer][cell] = wCell;
            }
            //Logger("Cloud water done");
            #region Droplet calcs, precipitations
            //calc droplet terminal speed from previous droplet size, as well as volume and new droplet size
            for (int layer = 0; layer < layerCount; layer++)
            {
                WeatherCell wCell = PD.BufferMap[layer][cell];

                //calc the terminal speed
                DI_S[layer] = (float)Math.Sqrt(8.0 / 3.0 * (wCell.getDropletSize()) * PD.dewData.Dl * G(PD.index, layer * (float)DeltaAltitude) / D_wet[layer]
                    / (wCell.temperature > PD.dewData.T_m ? 0.6f : 170f));

                //calc the droplet volume
                DI_V[layer] = WeatherFunctions.SphereSize2Volume(wCell.getDropletSize());
                DI_V[layer] += 4.0 * Math.PI * wCell.cloud.dropletSize * wCell.cloud.dropletSize * (N_cond[layer] + N_sscond[layer]) * DeltaTime  // growth by condensation
                    / Math.Min(K_DROP, (float)DeltaTime * 7.2f); // K_DROP has to allow for the growth of droplets, a short DeltaTime makes the increase rounded off when converting to dropletSize
                DI_V[layer] += Math.PI * wCell.cloud.dropletSize * wCell.cloud.dropletSize * DI_S[layer] * DeltaTime * (condensedDew[layer]+depositedDew[layer])  // growth by coalescence/collision
                    / Math.Min(K_DROP2, (float)DeltaTime * 4); 

                float windspeedUp = layer == 0 ? PD.BufferMap[layer][cell].windVector.y : PD.BufferMap[layer - 1][cell].windVector.y;

                if (wCell.cloud.getwaterContent() == 0)
                { N_Prec_p[layer] = 0; }
                else
                {
                    double avgDropSize = (WeatherFunctions.AverageDropletSize(wCell.cloud.dropletSize, wCell.cloud.rainyDecay, wCell.cloud.rainyDuration));
                    N_Prec_p[layer] = ((DI_S[layer] - windspeedUp > 0) || (DI_V[layer] > 5.236E-7)) ? // 5.236E-7 is volume for the max possible dropletSize = 0.005: if droplets are beyond the max Size, they'll drop whatever the upwind (reason: they move fast, will cause coalescence so high to grow larger than full sized raindrops in less than a single cycle)
                        Math.Max(0, Math.Min(1f, (float)(DI_V[layer] * K_N_DROP * avgDropSize / wCell.cloud.getwaterContent() 
                        * (DI_S[layer] - wCell.windVector.y) * DeltaTime / DeltaAltitude))) : 0; // Prec% is now given by the volume of the largest droplets against the total volume of droplets, and the vertical distance moved
                }

                N_Prec[layer] = N_Prec_p[layer] * (Math.Abs(wCell.cloud.cDew) + Math.Abs(wCell.cloud.dDew));

                if (float.IsNaN(N_Prec[layer]))
                {
                    KWSerror = true;
                    Logger("N_Prec is NaN" + " @ cell: " + cell.Index);
                }

                #region CCN update

                WeatherCell wCellLive = PD.LiveMap[layer][cell];
                if (layer != 0)
                {
                    double a = (1 - Math.Exp(-(getWsH(PD.LiveMap[layer - 1][cell])) * (DeltaTime) * K_CCN));
                    double c = Math.Max(0, PD.LiveMap[layer - 1][cell].windVector.y * DeltaTime / DeltaAltitude);
                    double b = Math.Exp(-(DeltaTime) * N_Prec[layer] * K_Prec); 

                    CCN[layer] = (float)((PD.LiveMap[layer - 1][cell].CCN - wCellLive.CCN) * Math.Min(a + c, 1f) + wCellLive.CCN * b);

                    if (float.IsInfinity(CCN[layer]))
                    {
                        KWSerror = true;
                        Logger("CCN is Infinity" + " @ cell: " + cell.Index);
                    }
                    if (float.IsNaN(CCN[layer]))
                    {
                        KWSerror = true;
                        Logger("CCN is NaN" + " @ cell: " + cell.Index);
                    }
                    if (CCN[layer] < 0)
                    {
                        KWSerror = true;
                        Logger("CCN is < 0" + " @ cell: " + cell.Index);
                    }
                    if (CCN[layer] > 1)
                    {
                        KWSerror = true;
                        Logger("CCN is > 1" + " @ cell: " + cell.Index);
                    }
                }
                else
                {
                    CCN[layer] = 1;
                }
                wCell.CCN = CCN[layer];
                #endregion
                //Logger("CCN done");


                CloudData cloud = wCell.cloud;
                float PrecAmount = (cloud.dDew < 0 ? 0 : N_Prec_p[layer]);  //if too long duration, total volume of droplets isn't decreasing because of N_Prec_p
                cloud.dDew *= 1 - PrecAmount;
                cloud.cDew *= 1 - PrecAmount;
                if ((N_Prec_p[layer] > FLT_Epsilon) && (cloud.rainyDuration > 0))
                {
                    cloud.rainyDuration--; // subtract from Rainy duration when it's raining
                }

                //calc the new droplet size
                cloud.dropletSize = WeatherFunctions.SphereVolume2Size((float)DI_V[layer]);  
                if (cloud.dropletSize > 5E-3f) // WARNING: dropletSize may grow > 5E-3, in particular below T_m. In reality such size will make for very fast coalescence.
                { cloud.dropletSize = 5E-3f; }  
                cloud.dropletSize = depositedDew[layer] > condensedDew[layer] ? -Math.Abs(cloud.dropletSize) : Math.Abs(cloud.dropletSize);
                
                wCell.cloud = cloud; //reassign cloud with updated cloud struct
                PD.BufferMap[layer][cell] = wCell; //reassign weathercell
            }
            
            // effects of precipitations
            
            float Pouring = 0f;
            float Q_prec = 0f;
            Q_Prec[layerCount - 1] = 0f;
            float T_prec_old, T_prec = 0;
            for (int layer = layerCount - 1; layer >= 0; layer--)
            {
                WeatherCell wCell = PD.BufferMap[layer][cell];
                Q_prec += N_Prec[layer] * wCell.temperature * PD.dewData.cl;
                T_prec_old = wCell.temperature * N_Prec[layer] + T_prec * Pouring;  // getting an average of temperature from all precipitation
                Pouring += N_Prec[layer];
                if (Pouring > FLT_Epsilon)
                {
                    T_prec_old /= Pouring;

                    // thermal effects (Q_prec)
                    if (layer > 0)
                    {
                        WeatherCell wCellLow = PD.BufferMap[layer - 1][cell];
                        T_prec = T_prec_old + (wCellLow.temperature - T_prec_old) * (1 - (float)Math.Exp(-3 * DeltaAltitude  // new temperature of dew from heat conduction with air at lower layer
                            * PD.atmoData.ks / (PD.dewData.Dl * PD.dewData.cl * Math.Abs(wCell.cloud.dropletSize) * (DI_S[layer] - wCell.windVector.y))));
                        Q_Prec[layer - 1] = (T_prec - T_prec_old) * Pouring * PD.dewData.cl;  // heat exchanged by dew with air while falling
                        if ((T_prec > PD.dewData.T_m) && (T_prec_old <= PD.dewData.T_m)) { Q_Prec[layer - 1] += Pouring * PD.dewData.hm; }  // heat exchanged in case of snow/hail melting
                        if ((T_prec > WeatherFunctions.getTempEq(PD.index, WeatherFunctions.getEwEq(PD.index, T_prec_old) * wCellLow.relativeHumidity))) // "if new temperature of dew is higher than equilibrium temperature"
                        {
                            float Pouring_P = Math.Max(1, (T_prec - T_prec_old) / (wCellLow.temperature - T_prec_old));  // percentage vaporizing
                            Q_Prec[layer - 1] += Pouring * Pouring_P * PD.dewData.he; // heat exchanged in case of rain vaporizing mid-air
                            wCellLow.N_Dew += Pouring * Pouring_P;
                            Pouring *= (1 - Pouring_P);
                            Q_prec *= (1 - Pouring_P);
                        }
                        if ((T_prec < PD.dewData.T_m) && (T_prec_old > PD.dewData.T_m)) { Q_Prec[layer - 1] -= Pouring * PD.dewData.hm; }  // heat exchanged in case of freezing rain
                    }
                    Q_prec += Q_Prec[layer];
                    wCell.temperature -= Q_Prec[layer] / thermalCap[layer];
                    if (float.IsNaN(wCell.temperature))
                    {
                        KWSerror = true;
                        Logger("Temp is NaN" + " @ cell: " + cell.Index);
                    }
                    if (wCell.temperature <= 2.725f)
                    {
                        KWSerror = true;
                        Logger("Temperature gone mad" + " @ cell: " + cell.Index);
                    }
                    // humidity effects
                    wCell.relativeHumidity = wCell.N_Dew * wCell.temperature * UGC / PD.dewData.M / ew_eq[layer];
                    PD.BufferMap[layer][cell] = wCell;  // and store updated values
                }
            }
            // soil effects
            {//soil temp
                SoilCell wCell = PD.BufferSoilMap[cell];
                QSoil = wCell.temperature * thermalCapSoil - Q_prec;
                wCell.temperature = Mathf.Max(2.725f, QSoil / thermalCapSoil);
                if (float.IsNaN(wCell.temperature))
                {
                    KWSerror = true;
                    Logger("Soil Temp is NaN" + " @ cell: " + cell.Index);
                }
                if (wCell.temperature <= 2.725f)
                {
                    KWSerror = true;
                    Logger("Soil temperature gone mad" + " @ cell: " + cell.Index);
                }
                PD.BufferSoilMap[cell] = wCell;
                if (Pouring > FLT_Epsilon)  // what is falling?
                {
                    if (T_prec > PD.dewData.T_m + 0.5)
                    {
                        return; // this is rain
                    }
                    else if ((T_prec >= PD.dewData.T_m) && (T_prec <= PD.dewData.T_m + 0.5) && (PD.BufferMap[0][cell].temperature < PD.dewData.T_m))
                    {
                        return; // this is freezing rain
                    }
                    else if (T_prec < PD.dewData.T_m)
                    {
                        for (int layer = layerCount - 1; layer > 0; layer--)
                        {
                            if ((PD.BufferMap[layer][cell].temperature < PD.dewData.T_m) && (PD.BufferMap[layer][cell].relativeHumidity > 1.0)
                                && (N_Prec[layer - 1] > 0) && (PD.BufferMap[layer - 1][cell].temperature > PD.dewData.T_m)
                                && (PD.BufferMap[layer - 1][cell].windVector.y > 0))
                            {
                                return; // this includes hail
                            }
                        }
                    }
                    else if ((T_prec < PD.dewData.T_m) && (wCell.temperature <= PD.dewData.T_m)) {
                        return; // this is snow
                    }
                    else { return;  // this is melting snow
                    }
                }
            }
            // TODO: soil biome should turn a bit more humid due to rain (calc biome humidity for next cycle based on past rain: requires each cell to have variable biome FLC so unfeasible for now)
            
            #endregion
            //Logger("Droplet calcs done");
            //Logger("Cell update done");
            
            #region debugLog

            bool takethisCell = false;
            if (WeatherSettings.SD.debugLog)
            {
                foreach (Cell neighbor in cell.GetNeighbors(PD.gridLevel))
                {
                    if (neighbor.Index == WeatherSettings.SD.debugCell)
                    {
                        takethisCell = WeatherSettings.SD.debugNeighbors;
                    }
                }
            }

            if (WeatherSettings.SD.debugLog && (cycle >= WeatherSettings.SD.LogStartCycle) && (cell.Index == WeatherSettings.SD.debugCell  || takethisCell ))
            {
                //write to debug file
                Logger("Writing debug file");
                int num = Directory.GetFiles(KSPUtil.ApplicationRootPath + "/GameData/KerbalWeatherSystems/Debug/").Length;
                using (System.IO.StreamWriter file =
                new System.IO.StreamWriter(@KSPUtil.ApplicationRootPath + "/GameData/KerbalWeatherSystems/Debug/debug" + num + "wc" + cell.Index + ".txt"))
                {
                    int layer = 0;
                    String STS = "  |  ";
                    WeatherCell wCell = PD.BufferMap[layer][cell];

                    file.WriteLine("Body: " + PD.body.bodyName + STS + "Update Cycle: " + cycle + STS + "DeltaTime: " + DeltaTime + STS + "AvgProcessTime (μs): " + WeatherSimulator.AvgCycleTime);
                    file.WriteLine("CellIndex: " + cell.Index + STS + "Latitude: " + latitude + STS + "Longitude: " + WeatherFunctions.GetCellLongitude(cell));
                    file.WriteLine("CellPosition: x: " + cell.Position.x + STS + "y: " + cell.Position.y + STS + "z: " + cell.Position.z);  // NOTE: this serves to debug Longitude and Latitude
                    file.WriteLine("CellCentroid: x: " + cell.Centroid(PD.gridLevel).x + STS + "y: " + cell.Centroid(PD.gridLevel).y + STS + "z: " + cell.Centroid(PD.gridLevel).z);  // NOTE: this serves to debug the gridcell geometry
                    file.WriteLine("Biome: " + WeatherFunctions.GetBiome(PD.index, cell) + STS + "FLC: " + PD.biomeDatas[WeatherFunctions.GetBiome(PD.index, cell)].FLC);
                    file.WriteLine("LayerCount: " + layerCount + STS + "DeltaLayerAlt: " + DeltaAltitude +
                    STS + "TropoHeight: " + DeltaAltitude * layerCount);

                    file.WriteLine();
                    file.WriteLine("SunAngle: " + WeatherFunctions.GetSunlightAngle(PD.index, cell) + STS + "SWT: " + SWT);
                    file.WriteLine();
                    file.WriteLine("Layer " + "   Altitude " + "  Pressure" + " Temperature" + "  ScaleHeight" + "   RH_i_%  " + "    LF_%   ");
                    file.WriteLine("Strat  " + "{0,10:N3} {1,10:N2} {2,10:N4} {3,12:N3} {4,10:N6} {5,10:N6}", DeltaAltitude * layerCount, 
                        PD.LiveStratoMap[0][cell].pressure, PD.LiveStratoMap[0][cell].temperature, WeatherFunctions.SH(PD.index, layerCount * (float)DeltaAltitude, PD.LiveStratoMap[0][cell].temperature),
                        " ---------", LFStrato[0]*100);
                    for (int i = layerCount - 1; i >= 0; i--)
                    {
                        file.WriteLine("{0,-6} {1,10:N3} {2,10:N2} {3,10:N4} {4,12:N3} {5,10:N6} {6,10:N6}", i, WeatherFunctions.GetCellAltitude(PD.index, i, cell), 
                            PD.LiveMap[i][cell].pressure, PD.LiveMap[i][cell].temperature, WeatherFunctions.SH(PD.index, i * (float)DeltaAltitude, PD.LiveMap[i][cell].temperature),
                            (PD.LiveMap[i][cell].relativeHumidity * 100), (LF[i] * 100));
                    }
                    file.WriteLine();
                    file.WriteLine("Layer " + "  waterContent " + " dropletSize " + "thickness " + "Ice_? " + "  Cl_SWR  " + "  Cl_SWA  " + "  Cl_IRR  " + "  Cl_IRA " + "  ReflFunc ");
                    for (int i = layerCount - 1; i >= 0; i--)
                    {
                        file.WriteLine("{0,-6} {1,12:E} {2,12:E} {3,7} {4,6} {5,9:N6} {6,9:N6} {7,9:N6} {8,9:N6} {9,9:N6}", i, PD.LiveMap[i][cell].cloud.getwaterContent(),
                            PD.LiveMap[i][cell].getDropletSize(), PD.LiveMap[i][cell].cloud.thickness, PD.LiveMap[i][cell].getIsIce(), Cl_SWR[i], Cl_SWA[i], Cl_IRR[i], Cl_IRA[i], ReflFunc[i]);
                    }
                    file.WriteLine();
                    file.WriteLine("Layer " + "   D_dry  " + "   D_dew  " + "    ew_eq  " + "     ew    " + "   AHDP   " + "    AH_i  " + "   D_wet " + "    Wet% " + "      N_dew ");
                    for (int i = layerCount - 1; i >= 0; i--)
                    {
                        file.WriteLine("{0,-6} {1,9:N6} {2,9:N6} {3,10:N4} {4,10:N4} {5,9:N6} {6,9:N6} {7,9:N6} {8,6:N2} {9,9:E}", i, D_dry[i], D_dew[i], ew_eq[i], ew[i], AHDP[i], AH[i], D_wet[i], WetPC[i], N_dew[i]);
                    }
                    file.WriteLine();
                    file.WriteLine("SPHeq: " + SPHeq + STS + "SPH: " + SPH + STS + "K_Evap: " + K_evap + STS + "Evap: " + Evap + STS + "Evap_Corr: " + evap_corr);
                    file.WriteLine("Q_Evap: " + Q_evap + STS + "Z_dew-dT: " + Z_dew_dT +  STS + "SoilRefl: " + SoilReflection);
                    file.WriteLine();
                    file.WriteLine("Layer " + " thermalCap  " + "    SWR   " + "    SWA   " + "    SWX   " + "    IRG   " + "    IRAU  " + "    IRR  " + "    IRAD  ");
                    file.Write("Strat  ");
                    file.WriteLine("{0,10:N0} {1,9:N4} {2,9:N4} {3,10:N4} {4,9:N4} {5,9:N4} {6,9:N4} {7,9:N4}", thermalCapStrato[0], SWRStrato[0], SWAStrato[0],SWXStrato[0], IRGStrato[0],IRAUStrato[0], IRRStrato[0],IRADStrato[0]);
                    for (int i = layerCount - 1; i >= 0; i--)
                    {
                        file.WriteLine("{0,-6} {1,10:N0} {2,9:N4} {3,9:N4} {4,10:N4} {5,9:N4} {6,9:N4} {7,9:N4} {8,9:N4}", i, thermalCap[i], SWR[i], SWA[i], SWX[i], IRG[i], IRAU[i], IRR[i], IRAD[i]);
                    }
                    file.Write("Soil   ");
                    file.WriteLine("{0,10:N0} {1,9:N4} {2,9:N4} {3,10:N4} {4,9:N4} {5,9:N4} {6,9:N4} {7,9:N4}", thermalCapSoil, SWRSoil, SWASoil, "----------", IRGSoil, IRAUSoil, IRRSoil, IRADSoil);
                    file.WriteLine();

                    file.WriteLine("Layer " + "    CCN  " + "      N_cond  " + "     N_sscond   " + "   Q_cond  " + "   N_Prec% " + "  N_prec  " + "      Q_prec ");
                    file.WriteLine("Strat ");
                    for (int i = layerCount - 1; i >= 0; i--)
                    {
                        file.WriteLine("{0,-6} {1,9:N7} {2,5:E} {3,5:E} {4,5:E} {5,6:N2} {6,5:E} {7,6:E}", i, CCN[i], N_cond[i], N_sscond[i], Q_cond[i], N_Prec_p[i], N_Prec[i], Q_Prec[i]);
                    }
                    file.WriteLine("Soil  ");
                    file.WriteLine();

                    file.WriteLine("Layer " + "   ∇P (all neighbors)" + "  ⇩ neighbor index ⇩  " + "                           tensStr.(x,y,z)     " + "             rotrStr.(x,y,z)     ");
                    file.Write("         ");
                    foreach (Cell neighbor in cell.GetNeighbors(PD.gridLevel))
                    {
                        file.Write(String.Format("{0:00000}", neighbor.Index) + "     ");
                    }
                    file.WriteLine();
                    file.WriteLine("Strat ");
                    for (int i = layerCount - 1; i >= 0; i--)
                    {
                        file.Write("{0,-6}", i);
                        for (int n = 0; n < DP[i].Length; n++)
                        {
                            file.Write(String.Format("{0:+0.000000;-0.000000}", DP[i][n]) + " ");
                        }
                        file.Write("  (");
                        file.Write(String.Format("{0:+0.00000;-0.00000}", tensStr[i].x) + ", ");
                        file.Write(String.Format("{0:+0.00000;-0.00000}", tensStr[i].y) + ", ");
                        file.Write(String.Format("{0:+0.00000;-0.00000}", tensStr[i].z) + ") ");
                        file.Write("  (");
                        file.Write(String.Format("{0:+0.00000;-0.00000}", rotrStr[i].x) + ", ");
                        file.Write(String.Format("{0:+0.00000;-0.00000}", rotrStr[i].y) + ", ");
                        file.Write(String.Format("{0:+0.00000;-0.00000}", rotrStr[i].z) + ")");
                        file.WriteLine();
                    }
                    file.WriteLine();

                    file.WriteLine("Layer " + "   Divg " + "  WsDiv  " + "       Ws_N " + "      Ws_E " + "    Total_N " + "   Total_E " + "   buoyancy " + "    DP_V   " + "         WindVector.(x,y,z)     " + "  WindSpeed " + " V_disp%");
                    file.WriteLine("Strat ");
                    for (int i = layerCount - 1; i >= 0; i--)
                    {
                        Vector3 wind = PD.BufferMap[i][cell].windVector;
                        file.Write("{0,-6}", i + " ");
                        file.Write(String.Format("{0:+00.000000;-00.000000}", Divg[i]) + " ");
                        file.Write(String.Format("{0:+000.000;-000.000}", wsDiv[i]) + " ");
                        file.Write(String.Format("{0:+00.000000;-00.000000}", wsN[i]) + " ");
                        file.Write(String.Format("{0:+00.000000;-00.000000}", wsE[i]) + " ");
                        file.Write(String.Format("{0:+00.000000;-00.000000}", Total_N[i]) + " ");
                        file.Write(String.Format("{0:+00.000000;-00.000000}", Total_E[i]) + " ");
                        file.Write(String.Format("{0:+000.000000;-000.000000}", buoyancy[i]) + " ");
                        file.Write(String.Format("{0:+000.000000;-000.000000}", DP_V[i]) + " ");
                        file.Write("(");
                        file.Write(String.Format("{0:+000.000;-000.000}", wind.x) + ", ");
                        file.Write(String.Format("{0:+000.000;-000.000}", wind.y) + ", ");
                        file.Write(String.Format("{0:+000.000;-000.000}", wind.z)+ ") ");
                        file.WriteLine("{0,9:N5} {1,7:N2}", wind.magnitude, V_disp[i]*100);
                    }
                    
                    file.WriteLine("Soil  ");
                    file.WriteLine();

                    file.WriteLine("Layer " + "        Q    " + "     T_adv_S  " + "     T_disp  " + "   Temperature " + "  SH  " + " FlowPChange" + " DynPressure " + " Pressure " + "  N_dew  " + "    RH_f_%");
                    file.WriteLine("Strat  " + "{0,6:E} {1,6:E} {2,6:E} {3,9:N4} {4,9:N3} {5,10} {6,10} {7,10:N2}", 
                        QStrato[0], "-------------", "-------------",PD.BufferStratoMap[0][cell].temperature, "---------", "---------",
                        "---------", PD.BufferStratoMap[0][cell].pressure);
                    for (int i = layerCount - 1; i >= 0; i--)
                    {
                        file.WriteLine("{0,-6} {1,6:E} {2:+0.000000E+00;-0.000000E+00} {3:+0.000000E+00;-0.000000E+00} {4,9:N4} {5,9:N3} {6:+0000.0000;-0000.0000} {7:+0000.0000;-0000.0000} {8,10:N2} {9,9:N7} {10,10:N6}", i,
                            Q[i], T_adv_S[i], T_disp[i], PD.BufferMap[i][cell].temperature, SH[i], PD.BufferMap[i][cell].flowPChange, dynPressure[i], PD.BufferMap[i][cell].pressure, N_dew[i], PD.BufferMap[i][cell].relativeHumidity*100);
                    }
                    file.WriteLine("Soil   " + "{0,6:E} {1,6:E} {2,6:E} {3,9:N4}", QSoil, "-------------", "-------------", PD.BufferSoilMap[cell].temperature);
                    file.WriteLine();

                    file.WriteLine("Layer " + "      Dl_S    " + "     Dl_v     " + "  waterContent " + " dropletSize " + "thickness " + "Ice_? " + "  condensed   " + "  deposited   " + "  RainDur " + "  RainDcy " + " dropletsCCN");
                    for (int i = layerCount - 1; i >= 0; i--)
                    {
                        wCell = PD.BufferMap[i][cell];
                        file.WriteLine("{0,-6} {1,9:E} {2,9:E} {3,12:E} {4,12:E} {5,7} {6,6} {7,6:E} {8,6:E} {9,9} {10,9} {11,9}",
                            i, DI_S[i], DI_V[i], wCell.cloud.getwaterContent(), wCell.getDropletSize(), wCell.cloud.thickness, wCell.getIsIce(), condensedDew[i], depositedDew[i], wCell.cloud.rainyDuration, wCell.cloud.rainyDecay, dropletsAsCCN[i]);
                    }
                    file.WriteLine();

                    file.WriteLine("                cell vertices                                 distance from cell center");
                    foreach (Vector3d Vertices in cell.GetVertices(PD.gridLevel))
                    {
                        file.Write("(");
                        file.Write(String.Format("{0:+0.000,000,000,000,000;-0.000,000,000,000,000}", Vertices.x) + ", ");
                        file.Write(String.Format("{0:+0.000,000,000,000,000;-0.000,000,000,000,000}", Vertices.y) + ", ");
                        file.Write(String.Format("{0:+0.000,000,000,000,000;-0.000,000,000,000,000}", Vertices.z) + ")   ");
                        file.Write("  " + Math.Sqrt((Vertices.x - cell.Position.x)*(Vertices.x - cell.Position.x)+(Vertices.y-cell.Position.y)*(Vertices.y - cell.Position.y)+(Vertices.z - cell.Position.z)*(Vertices.z - cell.Position.z))*PD.body.Radius);
                        file.WriteLine();
                    }
                    file.WriteLine();

                }
            }
            #endregion
        }
        private static float calcSoilRefractiveIndex(int database, Cell cell, float n1, float n2)
        {
            //n1 = refractive index of atmosphere (e.g. air = 1.000293)
            //n2 = refractive index of dew (e.g. water = 1.333)
            double ReflectionFactor = WeatherFunctions.GetSunriseFactor(database, cell);  // let's not make this call each time
            double RefractionFactor = Math.Sqrt(1 - (n1 * n1 / n2 / n2) * (1f - ReflectionFactor * ReflectionFactor));
            double s_polarizedRefr = (n1 * ReflectionFactor - n2 * RefractionFactor) / (n1 * ReflectionFactor + n2 * RefractionFactor);
            s_polarizedRefr *= s_polarizedRefr;
            double p_polarizedRefr = (n1 * RefractionFactor - n2 * ReflectionFactor) / (n1 * RefractionFactor + n2 * ReflectionFactor);
            p_polarizedRefr *= p_polarizedRefr;
            return (float)(25 * (s_polarizedRefr + p_polarizedRefr) / 2.0
                        / (25 + getWsH(WeatherDatabase.PlanetaryData[database].LiveMap[0][cell])));  
            // includes correction for sea state caused by wind
        }
        internal static float calcAH(int database, float RH, float temperature)
        {
            PlanetData PD = WeatherDatabase.PlanetaryData[database];
            float ew_eq = WeatherFunctions.getEwEq(database, temperature);
            float ew = ew_eq * RH;
            return ew * PD.dewData.M / UGC / temperature;
        }
        internal static float getAH(float ew, float MolarMass, float temperature)
        {
            return (ew * MolarMass / UGC / temperature);
        }
        internal static float getWsH(WeatherCell wCell)
        {
            return Mathf.Sqrt(wCell.windVector.x * wCell.windVector.x + wCell.windVector.z * wCell.windVector.z);
        }
        internal static float ToTheFourth(float thing)
        {
            return thing * thing * thing * thing;
        }
        internal static double G (int database, float altitude)
        {
            PlanetData PD = WeatherDatabase.PlanetaryData[database];
            return PD.body.gravParameter / ((PD.body.Radius + altitude) * (PD.body.Radius + altitude));
        }
        /*
        internal static QuaternionD Quaternion_mult(QuaternionD s, QuaternionD r)  // Hamilton product (https://en.wikipedia.org/wiki/Quaternion#Hamilton_product)
        {
            QuaternionD m = new QuaternionD(
                r.w * s.w - r.x * s.x - r.y * s.y - r.z * s.z, 
                r.w * s.x + r.x * s.w - r.y * s.z + r.z * s.y,
                r.w * s.y + r.x * s.z + r.y * s.w - r.z * s.x, 
                r.w * s.z - r.x * s.y + r.y * s.x + r.z * s.w);
            return m;
        }
        internal static QuaternionD Quaternion_rot(Vector3d v, QuaternionD q)
        {
            QuaternionD t = new QuaternionD(0.0, v.x, v.y, v.z);
            QuaternionD p = new QuaternionD(q.w, -q.x, -q.y, -q.z);
            return Quaternion_mult(Quaternion_mult(q, t), p);
        }
        */
        internal static void Logger(string s)
        {
            WeatherLogger.Log("[CU]" + s);
            if (KWSerror)
            {
                KWSerror = false;  // have a breakpoint here, then manually step out (F10 or F11)
            }
        }

    }
}
