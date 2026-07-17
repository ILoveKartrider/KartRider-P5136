using System;
using KartRider;
using System.Collections.Generic;
using Profile;
using System.Text.RegularExpressions;

namespace ExcData
{
    public class SpeedType
    {
        public static Dictionary<string, Dictionary<string, byte>> speedNames = new Dictionary<string, Dictionary<string, byte>>
        {
            { "国服", new Dictionary<string, byte> { { "标准", 7 }, { "慢速", 3 }, { "普通", 0 }, { "快速", 1 }, { "高速", 2 } } },
            { "国服复古", new Dictionary<string, byte> { { "新手", 0 }, { "初级", 1 }, { "L3", 2 }, { "L2", 3 }, { "L1", 4 }, { "Pro", 5 } } },
            { "韩服复古", new Dictionary<string, byte> { { "新手", 0 }, { "初级", 1 }, { "L3", 2 }, { "L2", 3 }, { "L1", 4 }, { "Pro", 5 } } }
        };

        public float AddSpec_TransAccelFactor { get; set; } = 0f;
        public float AddSpec_SteerConstraint { get; set; } = 0f;
        public float AddSpec_DriftEscapeForce { get; set; } = 0f;

        public float Mass { get; set; } = 0f;
        public float AirFriction { get; set; } = 0f;
        public float DragFactor { get; set; } = 0f;
        public float ForwardAccelForce { get; set; } = 0f;
        public float BackwardAccelForce { get; set; } = 0f;
        public float GripBrakeForce { get; set; } = 0f;
        public float SteerLeanFactor { get; set; } = 0f;
        public float SlipBrakeForce { get; set; } = 0f;
        public float MaxSteerAngle { get; set; } = 0f;
        public float SteerConstraint { get; set; } = 0f;
        public float FrontGripFactor { get; set; } = 0f;
        public float RearGripFactor { get; set; } = 0f;
        public float DriftTriggerFactor { get; set; } = 0f;
        public float DriftTriggerTime { get; set; } = 0f;
        public float DriftSlipFactor { get; set; } = 0f;
        public float DriftEscapeForce { get; set; } = 0f;
        public float CornerDrawFactor { get; set; } = 0f;
        public float DriftMaxGauge { get; set; } = 0f;
        public float TransAccelFactor { get; set; } = 0f;
        public float BoostAccelFactor { get; set; } = 0f;
        public float NormalBoosterTime { get; set; } = 0f;
        public float TeamBoosterTime { get; set; } = 0f;

