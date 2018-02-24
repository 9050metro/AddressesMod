﻿using ColossalFramework;
using ColossalFramework.Globalization;
using ColossalFramework.Math;
using ColossalFramework.UI;
using ICities;
using Klyte.Addresses.Utils;
using Klyte.Extensions;
using Klyte.Harmony;
using Klyte.TransportLinesManager.Extensors;
using Klyte.TransportLinesManager.Extensors.TransportTypeExt;
using Klyte.TransportLinesManager.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Klyte.Addresses.Overrides
{
    class NetManagerOverrides : Redirector<NetManagerOverrides>
    {
        private static MethodInfo roadBaseAiGenerateName = typeof(RoadBaseAI).GetMethod("GenerateStreetName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly | BindingFlags.GetField | BindingFlags.GetProperty);

        #region Mod

        private static bool GenerateSegmentName(ushort segmentID, ref string __result)
        {
            var segment = NetManager.instance.m_segments.m_buffer[segmentID];
            var info = segment.Info;
            PrefabAI ai = info.GetAI();
            string format = null;
            Randomizer randomizer = new Randomizer(segment.m_nameSeed);
            AdrConfigWarehouse.ConfigIndex district = (AdrConfigWarehouse.ConfigIndex)(AdrUtils.GetDistrict(segment.m_middlePosition) & 0xFF);

            if ((info.m_vehicleTypes & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None)
            {
                var filenamePrefix = AdrConfigWarehouse.getCurrentConfigString(AdrConfigWarehouse.ConfigIndex.PREFIX_FILENAME | district);
                if ((filenamePrefix == null || !AdrController.loadedLocalesRoadPrefix.ContainsKey(filenamePrefix)) && district > 0) filenamePrefix = AdrConfigWarehouse.getCurrentConfigString(AdrConfigWarehouse.ConfigIndex.PREFIX_FILENAME);
                if (filenamePrefix != null && AdrController.loadedLocalesRoadPrefix.ContainsKey(filenamePrefix))
                {
                    var currentPrefixFile = AdrController.loadedLocalesRoadPrefix[filenamePrefix];
                    format = currentPrefixFile.getPrefix(ai, info.m_forwardVehicleLaneCount == 0 || info.m_backwardVehicleLaneCount == 0, info.m_forwardVehicleLaneCount == info.m_backwardVehicleLaneCount, info.m_halfWidth * 2, (byte)(info.m_forwardVehicleLaneCount + info.m_backwardVehicleLaneCount), randomizer);
                }
                AdrUtils.doLog("selectedPrefix = {0}", format);
                if (format == null)
                {
                    string key = DefaultPrefix(info, ai);
                    uint rangeFormat = Locale.Count(key);
                    format = Locale.Get(key, randomizer.Int32(rangeFormat));
                }
            }

            if (format == null)
            {
                return true;
            }

            var filename = AdrConfigWarehouse.getCurrentConfigString(AdrConfigWarehouse.ConfigIndex.ROAD_NAME_FILENAME | district);
            if (string.IsNullOrEmpty(filename) || !AdrController.loadedLocalesRoadName.ContainsKey(filename))
            {
                filename = AdrConfigWarehouse.getCurrentConfigString(AdrConfigWarehouse.ConfigIndex.ROAD_NAME_FILENAME);
                if (string.IsNullOrEmpty(filename) || !AdrController.loadedLocalesRoadName.ContainsKey(filename))
                {
                    filename = null;
                }
            }

            randomizer = new Randomizer(segment.m_nameSeed);
            string arg;
            if (filename != null)
            {
                int range = AdrController.loadedLocalesRoadName[filename]?.Length ?? 0;
                if (range == 0)
                {
                    return true;
                }
                arg = AdrController.loadedLocalesRoadName[filename][randomizer.Int32((uint)range)];
            }
            else
            {
                arg = roadBaseAiGenerateName.Invoke(ai, new object[] { randomizer })?.ToString();
            }
            __result = StringUtils.SafeFormat(format, arg);
            return false;
        }

        private static string DefaultPrefix(NetInfo info, PrefabAI ai)
        {
            string text = null;
            if ((info.m_vehicleTypes & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None)
            {
                if (ai is RoadTunnelAI roadTunnel)
                {
                    if ((!roadTunnel.m_highwayRules || info.m_forwardVehicleLaneCount + info.m_backwardVehicleLaneCount >= 2))
                    {
                        text = "TUNNEL_NAME_PATTERN";
                    }
                }

                if (ai is RoadAI roadAi)
                {
                    if (roadAi.m_enableZoning)
                    {
                        if ((info.m_setVehicleFlags & Vehicle.Flags.OnGravel) != (Vehicle.Flags)0)
                        {
                            text = "ROAD_NAME_PATTERN";
                        }
                        else if (info.m_halfWidth >= 12f)
                        {
                            text = "AVENUE_NAME_PATTERN";
                        }
                        else
                        {
                            text = "STREET_NAME_PATTERN";
                        }
                    }
                    else if (roadAi.m_highwayRules)
                    {
                        if (info.m_hasForwardVehicleLanes && info.m_hasBackwardVehicleLanes)
                        {
                            text = "ROAD_NAME_PATTERN";
                        }
                        else if (info.m_forwardVehicleLaneCount >= 2 || info.m_backwardVehicleLaneCount >= 2)
                        {
                            text = "HIGHWAY_NAME_PATTERN";
                        }
                    }
                }

                if (ai is DamAI damAi)
                {
                    if ((info.m_vehicleTypes & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None)
                    {
                        text = "ROAD_NAME_PATTERN";
                    }
                }

                if (ai is RoadBridgeAI bridgeAi)
                {
                    if ((info.m_vehicleTypes & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None && (!bridgeAi.m_highwayRules || info.m_forwardVehicleLaneCount + info.m_backwardVehicleLaneCount >= 2))
                    {
                        text = "BRIDGE_NAME_PATTERN";
                    }
                }
            }
            return text;
        }
        #endregion

        #region Hooking

        public override void Awake()
        {
            AdrUtils.doLog("Loading NetManager Overrides");
            #region RoadBaseAI Hooks
            MethodInfo preRename = typeof(NetManagerOverrides).GetMethod("GenerateSegmentName", allFlags);

            AddRedirect(typeof(NetManager).GetMethod("GenerateSegmentName", allFlags), preRename);
            #endregion
        }
        #endregion

    }
}
