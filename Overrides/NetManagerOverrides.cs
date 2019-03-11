﻿using ColossalFramework;
using ColossalFramework.Globalization;
using ColossalFramework.Math;
using Klyte.Addresses.Utils;
using Klyte.Commons.Extensors;
using Klyte.Commons.Utils;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using static Klyte.Commons.Utils.KlyteUtils;

namespace Klyte.Addresses.Overrides
{
    class NetManagerOverrides : Redirector<NetManagerOverrides>
    {
        private static MethodInfo roadBaseAiGenerateName = typeof(RoadBaseAI).GetMethod("GenerateStreetName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly | BindingFlags.GetField | BindingFlags.GetProperty);

        #region Mod

        private static bool GenerateSegmentName(ushort segmentID, ref string __result)
        {
            var path = new List<ushort>();
            return GenerateSegmentNameInternal(segmentID, ref __result, ref path, false);
        }

        public static bool GetStreetNameForStation(ushort segmentID, ref string __result)
        {
            var path = new List<ushort>();
            return GenerateSegmentNameInternal(segmentID, ref __result, ref path, true);
        }

        private static bool GenerateSegmentNameInternal(ushort segmentID, ref string __result, ref List<ushort> usedQueue, bool removePrefix)
        {
            AdrUtils.doLog($"[START {segmentID}]" + __result);
            if ((NetManager.instance.m_segments.m_buffer[segmentID].m_flags & NetSegment.Flags.CustomName) != 0)
            {
                if (usedQueue.Count == 0)
                {
                    return true;
                }
                else
                {
                    InstanceID id = default(InstanceID);
                    id.NetSegment = segmentID;
                    __result = Singleton<InstanceManager>.instance.GetName(id);
                    return false;
                }
            }
            var segment = NetManager.instance.m_segments.m_buffer[segmentID];
            var info = segment.Info;
            PrefabAI ai = info.GetAI();
            string format = null;
            Randomizer randomizer = new Randomizer(segment.m_nameSeed);
            AdrConfigWarehouse.ConfigIndex district = (AdrConfigWarehouse.ConfigIndex)(DistrictManager.instance.GetDistrict(segment.m_middlePosition) & 0xFF);

            if ((info.m_vehicleTypes & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None)
            {
                var filenamePrefix = AdrConfigWarehouse.getCurrentConfigString(AdrConfigWarehouse.ConfigIndex.PREFIX_FILENAME | district);
                if ((filenamePrefix == null || !AdrController.loadedLocalesRoadPrefix.ContainsKey(filenamePrefix)) && district > 0) filenamePrefix = AdrConfigWarehouse.getCurrentConfigString(AdrConfigWarehouse.ConfigIndex.PREFIX_FILENAME);
                if (filenamePrefix != null && AdrController.loadedLocalesRoadPrefix.ContainsKey(filenamePrefix))
                {
                    var currentPrefixFile = AdrController.loadedLocalesRoadPrefix[filenamePrefix];
                    format = currentPrefixFile.getPrefix(ai, info.m_forwardVehicleLaneCount == 0 || info.m_backwardVehicleLaneCount == 0, info.m_forwardVehicleLaneCount == info.m_backwardVehicleLaneCount, info.m_halfWidth * 2, (byte)(info.m_forwardVehicleLaneCount + info.m_backwardVehicleLaneCount), randomizer, segmentID);
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

            if (removePrefix)
            {
                format = Regex.Replace(format, "(?!\\{)(\\w+|\\.)(?!\\})", "");
                if (format.IsNullOrWhiteSpace()) return true;
            }

            string genName = "";
            string sourceRoad = "";
            string targetRoad = "";
            string sourceKm = "";
            string sourceKmWithDecimal = "";
            string sourceDistrict = "";
            string targetDistrict = "";
            string direction = "";
            if (format.Contains("{0}"))
            {
                GetGeneratedRoadName(segment, ai, district, out genName);
                if (genName == null) return true;
            }
            ushort sourceSeg = 0;
            ushort targetSeg = 0;
            if (format.Contains("{1}") || format.Contains("{2}") || format.Contains("{3}") || format.Contains("{4}") || format.Contains("{7}"))
            {
                GetSegmentRoadEdges(segmentID, true, true, true, out ComparableRoad startRef, out ComparableRoad endRef);

                sourceSeg = startRef.segmentReference;
                targetSeg = endRef.segmentReference;

                if (format.Contains("{1}"))
                {
                    if (!usedQueue.Contains(sourceSeg))
                    {
                        usedQueue.Add(sourceSeg);
                        GenerateSegmentNameInternal(sourceSeg, ref sourceRoad, ref usedQueue, false);
                    }
                }
                if (format.Contains("{2}"))
                {
                    if (!usedQueue.Contains(targetSeg))
                    {
                        usedQueue.Add(targetSeg);
                        GenerateSegmentNameInternal(targetSeg, ref targetRoad, ref usedQueue, false);
                    }
                }
                if (format.Contains("{3}") || format.Contains("{4}"))
                {
                    float km = GetNumberAt(startRef.segmentReference, NetManager.instance.m_segments.m_buffer[startRef.segmentReference].m_startNode == startRef.nodeReference) / 1000f;
                    sourceKm = km.ToString("0");
                    sourceKmWithDecimal = km.ToString("0.0");
                }
                if (format.Contains("{7}"))//direction
                {
                    int cardinalDirection = KlyteUtils.GetCardinalDirection(startRef, endRef);

                    direction = Locale.Get("KCM_CARDINAL_POINT_SHORT", cardinalDirection.ToString());
                }
            }
            if (format.Contains("{5}") || format.Contains("{6}"))
            {
                GetSegmentRoadEdges(segmentID, false, false, false, out ComparableRoad startRef, out ComparableRoad endRef);
                if (format.Contains("{5}"))//source district
                {
                    sourceDistrict = GetDistrictAt(startRef);
                }
                if (format.Contains("{6}"))//target district
                {
                    targetDistrict = GetDistrictAt(endRef);
                }
            }
            if (AddressesMod.debugMode)
            {
                __result = $"[{segmentID}] " + StringUtils.SafeFormat(format, genName, sourceSeg, targetSeg, sourceKm, sourceKmWithDecimal, sourceDistrict, targetDistrict, direction)?.Trim();
            }
            else
            {
                __result = StringUtils.SafeFormat(format, genName, sourceRoad, targetRoad, sourceKm, sourceKmWithDecimal, sourceDistrict, targetDistrict, direction)?.Trim();
            }
            AdrUtils.doLog($"[END {segmentID}]" + __result);
            return false;

        }
        private static string GetDistrictAt(ComparableRoad refer)
        {
            string sourceDistrict;
            var node = NetManager.instance.m_nodes.m_buffer[refer.nodeReference];
            AdrUtils.doLog($"[Node {refer.nodeReference}] Flags => " + NetManager.instance.m_nodes.m_buffer[refer.nodeReference].m_flags);
            if ((NetManager.instance.m_nodes.m_buffer[refer.nodeReference].m_flags & NetNode.Flags.Outside) != 0)
            {
                float angle = Vector2.zero.GetAngleToPoint(VectorUtils.XZ(node.m_position));
                AdrUtils.doLog($"[Node {refer.nodeReference}] angle => {angle}, pos => {node.m_position} ");
                sourceDistrict = OutsideConnectionAIOverrides.GetNameBasedInAngle(angle, out bool canTrust);
            }
            else
            {
                int districtId = Singleton<DistrictManager>.instance.GetDistrict(node.m_position);
                if (districtId == 0)
                {
                    sourceDistrict = SimulationManager.instance.m_metaData.m_CityName;
                }
                else
                {
                    sourceDistrict = DistrictManager.instance.GetDistrictName(districtId);
                }
            }

            return sourceDistrict;
        }

        private static void GetGeneratedRoadName(NetSegment segment, PrefabAI ai, AdrConfigWarehouse.ConfigIndex district, out string genName)
        {
            var filename = AdrConfigWarehouse.getCurrentConfigString(AdrConfigWarehouse.ConfigIndex.ROAD_NAME_FILENAME | district);
            if (string.IsNullOrEmpty(filename) || !AdrController.loadedLocalesRoadName.ContainsKey(filename))
            {
                filename = AdrConfigWarehouse.getCurrentConfigString(AdrConfigWarehouse.ConfigIndex.ROAD_NAME_FILENAME);
                if (string.IsNullOrEmpty(filename) || !AdrController.loadedLocalesRoadName.ContainsKey(filename))
                {
                    filename = null;
                }
            }

            Randomizer randomizer = new Randomizer(segment.m_nameSeed);
            if (filename != null)
            {
                int range = AdrController.loadedLocalesRoadName[filename]?.Length ?? 0;
                if (range == 0)
                {
                    genName = null;
                    return;
                }
                genName = AdrController.loadedLocalesRoadName[filename][randomizer.Int32((uint)range)];
            }
            else
            {
                genName = roadBaseAiGenerateName.Invoke(ai, new object[] { randomizer })?.ToString();
            }
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
        public static readonly MethodInfo GenerateSegmentNameMethod = typeof(NetManager).GetMethod("GenerateSegmentName", allFlags);
        public override void AwakeBody()
        {
            AdrUtils.doLog("Loading NetManager Overrides");
            #region RoadBaseAI Hooks
            MethodInfo preRename = typeof(NetManagerOverrides).GetMethod("GenerateSegmentName", allFlags);

            AddRedirect(GenerateSegmentNameMethod, preRename);
            #endregion
        }

        public override void doLog(string text, params object[] param)
        {
            AdrUtils.doLog(text, param);
        }
        #endregion

    }
}