        public void SpeedTypeData(string version, byte SpeedType)
        {
            if (speedNames.ContainsKey(version))
            {
                if (version == "国服")
                {
                    if (SpeedType == 3)//S0 慢速
                    {
                        Console.WriteLine("SpeedType:S0");
                        AddSpec_SteerConstraint = -0.3f;
                        AddSpec_DriftEscapeForce = -350f;
                        AddSpec_TransAccelFactor = -0.015f;
                        Mass = 100f;
                        AirFriction = 3f;
                        DragFactor = 0.7f;
                        ForwardAccelForce = 1620.0f;
                        BackwardAccelForce = 1500.0f;
                        GripBrakeForce = 1500.0f;
                        SlipBrakeForce = 1200.0f;
                        MaxSteerAngle = 10.0f;
                        SteerConstraint = 20.0f;
                        FrontGripFactor = 5.0f;
                        RearGripFactor = 5.0f;
                        DriftTriggerFactor = 0.2f;
                        DriftTriggerTime = 0.2f;
                        DriftSlipFactor = 0.2f;
                        DriftEscapeForce = 1850.0f;
                        CornerDrawFactor = 0.13f;
                        DriftMaxGauge = 5050.0f;
                        TransAccelFactor = -0.22f;
                        BoostAccelFactor = 0f;
                        NormalBoosterTime = 0f;
                        TeamBoosterTime = 0f;
                    }
                    else if (SpeedType == 0)//S1 普通
                    {
                        Console.WriteLine("SpeedType:S1");
                        AddSpec_SteerConstraint = 1.7f;
                        AddSpec_DriftEscapeForce = 150f;
                        AddSpec_TransAccelFactor = 0.199f;
                        Mass = 100f;
                        AirFriction = 3f;
                        DragFactor = 0.735f;
                        ForwardAccelForce = 1950.0f;
                        BackwardAccelForce = 1500.0f;
                        GripBrakeForce = 1800.0f;
                        SlipBrakeForce = 1250.0f;
                        MaxSteerAngle = 10.0f;
                        SteerConstraint = 22.0f;
                        FrontGripFactor = 5.0f;
                        RearGripFactor = 5.0f;
                        DriftTriggerFactor = 0.2f;
                        DriftTriggerTime = 0.2f;
                        DriftSlipFactor = 0.2f;
                        DriftEscapeForce = 2350.0f;
                        CornerDrawFactor = 0.15f;
                        DriftMaxGauge = 3970.0f;
                        TransAccelFactor = -0.006f;
                        BoostAccelFactor = 0f;
                        NormalBoosterTime = 0f;
                        TeamBoosterTime = 0f;
                    }
                    else if (SpeedType == 1)//S2 快速
                    {
                        Console.WriteLine("SpeedType:S2");
                        AddSpec_SteerConstraint = 2.2f;
                        AddSpec_DriftEscapeForce = 1100f;
                        AddSpec_TransAccelFactor = 0.202f;
                        Mass = 100f;
                        AirFriction = 3f;
                        DragFactor = 0.7621f;
                        ForwardAccelForce = 2350.0f;
                        BackwardAccelForce = 1950.0f;
                        GripBrakeForce = 2340.0f;
                        SlipBrakeForce = 1580.0f;
                        MaxSteerAngle = 10.0f;
                        SteerConstraint = 22.5f;
                        FrontGripFactor = 5.0f;
                        RearGripFactor = 5.0f;
                        DriftTriggerFactor = 0.2f;
                        DriftTriggerTime = 0.2f;
                        DriftSlipFactor = 0.2f;
                        DriftEscapeForce = 3300.0f;
                        CornerDrawFactor = 0.18f;
                        DriftMaxGauge = 4880.0f;
                        TransAccelFactor = -0.003f;
                        BoostAccelFactor = 0f;
                        NormalBoosterTime = 0f;
                        TeamBoosterTime = 0f;
                    }
                    else if (SpeedType == 2)//S3 高速
                    {
                        Console.WriteLine("SpeedType:S3");
                        AddSpec_SteerConstraint = 2.7f;
                        AddSpec_DriftEscapeForce = 1500f;
                        AddSpec_TransAccelFactor = 0.2f;
                        Mass = 100f;
                        AirFriction = 3f;
                        DragFactor = 0.79f;
                        ForwardAccelForce = 2900.0f;
                        BackwardAccelForce = 2175.0f;
                        GripBrakeForce = 2610.0f;
                        SlipBrakeForce = 1740.0f;
                        MaxSteerAngle = 10.0f;
                        SteerConstraint = 23.0f;
                        FrontGripFactor = 5.0f;
                        RearGripFactor = 5.0f;
                        DriftTriggerFactor = 0.2f;
                        DriftTriggerTime = 0.2f;
                        DriftSlipFactor = 0.2f;
                        DriftEscapeForce = 3700.0f;
                        CornerDrawFactor = 0.16f;
                        DriftMaxGauge = 6000.0f;
                        TransAccelFactor = -0.005f;
                        BoostAccelFactor = 0f;
                        NormalBoosterTime = 0f;
                        TeamBoosterTime = 0f;
                    }
                    else if (SpeedType == 4)//S4 无限
                    {
                        Console.WriteLine("SpeedType:S4");
                        Default();
                        DriftMaxGauge = 1f;
                    }
                    else if (SpeedType == 5)//S5 CGS LTE
                    {
                        Console.WriteLine("SpeedType:S5");
                        AddSpec_SteerConstraint = 2.7f;
                        AddSpec_DriftEscapeForce = 1500f;
                        AddSpec_TransAccelFactor = 0.2f;
                        Mass = 100f;
                        AirFriction = 2.7f;
                        DragFactor = 0.15f;
                        ForwardAccelForce = 1700.0f;
                        BackwardAccelForce = 300.0f;
                        GripBrakeForce = 2000.0f;
                        SteerLeanFactor = 0.0015f;
                        SlipBrakeForce = 1300.0f;
                        MaxSteerAngle = 12.5f;
                        SteerConstraint = 25.5f;
                        FrontGripFactor = 10.0f;
                        RearGripFactor = 10.0f;
                        DriftTriggerFactor = 0.2f;
                        DriftTriggerTime = 0.2f;
                        DriftSlipFactor = 0.2f;
                        DriftEscapeForce = 2350.0f;
                        CornerDrawFactor = 0.1f;
                        DriftMaxGauge = 3970.0f;
                        TransAccelFactor = -0.5f;
                        BoostAccelFactor = 0f;
                        NormalBoosterTime = 0f;
                        TeamBoosterTime = 0f;
                    }
                    else if (SpeedType == 6)//S6 真·无限
                    {
                        Console.WriteLine("SpeedType:S6");
                        AddSpec_SteerConstraint = 1.7f;
                        AddSpec_DriftEscapeForce = 150f;
                        AddSpec_TransAccelFactor = 0.199f;
                        Mass = 100f;
                        AirFriction = 3f;
                        DragFactor = 0.735f;
                        ForwardAccelForce = 1950.0f;
                        BackwardAccelForce = 1500.0f;
                        GripBrakeForce = 1800.0f;
                        SlipBrakeForce = 1250.0f;
                        MaxSteerAngle = 10.0f;
                        SteerConstraint = 22.0f;
                        FrontGripFactor = 5.0f;
                        RearGripFactor = 5.0f;
                        DriftTriggerFactor = 0.2f;
                        DriftTriggerTime = 0.2f;
                        DriftSlipFactor = 0.2f;
                        DriftEscapeForce = 2300.0f;
                        CornerDrawFactor = 0.15f;
                        DriftMaxGauge = 1.0f;
                        TransAccelFactor = 0.4f;
                        BoostAccelFactor = 0f;
                        NormalBoosterTime = 2000000f;
                        TeamBoosterTime = 2000000f;
                    }
                    else if (SpeedType == 7)//S7 标准速度
                    {
                        Console.WriteLine("SpeedType:S7");
                        Default();
                    }
                    else if (SpeedType == 8)//S8 标准速度
                    {
                        Console.WriteLine("SpeedType:S8");
                        AddSpec_SteerConstraint = 1.95f;
                        AddSpec_DriftEscapeForce = 400f;
                        AddSpec_TransAccelFactor = 0.2005f;
                        Mass = 100f;
                        AirFriction = 3f;
                        DragFactor = 0.74f;
                        ForwardAccelForce = 2150.0f;
                        BackwardAccelForce = 1725f;
                        GripBrakeForce = 2070f;
                        SlipBrakeForce = 1415.0f;
                        MaxSteerAngle = 10.0f;
                        SteerConstraint = 22.25f;
                        FrontGripFactor = 5.0f;
                        RearGripFactor = 5.0f;
                        DriftTriggerFactor = 0.2f;
                        DriftTriggerTime = 0.2f;
                        DriftSlipFactor = 0.2f;
                        DriftEscapeForce = 2600.0f;
                        CornerDrawFactor = 0.18f;
                        DriftMaxGauge = 4300.0f;
                        TransAccelFactor = -0.0045f;
                        BoostAccelFactor = -0.006f;
                        NormalBoosterTime = 0f;
                        TeamBoosterTime = 0f;
                    }
                    else
                    {
                        Default();
                    }
                }
                else if (version == "国服复古")
                {
                    if (SpeedType == 0 || SpeedType == 1)//Rookie
                    {
                        Console.WriteLine("SpeedType:Rookie");
                        Rookie();
                    }
                    else if (SpeedType == 2)//L3, S2
                    {
                        Console.WriteLine("SpeedType:L3");
                        L3();
                    }
                    else if (SpeedType == 3)//L2
                    {
                        Console.WriteLine("SpeedType:L2");
                        AddSpec_SteerConstraint = 0f;
                        AddSpec_DriftEscapeForce = 0f;
                        AddSpec_TransAccelFactor = 0f;
                        Mass = 100f;
                        AirFriction = 3f;
                        DragFactor = 0.743f;
                        ForwardAccelForce = 2500.0f;
                        BackwardAccelForce = 2100.0f;
                        GripBrakeForce = 2400.0f;
                        SlipBrakeForce = 1610.0f;
                        MaxSteerAngle = 10.0f;
                        SteerConstraint = 22.82f;
                        FrontGripFactor = 5.0f;
                        RearGripFactor = 5.0f;
                        DriftTriggerFactor = 0.2f;
                        DriftTriggerTime = 0.2f;
                        DriftSlipFactor = 0.2f;
                        DriftEscapeForce = 3400.0f;
                        CornerDrawFactor = 0.2f;
                        DriftMaxGauge = 5100.0f;
                        TransAccelFactor = 0f;
                        BoostAccelFactor = 0f;
                        NormalBoosterTime = 0f;
                        TeamBoosterTime = 0f;
                    }
                    else if (SpeedType == 4)//L1, S3
                    {
                        Console.WriteLine("SpeedType:L1");
                        AddSpec_SteerConstraint = 0f;
                        AddSpec_DriftEscapeForce = 0f;
                        AddSpec_TransAccelFactor = 0f;
                        Mass = 100f;
                        AirFriction = 3f;
                        DragFactor = 0.772f;
                        ForwardAccelForce = 2700.0f;
                        BackwardAccelForce = 2137.0f;
                        GripBrakeForce = 2510.0f;
                        SlipBrakeForce = 1680.0f;
                        MaxSteerAngle = 10.0f;
                        SteerConstraint = 23.0f;
                        FrontGripFactor = 5.0f;
                        RearGripFactor = 5.0f;
                        DriftTriggerFactor = 0.2f;
                        DriftTriggerTime = 0.2f;
                        DriftSlipFactor = 0.2f;
                        DriftEscapeForce = 3550.0f;
                        CornerDrawFactor = 0.2f;
                        DriftMaxGauge = 5550.0f;
                        TransAccelFactor = 0f;
                        BoostAccelFactor = 0f;
                        NormalBoosterTime = 0f;
                        TeamBoosterTime = 0f;
                    }
                    else if (SpeedType == 5)//Pro
                    {
                        Console.WriteLine("SpeedType:Pro");
                        Pro();
                    }
                    else
                    {
                        Default();
                    }
                }
                else if (version == "韩服复古")
                {
                    if (SpeedType == 0 || SpeedType == 1)//Rookie
                    {
                        Console.WriteLine("SpeedType:Rookie");
                        Rookie();
                    }
                    else if (SpeedType == 2)//L3, S2
                    {
                        Console.WriteLine("SpeedType:L3");
                        L3();
                    }
                    else if (SpeedType == 3)//L2
                    {
                        Console.WriteLine("SpeedType:L2");
                        AddSpec_SteerConstraint = 0f;
                        AddSpec_DriftEscapeForce = 0f;
                        AddSpec_TransAccelFactor = 0f;
                        Mass = 100f;
                        AirFriction = 3f;
                        DragFactor = 0.801f;
                        ForwardAccelForce = 2900.0f;
                        BackwardAccelForce = 2175.0f;
                        GripBrakeForce = 2610.0f;
                        SlipBrakeForce = 1740.0f;
                        MaxSteerAngle = 10.0f;
                        SteerConstraint = 23.0f;
                        FrontGripFactor = 5.0f;
                        RearGripFactor = 5.0f;
                        DriftTriggerFactor = 0.2f;
                        DriftTriggerTime = 0.2f;
                        DriftSlipFactor = 0.2f;
                        DriftEscapeForce = 3700.0f;
                        CornerDrawFactor = 0.2f;
                        DriftMaxGauge = 6000.0f;
                        TransAccelFactor = 0f;
                        BoostAccelFactor = 0f;
                        NormalBoosterTime = 0f;
                        TeamBoosterTime = 0f;
                    }
                    else if (SpeedType == 4)//L1, S3
                    {
                        Console.WriteLine("SpeedType:L1");
                        AddSpec_SteerConstraint = 0f;
                        AddSpec_DriftEscapeForce = 0f;
                        AddSpec_TransAccelFactor = 0f;
                        Mass = 100f;
                        AirFriction = 3f;
                        DragFactor = 0.794f;
                        ForwardAccelForce = 2900.0f;
                        BackwardAccelForce = 2175.0f;
                        GripBrakeForce = 2610.0f;
                        SlipBrakeForce = 1740.0f;
                        MaxSteerAngle = 10.0f;
                        SteerConstraint = 23.0f;
                        FrontGripFactor = 5.0f;
                        RearGripFactor = 5.0f;
                        DriftTriggerFactor = 0.2f;
                        DriftTriggerTime = 0.2f;
                        DriftSlipFactor = 0.2f;
                        DriftEscapeForce = 3700.0f;
                        CornerDrawFactor = 0.2f;
                        DriftMaxGauge = 6000.0f;
                        TransAccelFactor = 0f;
                        BoostAccelFactor = 0f;
                        NormalBoosterTime = 0f;
                        TeamBoosterTime = 0f;
                    }
                    else if (SpeedType == 5)//Pro
                    {
                        Console.WriteLine("SpeedType:Pro");
                        Pro();
                    }
                    else
                    {
                        Default();
                    }
                    KR();
                }
            }
            else
            {
                Default();
            }
        }

        private void Default()
        {
            AddSpec_SteerConstraint = 1.95f;
            AddSpec_DriftEscapeForce = 400f;
            AddSpec_TransAccelFactor = 0.2005f;
            Mass = 100f;
            AirFriction = 3f;
            DragFactor = 0.75f;
            ForwardAccelForce = 2150.0f;
            BackwardAccelForce = 1725f;
            GripBrakeForce = 2070f;
            SlipBrakeForce = 1415.0f;
            MaxSteerAngle = 10.0f;
            SteerConstraint = 22.25f;
            FrontGripFactor = 5.0f;
            RearGripFactor = 5.0f;
            DriftTriggerFactor = 0.2f;
            DriftTriggerTime = 0.2f;
            DriftSlipFactor = 0.2f;
            DriftEscapeForce = 2600.0f;
            CornerDrawFactor = 0.18f;
            DriftMaxGauge = 4300f;
            TransAccelFactor = -0.0045f;
            BoostAccelFactor = -0.006f;
            NormalBoosterTime = 0f;
            TeamBoosterTime = 0f;
        }

        private void Rookie()
        {
            AddSpec_SteerConstraint = 0f;
            AddSpec_DriftEscapeForce = 0f;
            AddSpec_TransAccelFactor = 0f;
            Mass = 100f;
            AirFriction = 3f;
            DragFactor = 0.74f;
            ForwardAccelForce = 2000.0f;
            BackwardAccelForce = 1500.0f;
            GripBrakeForce = 1800.0f;
            SlipBrakeForce = 1200.0f;
            MaxSteerAngle = 10.0f;
            SteerConstraint = 22.0f;
            FrontGripFactor = 5.0f;
            RearGripFactor = 5.0f;
            DriftTriggerFactor = 0.2f;
            DriftTriggerTime = 0.2f;
            DriftSlipFactor = 0.2f;
            DriftEscapeForce = 2500.0f;
            CornerDrawFactor = 0.2f;
            DriftMaxGauge = 4000.0f;
            TransAccelFactor = 0f;
            BoostAccelFactor = 0f;
            NormalBoosterTime = 0f;
            TeamBoosterTime = 0f;
        }

        private void L3()
        {
            AddSpec_SteerConstraint = 0f;
            AddSpec_DriftEscapeForce = 0f;
            AddSpec_TransAccelFactor = 0f;
            Mass = 100f;
            AirFriction = 3f;
            DragFactor = 0.763f;
            ForwardAccelForce = 2400.0f;
            BackwardAccelForce = 1950.0f;
            GripBrakeForce = 2340.0f;
            SlipBrakeForce = 1560.0f;
            MaxSteerAngle = 10.0f;
            SteerConstraint = 22.8f;
            FrontGripFactor = 5.0f;
            RearGripFactor = 5.0f;
            DriftTriggerFactor = 0.2f;
            DriftTriggerTime = 0.2f;
            DriftSlipFactor = 0.2f;
            DriftEscapeForce = 3300.0f;
            CornerDrawFactor = 0.2f;
            DriftMaxGauge = 5000.0f;
            TransAccelFactor = 0f;
            BoostAccelFactor = 0f;
            NormalBoosterTime = 0f;
            TeamBoosterTime = 0f;
        }

        private void Pro()
        {
            AddSpec_SteerConstraint = 0f;
            AddSpec_DriftEscapeForce = 0f;
            AddSpec_TransAccelFactor = 0f;
            Mass = 100f;
            AirFriction = 3f;
            DragFactor = 0.810f;
            ForwardAccelForce = 3800.0f;
            BackwardAccelForce = 2850.0f;
            GripBrakeForce = 3420.0f;
            SlipBrakeForce = 2280.0f;
            MaxSteerAngle = 10.0f;
            SteerConstraint = 23.4f;
            FrontGripFactor = 5.0f;
            RearGripFactor = 5.0f;
            DriftTriggerFactor = 0.2f;
            DriftTriggerTime = 0.2f;
            DriftSlipFactor = 0.2f;
            DriftEscapeForce = 4700.0f;
            CornerDrawFactor = 0.2f;
            DriftMaxGauge = 8000f;
            TransAccelFactor = 0f;
            BoostAccelFactor = 0f;
            NormalBoosterTime = 0f;
            TeamBoosterTime = 0f;
        }

        private void KR()
        {
            DragFactor += -0.005f;
            ForwardAccelForce += -5f;
            SteerConstraint += 0.05f;
            DriftEscapeForce += 150f;
            CornerDrawFactor += 0.005f;
            TransAccelFactor += 0.18f;
        }

        private static readonly Dictionary<int, byte> RoomSpeedGrades = new Dictionary<int, byte>
        {
            { 0, 3 },
            { 1, 0 },
            { 2, 1 },
            { 3, 2 },
            { 4, 4 },
            { 5, 5 },
            { 6, 6 },
            { 7, 7 },
            { 8, 8 }
        };

        private static readonly Dictionary<string, byte> ClassicRoomSpeedGrades =
            new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase)
            {
                { "BEGINNER", 0 },
                { "ROOKIE", 1 },
                { "L3", 2 },
                { "L2", 3 },
                { "L1", 4 },
                { "PRO", 5 }
            };

        /// <summary>
        /// 방 이름의 ASCII 속도 키워드를 해석한다.
        /// 현행 물리는 S0~S8, 클래식 물리는 BEGINNER/ROOKIE/L3/L2/L1/PRO를 사용한다.
        /// KR 토큰이 함께 있으면 한국 클래식 물리를 선택한다.
        /// </summary>
        public static (string version, byte speed, byte infinite)? Parse(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return null;
            }

            Match speedMatch = Regex.Match(
                input,
                @"(?<![A-Za-z0-9])S(?<grade>[0-8])(?![A-Za-z0-9])",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (speedMatch.Success &&
                int.TryParse(speedMatch.Groups["grade"].Value, out int grade) &&
                RoomSpeedGrades.TryGetValue(grade, out byte speed))
            {
                byte infinite = grade == 4 || grade == 6 ? speed : byte.MaxValue;
                return ("国服", speed, infinite);
            }

            foreach (KeyValuePair<string, byte> pair in ClassicRoomSpeedGrades)
            {
                if (!ContainsAsciiKeyword(input, pair.Key))
                {
                    continue;
                }

                bool koreanClassic = ContainsAsciiKeyword(input, "KR") ||
                    ContainsAsciiKeyword(input, "KOREA");
                return (koreanClassic ? "韩服复古" : "国服复古", pair.Value, byte.MaxValue);
            }

            return null;
        }

        private static bool ContainsAsciiKeyword(string input, string keyword)
        {
            return Regex.IsMatch(
                input,
                $@"(?<![A-Za-z0-9]){Regex.Escape(keyword)}(?![A-Za-z0-9])",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
    }
}
