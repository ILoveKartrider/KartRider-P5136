using ExcData;
using KartLibrary.Consts;
using KartLibrary.Data;
using KartLibrary.File;
using KartLibrary.Xml;
using KartRider;
using KartRider.Common.Utilities;
using KartRider.IO.Packet;
using Profile;
using RiderData;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace KartRider
{
    public static class KartRhoFile
    {
        private const string KartCatalogFormatVersion = "3";
        private const string KartCatalogProtocolVersion = "5136";
        private const string KartCatalogRegion = "kr";
        private const int MinimumKartCatalogNames = 1400;
        private const int MinimumKartCatalogSpecs = 1300;
        private const int MinimumKartCatalogAbilities = 700;
        private const int MinimumResolvedKartCatalogAbilities = 250;
        private const int MinimumKartCatalogInventoryItems = 6800;
        private const int MinimumKartCatalogInventoryCategories = 60;
        private const int MinimumKartCatalogInventoryKarts = 1200;
        private const int MinimumKartCatalogGrantItems = 5250;
        private const int MinimumKartCatalogGrantCategories = 41;

        // Numeric ids recovered once from the Korean 5136 item initializers.
        // Catalog generation must not depend on distributing or retaining an
        // unpacked executable, so these verified mappings are part of the
        // protocol profile.  KartRiderU.exe, when present, is only used to
        // cross-check the table and detect a mismatched client build.
        private static readonly (string Name, short ItemId)[]
            Korean5136ExecutableItemSymbols =
        {
            ("animalBooster", 31),
            ("bigBanana", 85),
            ("blockRocket", 117),
            ("candyRocket", 102),
            ("cokeBomb", 20),
            ("cokeRocket", 30),
            ("cokeRocketWorldCup", 39),
            ("darkCloud", 1),
            ("darkCloud2", 115),
            ("dinoClawRocket", 108),
            ("dinoEggRocket", 107),
            ("drrMine", 23),
            ("duckMine", 45),
            ("eggMine", 82),
            ("foxTailRocket", 126),
            ("goldRocket", 32),
            ("goldShield", 36),
            ("infectedBomb", 27),
            ("infectedWaterFly", 119),
            ("lockdownRocket", 104),
            ("prisonBomb", 47),
            ("protectShield", 81),
            ("pumpkinBomb", 44),
            ("rainbowCloud", 43),
            ("rollingCokeBomb", 22),
            ("rollingInfectedBomb", 29),
            ("sirenShield", 106),
            ("snowBomb", 34),
            ("snowWaterFly", 118),
            ("snowman", 112),
            ("tigerGhost", 101),
            ("tigerRocket", 99),
            ("timeCokeBomb", 21),
            ("timeInfectedBomb", 28),
            ("timeSnowBomb", 35),
            ("waterMine", 37),
            ("waterbombFly", 120)
        };

        public static string regionCode = "cn";

        public static PackFolderManager Dump(string input)
        {
            try
            {
                regionCode = GetRegionCode(input);

                // Check for sound_bgm_korea.rho and sound_bgm_lotte.rho files
                string inputDir = Path.GetDirectoryName(input);
                string korea = Path.Combine(inputDir, "sound_bgm_korea.rho");
                bool koreaFileExists = File.Exists(korea);
                string lotte = Path.Combine(inputDir, "sound_bgm_lotte.rho");
                bool lotteFileExists = File.Exists(lotte);

                BinaryXmlTag rootTag = GetAAATag(input);

                // Find the sound/bgm folder path
                BinaryXmlTag soundFolder = null;
                BinaryXmlTag bgmFolder = null;

                foreach (BinaryXmlTag subtag in rootTag.Children)
                {
                    if (subtag.Name == "PackFolder" && subtag.GetAttribute("name") == "sound")
                    {
                        soundFolder = subtag;
                        foreach (BinaryXmlTag soundSubtag in soundFolder.Children)
                        {
                            if (soundSubtag.Name == "PackFolder" && soundSubtag.GetAttribute("name") == "bgm")
                            {
                                bgmFolder = soundSubtag;
                                break;
                            }
                        }
                        break;
                    }
                }

                if (bgmFolder != null)
                {
                    // Check existing RhoFolder entries
                    bool koreaEntryExists = false;
                    bool lotteEntryExists = false;
                    bool metadataModified = false;
                    List<BinaryXmlTag> tagsToRemove = new List<BinaryXmlTag>();

                    foreach (BinaryXmlTag tag in bgmFolder.Children)
                    {
                        if (tag.Name == "RhoFolder")
                        {
                            string fileName = tag.GetAttribute("fileName");
                            if (fileName == "sound_bgm_korea.rho")
                            {
                                koreaEntryExists = true;
                                if (!koreaFileExists)
                                {
                                    tagsToRemove.Add(tag);
                                    metadataModified = true;
                                }
                            }
                            else if (fileName == "sound_bgm_lotte.rho")
                            {
                                lotteEntryExists = true;
                                if (!lotteFileExists)
                                {
                                    tagsToRemove.Add(tag);
                                    metadataModified = true;
                                }
                            }
                        }
                    }

                    // Remove tags for files that don't exist
                    foreach (BinaryXmlTag tag in tagsToRemove)
                    {
                        bgmFolder.Children.Remove(tag);
                    }

                    // Add missing entries for files that exist
                    if (koreaFileExists && !koreaEntryExists)
                    {
                        metadataModified = true;
                        using (var Korea = new Rho(korea))
                        {
                            BinaryXmlTag koreaTag = new BinaryXmlTag("RhoFolder");
                            koreaTag.SetAttribute("name", "korea");
                            koreaTag.SetAttribute("fileName", "sound_bgm_korea.rho");
                            koreaTag.SetAttribute("key", Korea.GetFileKey().ToString());
                            koreaTag.SetAttribute("dataHash", Korea.GetDataHash().ToString());
                            koreaTag.SetAttribute("mediaSize", Korea.baseStream.Length.ToString());
                            bgmFolder.Children.Add(koreaTag);
                        }
                    }

                    if (lotteFileExists && !lotteEntryExists)
                    {
                        metadataModified = true;
                        using (var Lotte = new Rho(lotte))
                        {
                            BinaryXmlTag lotteTag = new BinaryXmlTag("RhoFolder");
                            lotteTag.SetAttribute("name", "lotte");
                            lotteTag.SetAttribute("fileName", "sound_bgm_lotte.rho");
                            lotteTag.SetAttribute("key", Lotte.GetFileKey().ToString());
                            lotteTag.SetAttribute("dataHash", Lotte.GetDataHash().ToString());
                            lotteTag.SetAttribute("mediaSize", Lotte.baseStream.Length.ToString());
                            bgmFolder.Children.Add(lotteTag);
                        }
                    }

                    if (metadataModified)
                    {
                        // Save the modified content back to aaa.pk
                        try
                        {
                            string xmlContent = rootTag.ToString();
                            string tempXmlPath = Path.Combine(Path.GetDirectoryName(input), "temp_aaa.xml");
                            File.WriteAllText(tempXmlPath, xmlContent, Encoding.GetEncoding("UTF-16"));

                            // Use the same approach as AAAD to write the aaa.pk file
                            var xdoc = XDocument.Load(tempXmlPath);
                            if (xdoc.Root == null)
                            {
                                Console.WriteLine("Error: Root element is null");
                                return null;
                            }
                            var childCounts = CountChildren(xdoc.Root, 0, new List<int>());
                            byte[] byteArray;
                            using (var reader = XmlReader.Create(tempXmlPath))
                            {
                                using (var outPacket = new OutPacket())
                                {
                                    var Count = 0;
                                    while (reader.Read())
                                        if (reader.NodeType == XmlNodeType.Element)
                                        {
                                            var elementName = reader.Name;
                                            var attCount = reader.AttributeCount;
                                            outPacket.WriteString(elementName);
                                            outPacket.WriteInt();
                                            outPacket.WriteInt(attCount);
                                            for (var i = 0; i < attCount; i++)
                                            {
                                                reader.MoveToAttribute(i);
                                                var attName = reader.Name;
                                                outPacket.WriteString(attName);
                                                var attValue = reader.Value;
                                                outPacket.WriteString(attValue);
                                            }

                                            outPacket.WriteInt(childCounts[Count]);
                                            Count++;
                                            reader.MoveToElement();
                                        }

                                    byteArray = outPacket.ToArray();
                                }
                            }

                            using (var fileStream = new FileStream(input, FileMode.Create))
                            {
                                var binaryWriter = new BinaryWriter(fileStream);
                                binaryWriter.Write(0);
                                var KRDataLength = binaryWriter.WriteKRData(byteArray, false, true);
                                binaryWriter.BaseStream.Seek(0, SeekOrigin.Begin);
                                binaryWriter.Write(KRDataLength);
                            }

                            if (File.Exists(tempXmlPath))
                            {
                                File.Delete(tempXmlPath);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error writing aaa.pk: {ex.Message}");
                            return null;
                        }
                    }
                }

                // Now open the modified aaa.pk with PackFolderManager
                PackFolderManager packFolderManager = new PackFolderManager();
                try
                {
                    Console.WriteLine("开始读取游戏Data内文件...");
                    Console.WriteLine("==============================");
                    packFolderManager.OpenDataFolder(input);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error opening modified aaa.pk: {ex.Message}");
                    return null;
                }

                Queue<PackFolderInfo> packFolderInfoQueue = new Queue<PackFolderInfo>();
                packFolderInfoQueue.Enqueue(packFolderManager.GetRootFolder());
                while (packFolderInfoQueue.Count > 0)
                {
                    PackFolderInfo packFolderInfo1 = packFolderInfoQueue.Dequeue();
                    foreach (PackFileInfo packFileInfo in packFolderInfo1.GetFilesInfo())
                    {
                        string fullName = ReplacePath(packFileInfo.FullName);
                        if (fullName.Contains("flyingPet") && fullName.Contains($"param@{regionCode}.bml"))
                        {
                            Console.WriteLine(fullName);
                            string name = fullName.Substring(10, fullName.Length - 23);
                            if (!(FlyingPet.flyingSpec.ContainsKey(name)))
                            {
                                byte[] data = packFileInfo.GetData();
                                using (MemoryStream stream = new MemoryStream(BmlToXml(fullName, data)))
                                {
                                    XmlDocument flying = new XmlDocument();
                                    flying.Load(stream);
                                    FlyingPet.flyingSpec.Add(name, flying);
                                }
                            }
                        }
                        if (fullName == $"track/common/randomTrack@{regionCode}.bml" ||
                            fullName == $"track/common/randomTrack@{regionCode}.xml" ||
                            fullName == $"track_/common/randomTrack@{regionCode}.bml" ||
                            fullName == $"track_/common/randomTrack@{regionCode}.xml"
                            )
                        {
                            Console.WriteLine(fullName);
                            byte[] data = packFileInfo.GetData();
                            using (MemoryStream stream = new MemoryStream(BmlToXml(fullName, data)))
                            {
                                RandomTrack.randomTrack = XDocument.Load(stream);
                            }
                        }
                        if (fullName == $"track/common/track@zz.bml" ||
                            fullName == $"track/common/track@zz.xml" ||
                            fullName == $"track_/common/track@zz.bml" ||
                            fullName == $"track_/common/track@zz.xml"
                            )
                        {
                            Console.WriteLine(fullName);
                            byte[] data = packFileInfo.GetData();
                            using (MemoryStream stream = new MemoryStream(BmlToXml(fullName, data)))
                            {
                                XmlDocument trackLocale = new XmlDocument();
                                trackLocale.Load(stream);
                                ProcessTrackList(trackLocale);
                            }
                        }
                        if (fullName == $"track/common/trackLocale@{regionCode}.bml" ||
                            fullName == $"track/common/trackLocale@{regionCode}.xml" ||
                            fullName == $"track_/common/trackLocale@{regionCode}.bml" ||
                            fullName == $"track_/common/trackLocale@{regionCode}.xml"
                            )
                        {
                            Console.WriteLine(fullName);
                            byte[] data = packFileInfo.GetData();
                            using (MemoryStream stream = new MemoryStream(BmlToXml(fullName, data)))
                            {
                                XmlDocument trackLocale = new XmlDocument();
                                trackLocale.Load(stream);
                                ProcessTrackLocale(trackLocale);
                            }
                        }
                        if (fullName == "etc_/itemTable.kml")
                        {
                            Console.WriteLine(fullName);
                            byte[] data = packFileInfo.GetData();
                            using (MemoryStream stream = new MemoryStream(data))
                            {
                                XDocument doc = XDocument.Load(stream);

                                var kartsWithName = doc.Descendants("kart").Where(kart => kart.Attribute("name") != null);
                                if (kartsWithName.Count() > 0)
                                {
                                    foreach (var kart in kartsWithName)
                                    {
                                        int id = int.Parse(kart.Attribute("id").Value);
                                        string name = kart.Attribute("name").Value;
                                        if (!(Kart.kartName.ContainsKey(id)))
                                        {
                                            Kart.kartName.Add(id, name);
                                        }
                                    }
                                }

                                var flyingWithName = doc.Descendants("flyingPet").Where(kart => kart.Attribute("name") != null);
                                if (flyingWithName.Count() > 0)
                                {
                                    foreach (var flyingPet in flyingWithName)
                                    {
                                        int id = int.Parse(flyingPet.Attribute("id").Value);
                                        string name = flyingPet.Attribute("name").Value;
                                        if (!(FlyingPet.flyingName.ContainsKey(id)))
                                        {
                                            FlyingPet.flyingName.Add(id, name);
                                        }
                                    }
                                }
                            }
                        }
                        if (fullName == $"etc_/itemTable@{regionCode}.xml")
                        {
                            Console.WriteLine(fullName);
                            byte[] data = packFileInfo.GetData();
                            using (MemoryStream stream = new MemoryStream(data))
                            {
                                XDocument doc = XDocument.Load(stream);
                                var kartsWithName = doc.Descendants("kart").Where(kart => kart.Attribute("name") != null);
                                if (kartsWithName.Count() > 0)
                                {
                                    foreach (var kart in kartsWithName)
                                    {
                                        int id = int.Parse(kart.Attribute("id").Value);
                                        string name = kart.Attribute("name").Value;
                                        if (Kart.kartName.ContainsKey(id))
                                        {
                                            Kart.kartName[id] = name;
                                        }
                                        else
                                        {
                                            Kart.kartName.Add(id, name);
                                        }
                                    }
                                }
                                var flyingsWithName = doc.Descendants("flyingPet").Where(flyingPet => flyingPet.Attribute("name") != null);
                                if (flyingsWithName.Count() > 0)
                                {
                                    foreach (var flyingPet in flyingsWithName)
                                    {
                                        int id = int.Parse(flyingPet.Attribute("id").Value);
                                        string name = flyingPet.Attribute("name").Value;
                                        if (FlyingPet.flyingName.ContainsKey(id))
                                        {
                                            FlyingPet.flyingName[id] = name;
                                        }
                                        else
                                        {
                                            FlyingPet.flyingName.Add(id, name);
                                        }
                                    }
                                }
                            }
                        }
                        if (fullName == $"etc_/emblem/emblem@{regionCode}.xml")
                        {
                            Console.WriteLine(fullName);
                            byte[] data = packFileInfo.GetData();
                            using (MemoryStream stream = new MemoryStream(data))
                            {
                                XmlDocument doc = new XmlDocument();
                                doc.Load(stream);
                                XmlNodeList bodyParams = doc.GetElementsByTagName("emblem");
                                if (bodyParams.Count > 0)
                                {
                                    foreach (XmlNode xn in bodyParams)
                                    {
                                        XmlElement xe = (XmlElement)xn;
                                        short id;
                                        if (short.TryParse(xe.GetAttribute("id"), out id))
                                        {
                                            if (!Emblem.emblem.Contains(id))
                                            {
                                                Emblem.emblem.Add(id);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        if (fullName == $"etc_/riderSchool/riderSchoolLocale@{regionCode}.xml")
                        {
                            Console.WriteLine(fullName);
                            byte[] data = packFileInfo.GetData();
                            using (MemoryStream stream = new MemoryStream(data))
                            {
                                XDocument xdoc = XDocument.Load(stream);

                                var validCatLevels = xdoc.Descendants("category")
                                    // 获取 catLevel 属性值，过滤掉 null 或空值
                                    .Select(c => c.Attribute("catLevel")?.Value)
                                    .Where(levelStr => !string.IsNullOrEmpty(levelStr))
                                    // 尝试转换为整数，过滤掉转换失败的（如非数字格式）
                                    .Select(levelStr =>
                                    {
                                        byte.TryParse(levelStr, out byte level);
                                        return level;
                                    })
                                    // 过滤掉转换后为 0 的无效值（默认转换失败返回 0）
                                    .Where(level => level > 0);

                                if (validCatLevels.Any())
                                {
                                    RiderSchool.catLevel = validCatLevels.Max();
                                }

                                // 筛选 catLevel='6' 的 category 节点
                                var targetCategory = xdoc.Descendants("category")
                                    .FirstOrDefault(c => c.Attribute("catLevel")?.Value == RiderSchool.catLevel.ToString());

                                if (targetCategory != null)
                                {
                                    List<byte> validSteps = targetCategory.Descendants("item")
                                       .Select(item => item.Attribute("step")?.Value)
                                       .Where(stepStr => !string.IsNullOrEmpty(stepStr))
                                       .Select(stepStr =>
                                       {
                                           byte.TryParse(stepStr, out byte step);
                                           return step;
                                       })
                                       .Where(step => step != 0) // 过滤转换失败的无效值
                                       .OrderBy(step => step)   // 升序排序
                                       .ToList();

                                    if (validSteps.Any())
                                    {
                                        RiderSchool.maxStep = validSteps.Max();

                                        // 按索引拆分：偶数列（索引 0、2、4...）
                                        RiderSchool.evenProStep = validSteps
                                            .Where((step, index) => index % 2 == 0) // 索引%2==0 → 偶索引
                                            .ToList();

                                        // 按索引拆分：奇数列（索引 1、3、5...）
                                        RiderSchool.oddProStep = validSteps
                                            .Where((step, index) => index % 2 != 0) // 索引%2!=0 → 奇索引
                                            .ToList();
                                    }
                                }
                            }
                        }
                        if (fullName.Contains("kart_") && fullName.Contains($"/param@{regionCode}.xml"))
                        {
                            Console.WriteLine(fullName);
                            string name = fullName.Substring(6, fullName.Length - 19);
                            if (!(Kart.kartSpec.ContainsKey(name)))
                            {
                                byte[] data = ReplaceBytes(packFileInfo.GetData());
                                if (data[2] == 13 && data[3] == 0 && data[4] == 10 && data[5] == 0)
                                {
                                    byte[] newBytes = new byte[data.Length - 4];
                                    newBytes[0] = 255;
                                    newBytes[1] = 254;
                                    Array.Copy(data, 6, newBytes, 2, data.Length - 6);
                                    using (MemoryStream stream = new MemoryStream(newBytes))
                                    {
                                        XmlDocument kart1 = new XmlDocument();
                                        kart1.Load(stream);
                                        Kart.kartSpec.Add(name, kart1);
                                    }
                                }
                                else
                                {
                                    using (MemoryStream stream = new MemoryStream(data))
                                    {
                                        XmlDocument kart2 = new XmlDocument();
                                        kart2.Load(stream);
                                        Kart.kartSpec.Add(name, kart2);
                                    }
                                }
                            }
                        }
                        if (fullName.Contains("kart_") && fullName.Contains($"/param@{regionCode}.kml"))
                        {
                            string name = fullName.Substring(6, fullName.Length - 19);
                            bool containsTarget = packFolderInfo1.GetFilesInfo().Any(PackFileInfo => ReplacePath(PackFileInfo.FullName) == $"kart_/{name}/param@{regionCode}.xml");
                            if (!containsTarget)
                            {
                                Console.WriteLine(fullName);
                                if (!(Kart.kartSpec.ContainsKey(name)))
                                {
                                    byte[] data = ReplaceBytes(packFileInfo.GetData());
                                    if (data[2] == 13 && data[3] == 0 && data[4] == 10 && data[5] == 0)
                                    {
                                        byte[] newBytes = new byte[data.Length - 4];
                                        newBytes[0] = 255;
                                        newBytes[1] = 254;
                                        Array.Copy(data, 6, newBytes, 2, data.Length - 6);
                                        using (MemoryStream stream = new MemoryStream(newBytes))
                                        {
                                            XmlDocument kart1 = new XmlDocument();
                                            kart1.Load(stream);
                                            Kart.kartSpec.Add(name, kart1);
                                        }
                                    }
                                    else
                                    {
                                        using (MemoryStream stream = new MemoryStream(data))
                                        {
                                            XmlDocument kart2 = new XmlDocument();
                                            kart2.Load(stream);
                                            Kart.kartSpec.Add(name, kart2);
                                        }
                                    }
                                }
                            }
                        }
                        if (fullName.Contains("kart_") && fullName.Contains("/param.xml"))
                        {
                            string name = fullName.Substring(6, fullName.Length - 16);
                            bool containsTarget = packFolderInfo1.GetFilesInfo().Any(PackFileInfo => ReplacePath(PackFileInfo.FullName) == $"kart_/{name}/param@{regionCode}.xml" || ReplacePath(PackFileInfo.FullName) == $"kart_/{name}/param@{regionCode}.kml");
                            if (!containsTarget)
                            {
                                Console.WriteLine(fullName);
                                if (!(Kart.kartSpec.ContainsKey(name)))
                                {
                                    byte[] data = ReplaceBytes(packFileInfo.GetData());
                                    if (data[2] == 13 && data[3] == 0 && data[4] == 10 && data[5] == 0)
                                    {
                                        byte[] newBytes = new byte[data.Length - 4];
                                        newBytes[0] = 255;
                                        newBytes[1] = 254;
                                        Array.Copy(data, 6, newBytes, 2, data.Length - 6);
                                        using (MemoryStream stream = new MemoryStream(newBytes))
                                        {
                                            XmlDocument kart1 = new XmlDocument();
                                            kart1.Load(stream);
                                            Kart.kartSpec.Add(name, kart1);
                                        }
                                    }
                                    else
                                    {
                                        using (MemoryStream stream = new MemoryStream(data))
                                        {
                                            XmlDocument kart2 = new XmlDocument();
                                            kart2.Load(stream);
                                            Kart.kartSpec.Add(name, kart2);
                                        }
                                    }
                                }
                            }
                        }
                        if (fullName == $"zeta/{regionCode}/quest/QuestAutomation.bml")
                        {
                            Console.WriteLine(fullName);
                            byte[] data = packFileInfo.GetData();
                            using (MemoryStream stream = new MemoryStream(BmlToXml(fullName, data)))
                            {
                                XmlDocument Quest = new XmlDocument();
                                Quest.Load(stream);
                                GameSupport.QuestParams = Quest.GetElementsByTagName("QuestItem");
                            }
                        }
                        if (fullName == $"zeta/{regionCode}/quest/kartPassQuestAutomation.bml")
                        {
                            Console.WriteLine(fullName);
                            byte[] data = packFileInfo.GetData();
                            using (MemoryStream stream = new MemoryStream(BmlToXml(fullName, data)))
                            {
                                XDocument doc = XDocument.Load(stream);
                                GameSupport.questInfo = doc.Descendants("kartPassQuestInfo").First();
                            }
                        }
                        if (fullName == $"zeta/{regionCode}/scenario/scenario.bml")
                        {
                            Console.WriteLine(fullName);
                            byte[] data = packFileInfo.GetData();
                            using (MemoryStream stream = new MemoryStream(BmlToXml(fullName, data)))
                            {
                                XmlDocument Scenario = new XmlDocument();
                                Scenario.Load(stream);
                                XmlNodeList ScenarioParams = Scenario.GetElementsByTagName("Chapter");
                                if (ScenarioParams.Count > 0)
                                {
                                    foreach (XmlNode xn in ScenarioParams)
                                    {
                                        XmlElement xe = (XmlElement)xn;
                                        int id = int.Parse(xe.GetAttribute("id"));
                                        if (!(GameSupport.scenario.Contains(id)))
                                        {
                                            GameSupport.scenario.Add(id);
                                        }
                                    }
                                }
                            }
                        }
                        if (fullName == "item/slot/itemProb_indi@zz.bml")
                        {
                            Console.WriteLine(fullName);
                            byte[] data = packFileInfo.GetData();
                            using (MemoryStream stream = new MemoryStream(BmlToXml(fullName, data)))
                            {
                                XDocument doc = XDocument.Load(stream);
                                foreach (var item in doc.Descendants("item"))
                                {
                                    // 获取 idx 属性值
                                    string idxValue = item.Attribute("idx")?.Value;

                                    // 验证并转换为 short 类型
                                    if (short.TryParse(idxValue, out short idx))
                                    {
                                        MultyPlayer.itemProb_indi.Add(idx);
                                    }
                                    else
                                    {
                                        Console.WriteLine($"无法将 '{idxValue}' 转换为 short 类型");
                                    }
                                }
                            }
                        }
                        if (fullName == "item/slot/itemProb_team@zz.bml")
                        {
                            Console.WriteLine(fullName);
                            byte[] data = packFileInfo.GetData();
                            using (MemoryStream stream = new MemoryStream(BmlToXml(fullName, data)))
                            {
                                XDocument doc = XDocument.Load(stream);
                                foreach (var item in doc.Descendants("item"))
                                {
                                    // 获取 idx 属性值
                                    string idxValue = item.Attribute("idx")?.Value;

                                    // 验证并转换为 short 类型
                                    if (short.TryParse(idxValue, out short idx))
                                    {
                                        MultyPlayer.itemProb_team.Add(idx);
                                    }
                                    else
                                    {
                                        Console.WriteLine($"无法将 '{idxValue}' 转换为 short 类型");
                                    }
                                }
                            }
                        }
                        if (fullName == $"zeta_/{regionCode}/content/basicAI.xml")
                        {
                            Console.WriteLine(fullName);
                            byte[] data = packFileInfo.GetData();
                            using (MemoryStream stream = new MemoryStream(data))
                            {
                                XDocument doc = XDocument.Load(stream);
                                XElement aiItem = doc.Descendants("aiItem").First();

                                // 角色Dictionary：键为short类型的角色ID
                                var aiCharacterDict = aiItem.Elements("character")
                                    .ToDictionary(
                                        c => short.Parse(c.Attribute("id").Value),  // 键：short类型ID
                                        c => new AICharacter
                                        {
                                            Id = short.Parse(c.Attribute("id").Value),
                                            Rids = c.Elements("rid").Select(rid => rid.Attribute("name").Value).ToList(),
                                            Balloons = c.Elements("balloon").Select(b => new AIAccessory
                                            {
                                                Id = short.Parse(b.Attribute("id").Value),
                                                Speed = int.Parse(b.Attribute("speed").Value),
                                                Item = int.Parse(b.Attribute("item").Value)
                                            }).ToList(),
                                            Headbands = c.Elements("headband").Select(h => new AIAccessory
                                            {
                                                Id = short.Parse(h.Attribute("id").Value),
                                                Speed = int.Parse(h.Attribute("speed").Value),
                                                Item = int.Parse(h.Attribute("item").Value)
                                            }).ToList(),
                                            Goggles = c.Elements("goggle").Select(g => new AIAccessory
                                            {
                                                Id = short.Parse(g.Attribute("id").Value),
                                                Speed = int.Parse(g.Attribute("speed").Value),
                                                Item = int.Parse(g.Attribute("item").Value)
                                            }).ToList()
                                        }
                                    );
                                MultyPlayer.aiCharacterDict = aiCharacterDict;

                                // 卡丁车Dictionary：键为short类型的卡丁车ID
                                var aiKartDict = aiItem.Elements("kart")
                                    .ToDictionary(
                                        k => short.Parse(k.Attribute("id").Value),  // 键：short类型ID
                                        k => new AIKart
                                        {
                                            Id = short.Parse(k.Attribute("id").Value),
                                            Speed = int.Parse(k.Attribute("speed").Value),
                                            Item = int.Parse(k.Attribute("item").Value),
                                        }
                                    );
                                MultyPlayer.aiKartDict = aiKartDict;
                            }
                        }
                        if (fullName == $"zeta_/{regionCode}/content/itemDictionary.xml")
                        {
                            Console.WriteLine(fullName);
                            byte[] data = packFileInfo.GetData();
                            using (MemoryStream stream = new MemoryStream(data))
                            {
                                XDocument doc = XDocument.Load(stream);
                                var items = doc.Descendants("item");
                                foreach (var item in items)
                                {
                                    short catId = short.Parse(item.Attribute("catId")?.Value ?? "0");
                                    string valuesStr = item.Attribute("values")?.Value;
                                    string[] valuesArray = valuesStr?.Split(',');
                                    for (int i = 0; i < valuesArray.Count(); i++)
                                    {
                                        List<short> Add = new List<short> { catId, short.Parse(valuesArray[i]) };
                                        GameSupport.Dictionary.Add(Add);
                                    }
                                }
                            }
                        }
                        if (fullName == $"zeta_/{regionCode}/content/timeAttack/timeAttackMission.xml")
                        {
                            Console.WriteLine(fullName);
                            byte[] data = packFileInfo.GetData();
                            using (MemoryStream stream = new MemoryStream(data))
                            {
                                // 加载文档并解析任务
                                TimeAttack.timeAttackMission = XDocument.Load(stream);
                            }
                        }
                        if (fullName == $"zeta_/{regionCode}/content/timeAttack/timeAttackCompetitive.xml")
                        {
                            Console.WriteLine(fullName);
                            byte[] data = packFileInfo.GetData();
                            using (MemoryStream stream = new MemoryStream(data))
                            {
                                // 加载文档并解析任务
                                TimeAttack.timeAttackCompetitive = XDocument.Load(stream);
                            }
                        }
                        if (fullName == $"zeta_/{regionCode}/content/timeAttack/timeAttackCompetitiveData.xml")
                        {
                            Console.WriteLine(fullName);
                            byte[] data = packFileInfo.GetData();
                            using (MemoryStream stream = new MemoryStream(data))
                            {
                                // 加载文档并解析任务
                                TimeAttack.timeAttackCompetitiveData = XDocument.Load(stream);
                            }
                        }
                        if (fullName == $"zeta_/{regionCode}/lottery/lottery.xml")
                        {
                            Console.WriteLine(fullName);
                            byte[] data = packFileInfo.GetData();
                            using (MemoryStream stream = new MemoryStream(data))
                            {
                                // 创建XML文档对象
                                XmlDocument doc = new XmlDocument();
                                doc.Load(stream);

                                // 获取所有rewardSet节点
                                XmlNodeList rewardSetNodes = doc.GetElementsByTagName("lottery");
                                XmlNode targetRewardSet = null;

                                var BingoLotteryID = Bingo.BingoLotteryIDs[Bingo.BingoLotteryIDs.Length - 1].ToString();

                                // 查找指定id的rewardSet
                                foreach (XmlNode node in rewardSetNodes)
                                {
                                    XmlElement rewardSetElement = node as XmlElement;

                                    if (rewardSetElement != null && rewardSetElement.GetAttribute("id") == BingoLotteryID)
                                    {
                                        targetRewardSet = node;
                                        break;
                                    }
                                }
                                if (targetRewardSet == null)
                                {
                                    Console.WriteLine($"未找到ID为{BingoLotteryID}的lottery节点");
                                }
                                else
                                {
                                    // 获取该rewardSet下的所有reward节点
                                    XmlNodeList rewardNodes = targetRewardSet.SelectNodes("./rewardSet/reward");
                                    LotteryManager.Initialize(rewardNodes);
                                }
                            }
                        }
                        if (fullName == $"zeta_/{regionCode}/shop/data/item.kml")
                        {
                            Console.WriteLine(fullName);
                            byte[] data = packFileInfo.GetData();
                            using (MemoryStream stream = new MemoryStream(data))
                            {
                                XmlDocument doc = new XmlDocument();
                                doc.Load(stream);
                                XmlNodeList bodyParams = doc.GetElementsByTagName("item");
                                if (bodyParams.Count > 0)
                                {
                                    foreach (XmlNode xn in bodyParams)
                                    {
                                        XmlElement xe = (XmlElement)xn;
                                        ushort itemCatId = ushort.Parse(xe.GetAttribute("itemCatId"));
                                        ushort itemId = ushort.Parse(xe.GetAttribute("itemId"));
                                        string itemName = xe.GetAttribute("itemName");
                                        if (!NewRider.items.ContainsKey(itemCatId))
                                        {
                                            NewRider.items[itemCatId] = new Dictionary<ushort, string>();
                                        }
                                        NewRider.items[itemCatId][itemId] = itemName;
                                    }
                                }
                            }
                        }
                        if (fullName == $"zeta_/{regionCode}/content/channel.xml")
                        {
                            Console.WriteLine(fullName);
                            DateTime now = DateTime.Now;
                            byte[] data = packFileInfo.GetData();
                            byte i = 1;
                            using (MemoryStream stream = new MemoryStream(data))
                            {
                                XDocument doc = XDocument.Load(stream);

                                // 遍历所有Channel节点
                                foreach (var channel in doc.Descendants("Channel"))
                                {
                                    XAttribute openPeriodAttr = channel.Attribute("openPeriod");

                                    var channelValue = new Channel
                                    {
                                        Name = channel.Attribute("name").Value,
                                        CreateSpeed = byte.Parse(channel.Attribute("createSpeed").Value),
                                        GameType = byte.Parse(channel.Attribute("gameType").Value),
                                    };

                                    // 情况1：无openPeriod属性 → 直接加入结果
                                    if (openPeriodAttr == null)
                                    {
                                        GameSupport.Channels.TryAdd(i++, channelValue);
                                        continue;
                                    }

                                    // 情况2：有openPeriod属性 → 判断时间范围
                                    string openPeriod = openPeriodAttr.Value;
                                    string[] periodParts = openPeriod.Split('~');
                                    // 解析开始时间（格式：yyyy-MM-ddTHH:mm:ss）
                                    if (periodParts.Length != 2 || !DateTime.TryParse(periodParts[0], out DateTime startTime))
                                    {
                                        Console.WriteLine($"警告：{channelValue.Name} 的openPeriod格式错误，跳过该节点：{openPeriod}");
                                        continue;
                                    }

                                    // 当前时间 >= 开始时间 → 加入结果（*表示无结束时间）
                                    if (now >= startTime)
                                    {
                                        GameSupport.Channels.TryAdd(i++, channelValue);
                                    }
                                }
                            }
                        }
                    }
                    foreach (PackFolderInfo packFolderInfo2 in packFolderInfo1.GetFoldersInfo())
                        packFolderInfoQueue.Enqueue(packFolderInfo2);
                }
                return packFolderManager;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                return null;
            }
        }

        public static bool TryExportKartCatalogXmlReadOnly(
            string gameRootOrAaaPath,
            string outputXmlPath,
            out int names,
            out int specs,
            out string error)
        {
            names = 0;
            specs = 0;
            error = string.Empty;
            if (!TryExtractKartCatalogReadOnly(
                    gameRootOrAaaPath,
                    out Dictionary<int, string> extractedNames,
                    out Dictionary<string, XmlDocument> extractedSpecs,
                    out List<KartCatalogAbilityRule> extractedAbilities,
                    out List<KartCatalogItemSymbol> extractedItemSymbols,
                    out List<KartCatalogInventoryItem> extractedInventory,
                    out string extractedRegion,
                    out string extractedSourcePath,
                    out error))
            {
                return false;
            }

            try
            {
                XElement namesElement = new XElement(
                    "Names",
                    extractedNames
                        .OrderBy(entry => entry.Key)
                        .Select(entry => new XElement(
                            "Kart",
                            new XAttribute("id", entry.Key),
                            new XAttribute("name", entry.Value))));
                XElement specsElement = new XElement("Specs");
                foreach (var entry in extractedSpecs.OrderBy(
                    entry => entry.Key,
                    StringComparer.OrdinalIgnoreCase))
                {
                    XmlElement documentElement = entry.Value.DocumentElement
                        ?? throw new InvalidDataException(
                            $"kart parameter has no root element: {entry.Key}");
                    specsElement.Add(new XElement(
                        "Spec",
                        new XAttribute("name", entry.Key),
                        XElement.Parse(documentElement.OuterXml, LoadOptions.PreserveWhitespace)));
                }
                XElement itemSymbolsElement = CreateItemSymbolsXml(extractedItemSymbols);
                XElement abilitiesElement = CreateAbilitiesXml(extractedAbilities);
                XElement inventoryElement = CreateInventoryXml(extractedInventory);
                string unpackedExecutablePath = ResolveUnpackedExecutablePath(
                    Path.GetDirectoryName(extractedSourcePath)
                        ?? throw new InvalidDataException("aaa.pk has no parent directory"));
                XAttribute executableHashAttribute = File.Exists(unpackedExecutablePath)
                    ? new XAttribute(
                        "sourceExecutableSha256",
                        ComputeFileSha256(unpackedExecutablePath))
                    : null;
                XElement catalogRoot = new XElement(
                    "KartCatalog",
                    new XAttribute("formatVersion", KartCatalogFormatVersion),
                    new XAttribute("protocolVersion", KartCatalogProtocolVersion),
                    new XAttribute("region", extractedRegion),
                    new XAttribute("sourceAaaSha256", ComputeFileSha256(extractedSourcePath)),
                    executableHashAttribute,
                    namesElement,
                    specsElement,
                    inventoryElement,
                    itemSymbolsElement,
                    abilitiesElement);

                XDocument catalog = new XDocument(
                    new XDeclaration("1.0", "utf-8", null),
                    catalogRoot);

                using MemoryStream output = new MemoryStream();
                using (XmlWriter writer = XmlWriter.Create(
                    output,
                    new XmlWriterSettings
                    {
                        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                        Indent = true,
                        NewLineChars = "\r\n",
                        NewLineHandling = NewLineHandling.Replace
                    }))
                {
                    catalog.Save(writer);
                }

                string fullOutputPath = Path.GetFullPath(outputXmlPath);
                string outputDirectory = Path.GetDirectoryName(fullOutputPath)
                    ?? throw new InvalidDataException("output XML has no parent directory");
                Directory.CreateDirectory(outputDirectory);
                string stagedOutputPath = fullOutputPath + ".new";
                try
                {
                    using (FileStream stagedOutput = new FileStream(
                        stagedOutputPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None))
                    {
                        byte[] serializedCatalog = output.ToArray();
                        stagedOutput.Write(serializedCatalog, 0, serializedCatalog.Length);
                        stagedOutput.Flush(flushToDisk: true);
                    }
                    File.Move(stagedOutputPath, fullOutputPath, overwrite: true);
                }
                finally
                {
                    if (File.Exists(stagedOutputPath))
                    {
                        File.Delete(stagedOutputPath);
                    }
                }
                names = extractedNames.Count;
                specs = extractedSpecs.Count;
                Console.WriteLine(
                    $"[RHO kart catalog] XML exported: names={names}, specs={specs}, " +
                    $"inventory={extractedInventory.Count}/{extractedInventory.Select(item => item.Category).Distinct().Count()} categories, " +
                    $"abilities={extractedAbilities.Count}/" +
                    $"{extractedAbilities.Count(rule => rule.IsRuntimeResolved)} resolved, " +
                    $"path={fullOutputPath}");
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Console.WriteLine($"[RHO kart catalog] XML export failed: {ex.Message}");
                return false;
            }
        }

        public static bool TryLoadKartCatalogXml(
            string xmlPath,
            out int names,
            out int specs,
            out string error)
        {
            names = 0;
            specs = 0;
            error = string.Empty;

            try
            {
                string fullPath = Path.GetFullPath(xmlPath);
                if (!File.Exists(fullPath))
                {
                    throw new FileNotFoundException("kart catalog XML was not found", fullPath);
                }

                using XmlReader reader = XmlReader.Create(
                    fullPath,
                    new XmlReaderSettings
                    {
                        DtdProcessing = DtdProcessing.Prohibit,
                        XmlResolver = null,
                        MaxCharactersInDocument = 64L * 1024L * 1024L
                    });
                XDocument catalog = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
                XElement root = catalog.Root
                    ?? throw new InvalidDataException("kart catalog XML has no root element");
                if (!root.Name.LocalName.Equals("KartCatalog", StringComparison.Ordinal) ||
                    root.Attribute("formatVersion")?.Value != KartCatalogFormatVersion ||
                    root.Attribute("protocolVersion")?.Value != KartCatalogProtocolVersion ||
                    root.Attribute("region")?.Value != KartCatalogRegion)
                {
                    throw new InvalidDataException(
                        $"kart catalog is not a Korean protocol 5136 format-{KartCatalogFormatVersion} catalog");
                }
                ValidateCatalogSourceFingerprints(root, fullPath);

                var loadedNames = new Dictionary<int, string>();
                foreach (XElement kart in root.Element("Names")?.Elements("Kart")
                    ?? Enumerable.Empty<XElement>())
                {
                    if (!int.TryParse(
                            kart.Attribute("id")?.Value,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out int id))
                    {
                        continue;
                    }

                    string name = kart.Attribute("name")?.Value;
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        loadedNames[id] = name;
                    }
                }

                var loadedSpecs = new Dictionary<string, XmlDocument>(StringComparer.OrdinalIgnoreCase);
                foreach (XElement spec in root.Element("Specs")?.Elements("Spec")
                    ?? Enumerable.Empty<XElement>())
                {
                    string name = spec.Attribute("name")?.Value;
                    XElement parameter = spec.Elements().FirstOrDefault();
                    if (string.IsNullOrWhiteSpace(name) || parameter == null)
                    {
                        continue;
                    }

                    XmlDocument document = new XmlDocument();
                    document.LoadXml(parameter.ToString(SaveOptions.DisableFormatting));
                    if (document.GetElementsByTagName("BodyParam").Count > 0)
                    {
                        loadedSpecs[name] = document;
                    }
                }

                Dictionary<string, KartCatalogItemSymbol> loadedItemSymbols =
                    ParseItemSymbolsXml(root);
                List<KartCatalogInventoryItem> loadedInventory = ParseInventoryXml(root);
                List<KartCatalogAbilityRule> loadedAbilities = ParseAbilitiesXml(
                    root,
                    loadedItemSymbols);
                ValidateKartCatalogContents(
                    root.Attribute("region")!.Value,
                    loadedNames,
                    loadedSpecs,
                    loadedInventory,
                    loadedItemSymbols.Values,
                    loadedAbilities);

                // Publish only after complete validation so a truncated/broken
                // local XML cannot erase the catalog already in use.
                Kart.kartName = loadedNames;
                Kart.kartSpec = loadedSpecs;
                KartCatalogInventory.Publish(loadedInventory);
                KartCatalogAbilities.Publish(loadedAbilities);
                regionCode = KartCatalogRegion;

                names = loadedNames.Count;
                specs = loadedSpecs.Count;
                Console.WriteLine(
                    $"[RHO kart catalog] XML loaded: region={regionCode}, names={names}, " +
                    $"specs={specs}, inventory={KartCatalogInventory.TotalItemCount}/" +
                    $"{KartCatalogInventory.CategoryCount} categories, " +
                    $"abilities={KartCatalogAbilities.TotalRuleCount}/" +
                    $"{KartCatalogAbilities.ResolvedRuleCount} resolved, path={fullPath}");
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Console.WriteLine(
                    $"[RHO kart catalog] XML load failed; keeping current catalog: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Extracts kart metadata from the client's archives without modifying
        /// aaa.pk, any RHO/RHO5 package, or the live server catalog.
        /// </summary>
        private static bool TryExtractKartCatalogReadOnly(
            string gameRootOrAaaPath,
            out Dictionary<int, string> extractedNames,
            out Dictionary<string, XmlDocument> extractedSpecs,
            out List<KartCatalogAbilityRule> extractedAbilities,
            out List<KartCatalogItemSymbol> extractedItemSymbols,
            out List<KartCatalogInventoryItem> extractedInventory,
            out string extractedRegion,
            out string sourcePath,
            out string error)
        {
            extractedNames = new Dictionary<int, string>();
            extractedSpecs = new Dictionary<string, XmlDocument>(StringComparer.OrdinalIgnoreCase);
            extractedAbilities = new List<KartCatalogAbilityRule>();
            extractedItemSymbols = new List<KartCatalogItemSymbol>();
            extractedInventory = new List<KartCatalogInventoryItem>();
            extractedRegion = string.Empty;
            sourcePath = string.Empty;
            error = string.Empty;
            PackFolderManager packFolderManager = null;
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                string fullPath = ResolveAaaPkPath(gameRootOrAaaPath);
                sourcePath = fullPath;
                string catalogRegion = GetRegionCode(fullPath);
                string dataDirectory = Path.GetDirectoryName(fullPath)
                    ?? throw new InvalidDataException("aaa.pk has no parent directory");
                string kartRhoPath = Path.Combine(dataDirectory, "kart.rho");
                CountryCode countryCode = catalogRegion.ToLowerInvariant() switch
                {
                    "kr" => CountryCode.KR,
                    "tw" => CountryCode.TW,
                    "cn" => CountryCode.CN,
                    _ => CountryCode.CN
                };

                // The 5136 base specs all live in kart.rho.  Opening only that
                // archive avoids retaining streams for every RHO referenced by
                // aaa.pk (more than 1,500 files in the Korean client).
                packFolderManager = new PackFolderManager();
                packFolderManager.OpenSingleFile(kartRhoPath, countryCode);

                var nameCandidates = new List<(int Priority, string Path, byte[] Data)>();
                var specCandidates = new List<(int Priority, string KartName, string Path, byte[] Data)>();
                var inventoryCandidates = new List<(string Archive, string Path, byte[] Data)>();
                Queue<PackFolderInfo> folders = new Queue<PackFolderInfo>();
                folders.Enqueue(packFolderManager.GetRootFolder());

                while (folders.Count > 0)
                {
                    PackFolderInfo folder = folders.Dequeue();
                    foreach (PackFileInfo file in folder.GetFilesInfo())
                    {
                        string path = ReplacePath(file.FullName)
                            .Replace('\\', '/')
                            .TrimStart('/');
                        string fileName = path.Substring(path.LastIndexOf('/') + 1);

                        int itemTablePriority = GetCatalogFilePriority(
                            fileName,
                            "itemTable",
                            catalogRegion);
                        if (itemTablePriority > 0)
                        {
                            nameCandidates.Add((itemTablePriority, path, file.GetData()));
                        }

                        if (TryGetKartParamCandidate(
                                path,
                                catalogRegion,
                                out string kartName,
                                out int specPriority))
                        {
                            specCandidates.Add((specPriority, kartName, path, file.GetData()));
                        }
                    }

                    foreach (PackFolderInfo child in folder.GetFoldersInfo())
                    {
                        folders.Enqueue(child);
                    }
                }

                // Locale item tables and a small number of corrected locale
                // params are overlays in RHO5 packages.  Scan only their file
                // metadata and materialize matching entries before disposing
                // each package.
                foreach (string rho5Path in Directory
                    .EnumerateFiles(dataDirectory, "*.rho5")
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        using Rho5 rho5 = new Rho5(rho5Path, countryCode);
                        foreach (Rho5FileInfo file in rho5.Files)
                        {
                            string path = file.FullPath.Replace('\\', '/').TrimStart('/');
                            string fileName = path.Substring(path.LastIndexOf('/') + 1);
                            int itemTablePriority = GetCatalogFilePriority(
                                fileName,
                                "itemTable",
                                catalogRegion);
                            bool isKartParam = TryGetKartParamCandidate(
                                path,
                                catalogRegion,
                                out string kartName,
                                out int specPriority);
                            bool isInventory = path.Equals(
                                $"zeta_/{catalogRegion}/shop/data/item.kml",
                                StringComparison.OrdinalIgnoreCase);
                            if (itemTablePriority <= 0 && !isKartParam && !isInventory)
                            {
                                continue;
                            }

                            try
                            {
                                byte[] data = file.GetData();
                                if (itemTablePriority > 0)
                                {
                                    nameCandidates.Add((itemTablePriority, path, data));
                                }
                                if (isKartParam)
                                {
                                    specCandidates.Add((specPriority, kartName, path, data));
                                }
                                if (isInventory)
                                {
                                    inventoryCandidates.Add((Path.GetFileName(rho5Path), path, data));
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(
                                    $"[RHO kart catalog] overlay entry skipped: {path} ({ex.Message})");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"[RHO kart catalog] overlay skipped: {Path.GetFileName(rho5Path)} " +
                            $"({ex.Message})");
                    }
                }

                extractedAbilities.AddRange(ExtractKartAbilityRules(
                    dataDirectory,
                    countryCode,
                    catalogRegion,
                    packFolderManager,
                    out extractedItemSymbols));

                var loadedNames = new Dictionary<int, (int Priority, string Name)>();
                foreach (var candidate in nameCandidates.OrderBy(candidate => candidate.Priority))
                {
                    try
                    {
                        byte[] data = GetCatalogXml(candidate.Path, candidate.Data);
                        XmlDocument document = LoadCatalogXmlDocument(data);
                        foreach (XmlNode node in document.GetElementsByTagName("kart"))
                        {
                            if (node is not XmlElement kart)
                            {
                                continue;
                            }

                            if (!int.TryParse(
                                    kart.GetAttribute("id"),
                                    NumberStyles.Integer,
                                    CultureInfo.InvariantCulture,
                                    out int id))
                            {
                                continue;
                            }

                            string name = kart.GetAttribute("name");
                            if (string.IsNullOrWhiteSpace(name))
                            {
                                continue;
                            }

                            if (!loadedNames.TryGetValue(id, out var current) ||
                                candidate.Priority >= current.Priority)
                            {
                                loadedNames[id] = (candidate.Priority, name);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"[RHO kart catalog] item table skipped: {candidate.Path} ({ex.Message})");
                    }
                }

                var loadedSpecs = new Dictionary<string, (int Priority, XmlDocument Document)>(
                    StringComparer.OrdinalIgnoreCase);
                foreach (var candidate in specCandidates.OrderBy(candidate => candidate.Priority))
                {
                    try
                    {
                        byte[] data = GetCatalogXml(candidate.Path, candidate.Data);
                        XmlDocument document = LoadCatalogXmlDocument(data);
                        if (document.GetElementsByTagName("BodyParam").Count == 0)
                        {
                            continue;
                        }

                        if (!loadedSpecs.TryGetValue(candidate.KartName, out var current) ||
                            candidate.Priority >= current.Priority)
                        {
                            loadedSpecs[candidate.KartName] = (candidate.Priority, document);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"[RHO kart catalog] kart parameter skipped: {candidate.Path} ({ex.Message})");
                    }
                }

                if (loadedNames.Count == 0 || loadedSpecs.Count == 0)
                {
                    throw new InvalidDataException(
                        $"kart metadata was not found (names={loadedNames.Count}, specs={loadedSpecs.Count})");
                }

                foreach (var entry in loadedNames)
                {
                    extractedNames[entry.Key] = entry.Value.Name;
                }

                foreach (var entry in loadedSpecs)
                {
                    extractedSpecs[entry.Key] = entry.Value.Document;
                }

                extractedInventory.AddRange(ExtractCatalogInventory(
                    inventoryCandidates,
                    extractedNames));

                extractedRegion = catalogRegion;
                ValidateKartCatalogContents(
                    extractedRegion,
                    extractedNames,
                    extractedSpecs,
                    extractedInventory,
                    extractedItemSymbols,
                    extractedAbilities);
                stopwatch.Stop();
                Console.WriteLine(
                    $"[RHO kart catalog] extracted read-only: region={catalogRegion}, " +
                    $"names={extractedNames.Count}, specs={extractedSpecs.Count}, " +
                    $"inventory={extractedInventory.Count}/" +
                    $"{extractedInventory.Select(item => item.Category).Distinct().Count()} categories, " +
                    $"abilities={extractedAbilities.Count}/" +
                    $"{extractedAbilities.Count(rule => rule.IsRuntimeResolved)} resolved, " +
                    $"elapsed={stopwatch.ElapsedMilliseconds}ms, source={fullPath}");
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Console.WriteLine(
                    $"[RHO kart catalog] extraction failed: {ex.Message}");
                return false;
            }
            finally
            {
                // PackFolderManager keeps every opened RHO stream alive.  The
                // server only needs detached strings/XmlDocuments after startup.
                packFolderManager?.Reset();
            }
        }

        private static List<KartCatalogInventoryItem> ExtractCatalogInventory(
            IReadOnlyList<(string Archive, string Path, byte[] Data)> candidates,
            IReadOnlyDictionary<int, string> names)
        {
            if (candidates == null || candidates.Count == 0)
            {
                throw new InvalidDataException(
                    "P5136 shop inventory zeta_/kr/shop/data/item.kml was not found in RHO5");
            }

            (string Archive, string Path, byte[] Data) selected = candidates[candidates.Count - 1];
            byte[] xml = GetCatalogXml(selected.Path, selected.Data);
            XmlDocument document = LoadCatalogXmlDocument(xml);
            XmlNodeList nodes = document.GetElementsByTagName("item");
            var items = new Dictionary<(ushort Category, ushort Id), KartCatalogInventoryItem>();
            foreach (XmlNode node in nodes)
            {
                if (node is not XmlElement element ||
                    !ushort.TryParse(
                        element.GetAttribute("itemCatId"),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out ushort category) ||
                    !ushort.TryParse(
                        element.GetAttribute("itemId"),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out ushort id) ||
                    id == 0)
                {
                    throw new InvalidDataException(
                        $"shop inventory contains an invalid item row in {selected.Path}");
                }

                // The shop table is the discovery catalog, not an unconditional
                // ownership allowlist: it also contains UI-internal character
                // assets.  The P5136 grant layer filters those rows.  Every shop
                // kart must still resolve through the client kart-name table;
                // four legitimate shop karts have no BodyParam and intentionally
                // use the existing physics fallback when selected.
                if (category == 3 && !names.ContainsKey(id))
                {
                    throw new InvalidDataException(
                        $"shop inventory kart {id} is missing from the kart-name table");
                }

                var item = new KartCatalogInventoryItem
                {
                    Category = category,
                    Id = id,
                    Serial = 0,
                    Name = element.GetAttribute("itemName") ?? string.Empty
                };
                if (!items.TryAdd((category, id), item))
                {
                    throw new InvalidDataException(
                        $"shop inventory contains duplicate item {category}:{id}");
                }
            }

            List<KartCatalogInventoryItem> result = items.Values
                .OrderBy(item => item.Category)
                .ThenBy(item => item.Id)
                .ToList();
            Console.WriteLine(
                $"[RHO kart catalog] shop inventory selected: archive={selected.Archive}, " +
                $"items={result.Count}, categories={result.Select(item => item.Category).Distinct().Count()}, " +
                $"karts={result.Count(item => item.Category == 3)}");
            return result;
        }

        private static List<KartCatalogAbilityRule> ExtractKartAbilityRules(
            string dataDirectory,
            CountryCode countryCode,
            string catalogRegion,
            PackFolderManager packFolderManager,
            out List<KartCatalogItemSymbol> itemSymbols)
        {
            string itemRhoPath = Path.Combine(dataDirectory, "item.rho");
            packFolderManager.Reset();
            packFolderManager.OpenSingleFile(itemRhoPath, countryCode);

            var candidates = new List<(
                KartCatalogAbilityKind Kind,
                int Priority,
                string Path,
                byte[] Data)>();
            var symbolCandidates = new List<(string Path, byte[] Data)>();
            Queue<PackFolderInfo> folders = new Queue<PackFolderInfo>();
            folders.Enqueue(packFolderManager.GetRootFolder());
            while (folders.Count > 0)
            {
                PackFolderInfo folder = folders.Dequeue();
                foreach (PackFileInfo file in folder.GetFilesInfo())
                {
                    string path = ReplacePath(file.FullName)
                        .Replace('\\', '/')
                        .TrimStart('/');
                    string fileName = path.Substring(path.LastIndexOf('/') + 1);
                    if (path.Contains("/slot/", StringComparison.OrdinalIgnoreCase) &&
                        fileName.Contains("prob", StringComparison.OrdinalIgnoreCase) &&
                        GetCatalogFileFormatPriority(fileName) > 0)
                    {
                        symbolCandidates.Add((path, file.GetData()));
                    }
                    foreach (KartCatalogAbilityKind kind in Enum.GetValues<KartCatalogAbilityKind>())
                    {
                        int priority = GetCatalogFilePriority(
                            fileName,
                            GetAbilityFileStem(kind),
                            catalogRegion);
                        if (priority > 0)
                        {
                            candidates.Add((kind, priority, path, file.GetData()));
                            break;
                        }
                    }
                }

                foreach (PackFolderInfo child in folder.GetFoldersInfo())
                {
                    folders.Enqueue(child);
                }
            }

            var symbolByName = new Dictionary<string, KartCatalogItemSymbol>(StringComparer.Ordinal);
            foreach (var candidate in symbolCandidates.OrderBy(
                candidate => candidate.Path,
                StringComparer.OrdinalIgnoreCase))
            {
                XmlDocument document = LoadCatalogXmlDocument(
                    GetCatalogXml(candidate.Path, candidate.Data));
                foreach (XmlNode node in document.GetElementsByTagName("item"))
                {
                    if (node is not XmlElement item ||
                        !short.TryParse(
                            item.GetAttribute("idx"),
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out short itemId))
                    {
                        continue;
                    }

                    string name = item.GetAttribute("name");
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    AddVerifiedItemSymbol(
                        symbolByName,
                        name,
                        itemId,
                        $"item.rho:{candidate.Path}");
                }
            }

            // These three special-item ids were independently verified for the
            // Korean 5136 item enum.  They are absent from the random-box
            // probability tables, so keep the evidence explicit in the XML.
            AddVerifiedItemSymbol(symbolByName, "goldEggMine", 83, "P5136 verified supplement");
            AddVerifiedItemSymbol(symbolByName, "superMagnet", 103, "P5136 verified supplement");
            AddVerifiedItemSymbol(symbolByName, "siren", 24, "P5136 verified supplement");
            foreach ((string name, short itemId) in Korean5136ExecutableItemSymbols)
            {
                AddVerifiedItemSymbol(
                    symbolByName,
                    name,
                    itemId,
                    "P5136 verified executable supplement");
            }
            var merged = new Dictionary<string, (
                int Priority,
                KartCatalogAbilityKind Kind,
                Dictionary<string, string> Attributes)>(StringComparer.Ordinal);
            foreach (var candidate in candidates.OrderBy(candidate => candidate.Priority))
            {
                XmlDocument document = LoadCatalogXmlDocument(
                    GetCatalogXml(candidate.Path, candidate.Data));
                foreach (XmlNode node in document.GetElementsByTagName("item"))
                {
                    if (node is not XmlElement item)
                    {
                        continue;
                    }

                    var attributes = item.Attributes
                        .Cast<XmlAttribute>()
                        .ToDictionary(
                            attribute => attribute.Name,
                            attribute => attribute.Value,
                            StringComparer.Ordinal);
                    string key = GetAbilityMergeKey(candidate.Kind, attributes);
                    if (string.IsNullOrEmpty(key))
                    {
                        continue;
                    }

                    merged[key] = (candidate.Priority, candidate.Kind, attributes);
                }
            }

            string[] requiredSymbols = merged.Values
                .SelectMany(entry => new[]
                {
                    GetAbilitySourceSymbol(entry.Kind, entry.Attributes),
                    GetAbilityTargetSymbol(entry.Attributes)
                })
                .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(symbol => symbol, StringComparer.Ordinal)
                .ToArray();
            string unpackedExecutablePath = ResolveUnpackedExecutablePath(dataDirectory);
            P5136ItemSymbolScanResult executableSymbols =
                P5136ItemSymbolScanner.Scan(unpackedExecutablePath, requiredSymbols);
            foreach (var mapping in executableSymbols.Mappings)
            {
                AddVerifiedItemSymbol(
                    symbolByName,
                    mapping.Key,
                    mapping.Value,
                    "KartRiderU.exe verified item initializer");
            }
            Console.WriteLine(
                $"[RHO kart catalog] optional executable item-symbol verification: " +
                $"resolved={executableSymbols.ResolvedCount}/{executableSymbols.RequestedCount}, " +
                $"path={unpackedExecutablePath}" +
                (string.IsNullOrWhiteSpace(executableSymbols.Error)
                    ? string.Empty
                    : $", warning={executableSymbols.Error}"));

            itemSymbols = symbolByName.Values
                .OrderBy(symbol => symbol.Name, StringComparer.Ordinal)
                .ToList();

            List<KartCatalogAbilityRule> rules = merged.Values
                .Select(entry => CreateKartCatalogAbilityRule(
                    entry.Kind,
                    entry.Attributes,
                    ResolveItemSymbol(symbolByName, GetAbilitySourceSymbol(
                        entry.Kind,
                        entry.Attributes)),
                    ResolveItemSymbol(symbolByName, GetAbilityTargetSymbol(entry.Attributes))))
                .Where(rule => rule != null)
                .OrderBy(rule => rule.Kind)
                .ThenBy(rule => rule.KartId)
                .ThenBy(rule => rule.SourceSymbol, StringComparer.Ordinal)
                .ThenBy(rule => rule.Mode, StringComparer.Ordinal)
                .ThenBy(rule => rule.Step)
                .ToList();

            Console.WriteLine(
                $"[RHO kart catalog] abilities extracted: symbols={itemSymbols.Count}, " +
                $"transform={rules.Count(rule => rule.Kind == KartCatalogAbilityKind.TransformByKart)}, " +
                $"firing={rules.Count(rule => rule.Kind == KartCatalogAbilityKind.FiringToGain)}, " +
                $"fired={rules.Count(rule => rule.Kind == KartCatalogAbilityKind.FiredToGain)}, " +
                $"resolved={rules.Count(rule => rule.IsRuntimeResolved)}/{rules.Count}");
            return rules;
        }

        private static void AddVerifiedItemSymbol(
            IDictionary<string, KartCatalogItemSymbol> symbols,
            string name,
            short itemId,
            string evidence)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            if (symbols.TryGetValue(name, out KartCatalogItemSymbol current))
            {
                if (current.ItemId != itemId)
                {
                    throw new InvalidDataException(
                        $"conflicting item symbol '{name}': {current.ItemId} and {itemId}");
                }
                return;
            }

            symbols[name] = new KartCatalogItemSymbol
            {
                Name = name,
                ItemId = itemId,
                Evidence = evidence ?? string.Empty
            };
        }

        private static short? ResolveItemSymbol(
            IReadOnlyDictionary<string, KartCatalogItemSymbol> symbols,
            string name)
        {
            return !string.IsNullOrWhiteSpace(name) &&
                symbols.TryGetValue(name, out KartCatalogItemSymbol symbol)
                    ? symbol.ItemId
                    : null;
        }

        private static string GetAbilitySourceSymbol(
            KartCatalogAbilityKind kind,
            IReadOnlyDictionary<string, string> attributes)
        {
            string sourceAttribute = kind switch
            {
                KartCatalogAbilityKind.TransformByKart => "srcIdx",
                KartCatalogAbilityKind.FiringToGain => "firingItemIdx",
                _ => "firedItemIdx"
            };
            return attributes.TryGetValue(sourceAttribute, out string symbol)
                ? symbol
                : string.Empty;
        }

        private static string GetAbilityTargetSymbol(
            IReadOnlyDictionary<string, string> attributes)
        {
            if (attributes.TryGetValue("dstIdx", out string transformTarget))
            {
                return transformTarget;
            }
            return attributes.TryGetValue("gainItemIdx", out string gainTarget)
                ? gainTarget
                : string.Empty;
        }

        private static XElement CreateInventoryXml(
            IEnumerable<KartCatalogInventoryItem> items)
        {
            KartCatalogInventoryItem[] ordered = items
                .OrderBy(item => item.Category)
                .ThenBy(item => item.Id)
                .ToArray();
            return new XElement(
                "Inventory",
                new XAttribute("total", ordered.Length),
                new XAttribute(
                    "categories",
                    ordered.Select(item => item.Category).Distinct().Count()),
                ordered.Select(item =>
                {
                    XElement element = new XElement(
                        "Item",
                        new XAttribute("category", item.Category),
                        new XAttribute("id", item.Id));
                    if (item.Serial != 0)
                    {
                        element.Add(new XAttribute("serial", item.Serial));
                    }
                    if (!string.IsNullOrWhiteSpace(item.Name))
                    {
                        element.Add(new XAttribute("name", item.Name));
                    }
                    return element;
                }));
        }

        private static XElement CreateItemSymbolsXml(
            IEnumerable<KartCatalogItemSymbol> symbols)
        {
            KartCatalogItemSymbol[] ordered = symbols
                .OrderBy(symbol => symbol.Name, StringComparer.Ordinal)
                .ToArray();
            return new XElement(
                "ItemSymbols",
                new XAttribute("resolution", "verified-partial"),
                new XAttribute("total", ordered.Length),
                ordered.Select(symbol => new XElement(
                    "Item",
                    new XAttribute("name", symbol.Name),
                    new XAttribute("id", symbol.ItemId),
                    new XAttribute("evidence", symbol.Evidence))));
        }

        private static XElement CreateAbilitiesXml(IEnumerable<KartCatalogAbilityRule> rules)
        {
            KartCatalogAbilityRule[] ordered = rules
                .OrderBy(rule => rule.Kind)
                .ThenBy(rule => rule.KartId)
                .ThenBy(rule => rule.SourceSymbol, StringComparer.Ordinal)
                .ThenBy(rule => rule.Mode, StringComparer.Ordinal)
                .ThenBy(rule => rule.Step)
                .ToArray();
            XElement abilities = new XElement(
                "Abilities",
                new XAttribute("numericResolution", "verified-partial"),
                new XAttribute("total", ordered.Length),
                new XAttribute("resolved", ordered.Count(rule => rule.IsRuntimeResolved)));
            foreach (KartCatalogAbilityKind kind in Enum.GetValues<KartCatalogAbilityKind>())
            {
                XElement group = new XElement(kind.ToString());
                foreach (KartCatalogAbilityRule rule in ordered.Where(rule => rule.Kind == kind))
                {
                    XElement element = new XElement("Rule");
                    foreach (var attribute in rule.RawAttributes.OrderBy(
                        attribute => attribute.Key,
                        StringComparer.Ordinal))
                    {
                        element.Add(new XAttribute(attribute.Key, attribute.Value));
                    }
                    if (rule.SourceItemId.HasValue)
                    {
                        element.Add(new XAttribute("sourceId", rule.SourceItemId.Value));
                    }
                    if (rule.TargetItemId.HasValue)
                    {
                        element.Add(new XAttribute("targetId", rule.TargetItemId.Value));
                    }
                    group.Add(element);
                }
                abilities.Add(group);
            }
            return abilities;
        }

        private static List<KartCatalogInventoryItem> ParseInventoryXml(
            XElement catalogRoot)
        {
            XElement inventory = catalogRoot.Element("Inventory")
                ?? throw new InvalidDataException("kart catalog XML has no Inventory element");
            var items = new List<KartCatalogInventoryItem>();
            var keys = new HashSet<(ushort Category, ushort Id)>();
            foreach (XElement element in inventory.Elements("Item"))
            {
                if (!ushort.TryParse(
                        element.Attribute("category")?.Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out ushort category) ||
                    !ushort.TryParse(
                        element.Attribute("id")?.Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out ushort id) ||
                    id == 0)
                {
                    throw new InvalidDataException(
                        "kart catalog XML has an invalid inventory item");
                }

                ushort serial = 0;
                string serialText = element.Attribute("serial")?.Value;
                if (!string.IsNullOrWhiteSpace(serialText) &&
                    !ushort.TryParse(
                        serialText,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out serial))
                {
                    throw new InvalidDataException(
                        $"kart catalog XML has an invalid inventory serial for {category}:{id}");
                }
                if (!keys.Add((category, id)))
                {
                    throw new InvalidDataException(
                        $"kart catalog XML has duplicate inventory item {category}:{id}");
                }

                items.Add(new KartCatalogInventoryItem
                {
                    Category = category,
                    Id = id,
                    Serial = serial,
                    Name = element.Attribute("name")?.Value ?? string.Empty
                });
            }

            if (!int.TryParse(
                    inventory.Attribute("total")?.Value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int declaredTotal) ||
                declaredTotal != items.Count)
            {
                throw new InvalidDataException(
                    $"kart catalog inventory count mismatch ({declaredTotal} declared, {items.Count} read)");
            }
            int categoryCount = items.Select(item => item.Category).Distinct().Count();
            if (!int.TryParse(
                    inventory.Attribute("categories")?.Value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int declaredCategories) ||
                declaredCategories != categoryCount)
            {
                throw new InvalidDataException(
                    $"kart catalog inventory category count mismatch " +
                    $"({declaredCategories} declared, {categoryCount} read)");
            }
            return items;
        }

        private static Dictionary<string, KartCatalogItemSymbol> ParseItemSymbolsXml(
            XElement catalogRoot)
        {
            XElement itemSymbols = catalogRoot.Element("ItemSymbols")
                ?? throw new InvalidDataException("kart catalog XML has no ItemSymbols element");
            var symbols = new Dictionary<string, KartCatalogItemSymbol>(StringComparer.Ordinal);
            foreach (XElement element in itemSymbols.Elements("Item"))
            {
                string name = element.Attribute("name")?.Value;
                short? itemId = TryReadOptionalShort(element.Attribute("id")?.Value);
                if (string.IsNullOrWhiteSpace(name) || !itemId.HasValue)
                {
                    throw new InvalidDataException("kart catalog XML has an invalid item symbol");
                }

                AddVerifiedItemSymbol(
                    symbols,
                    name,
                    itemId.Value,
                    element.Attribute("evidence")?.Value ?? string.Empty);
            }

            if (symbols.Count == 0)
            {
                throw new InvalidDataException("kart catalog XML has no item symbols");
            }
            return symbols;
        }

        private static List<KartCatalogAbilityRule> ParseAbilitiesXml(
            XElement catalogRoot,
            IReadOnlyDictionary<string, KartCatalogItemSymbol> itemSymbols)
        {
            XElement abilities = catalogRoot.Element("Abilities")
                ?? throw new InvalidDataException("kart catalog XML has no Abilities element");
            List<KartCatalogAbilityRule> rules = new List<KartCatalogAbilityRule>();
            foreach (XElement group in abilities.Elements())
            {
                if (!Enum.TryParse(
                        group.Name.LocalName,
                        ignoreCase: false,
                        out KartCatalogAbilityKind kind))
                {
                    continue;
                }

                foreach (XElement element in group.Elements("Rule"))
                {
                    var rawAttributes = element.Attributes()
                        .Where(attribute =>
                            attribute.Name.LocalName != "sourceId" &&
                            attribute.Name.LocalName != "targetId")
                        .ToDictionary(
                            attribute => attribute.Name.LocalName,
                            attribute => attribute.Value,
                            StringComparer.Ordinal);
                    short? sourceItemId =
                        TryReadOptionalShort(element.Attribute("sourceId")?.Value) ??
                        ResolveItemSymbol(
                            itemSymbols,
                            GetAbilitySourceSymbol(kind, rawAttributes));
                    short? targetItemId =
                        TryReadOptionalShort(element.Attribute("targetId")?.Value) ??
                        ResolveItemSymbol(itemSymbols, GetAbilityTargetSymbol(rawAttributes));
                    KartCatalogAbilityRule rule = CreateKartCatalogAbilityRule(
                        kind,
                        rawAttributes,
                        sourceItemId,
                        targetItemId);
                    if (rule != null)
                    {
                        rules.Add(rule);
                    }
                }
            }
            return rules;
        }

        private static KartCatalogAbilityRule CreateKartCatalogAbilityRule(
            KartCatalogAbilityKind kind,
            IReadOnlyDictionary<string, string> attributes,
            short? sourceItemId,
            short? targetItemId)
        {
            if (!attributes.TryGetValue("kartId", out string kartIdText) ||
                !ushort.TryParse(
                    kartIdText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out ushort kartId))
            {
                return null;
            }

            string sourceAttribute;
            string targetAttribute;
            string modeAttribute;
            string stepAttribute;
            switch (kind)
            {
                case KartCatalogAbilityKind.TransformByKart:
                    sourceAttribute = "srcIdx";
                    targetAttribute = "dstIdx";
                    modeAttribute = "gitType";
                    stepAttribute = string.Empty;
                    break;
                case KartCatalogAbilityKind.FiringToGain:
                    sourceAttribute = "firingItemIdx";
                    targetAttribute = "gainItemIdx";
                    modeAttribute = "gameType";
                    stepAttribute = "firingStep";
                    break;
                default:
                    sourceAttribute = "firedItemIdx";
                    targetAttribute = "gainItemIdx";
                    modeAttribute = "gameType";
                    stepAttribute = string.Empty;
                    break;
            }

            attributes.TryGetValue(sourceAttribute, out string sourceSymbol);
            attributes.TryGetValue(targetAttribute, out string targetSymbol);
            if (string.IsNullOrWhiteSpace(sourceSymbol) || string.IsNullOrWhiteSpace(targetSymbol))
            {
                return null;
            }

            attributes.TryGetValue("probability", out string probabilityText);
            byte? probability = byte.TryParse(
                    probabilityText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out byte parsedProbability) &&
                parsedProbability <= 100
                    ? parsedProbability
                    : null;
            attributes.TryGetValue(modeAttribute, out string mode);
            int step = !string.IsNullOrEmpty(stepAttribute) &&
                attributes.TryGetValue(stepAttribute, out string stepText) &&
                int.TryParse(
                    stepText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int parsedStep)
                    ? parsedStep
                    : 0;

            return new KartCatalogAbilityRule
            {
                Kind = kind,
                KartId = kartId,
                SourceSymbol = sourceSymbol,
                SourceItemId = sourceItemId,
                TargetSymbol = targetSymbol,
                TargetItemId = targetItemId,
                ProbabilityText = probabilityText ?? string.Empty,
                Probability = probability,
                Mode = mode ?? string.Empty,
                Step = step,
                RawAttributes = new SortedDictionary<string, string>(
                    attributes.ToDictionary(
                        entry => entry.Key,
                        entry => entry.Value,
                        StringComparer.Ordinal),
                    StringComparer.Ordinal)
            };
        }

        private static string GetAbilityMergeKey(
            KartCatalogAbilityKind kind,
            IReadOnlyDictionary<string, string> attributes)
        {
            attributes.TryGetValue("kartId", out string kartId);
            string sourceAttribute = kind switch
            {
                KartCatalogAbilityKind.TransformByKart => "srcIdx",
                KartCatalogAbilityKind.FiringToGain => "firingItemIdx",
                _ => "firedItemIdx"
            };
            attributes.TryGetValue(sourceAttribute, out string source);
            string modeAttribute = kind == KartCatalogAbilityKind.TransformByKart
                ? "gitType"
                : "gameType";
            attributes.TryGetValue(modeAttribute, out string mode);
            string step = string.Empty;
            if (kind == KartCatalogAbilityKind.FiringToGain)
            {
                attributes.TryGetValue("firingStep", out step);
            }

            return string.IsNullOrWhiteSpace(kartId) || string.IsNullOrWhiteSpace(source)
                ? string.Empty
                : $"{kind}|{kartId}|{source}|{mode}|{step}";
        }

        private static string GetAbilityFileStem(KartCatalogAbilityKind kind)
        {
            return kind switch
            {
                KartCatalogAbilityKind.TransformByKart => "transformByKart",
                KartCatalogAbilityKind.FiringToGain => "firing2Gain",
                _ => "fired2Gain"
            };
        }

        private static short? TryReadOptionalShort(string value)
        {
            return short.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out short parsed)
                    ? parsed
                    : null;
        }

        private static string ResolveUnpackedExecutablePath(string dataDirectory)
        {
            string fullDataDirectory = Path.GetFullPath(dataDirectory);
            string parentDirectory = Path.GetDirectoryName(fullDataDirectory)
                ?? fullDataDirectory;
            return new[]
                {
                    Path.Combine(parentDirectory, "KartRiderU.exe"),
                    Path.Combine(fullDataDirectory, "KartRiderU.exe")
                }
                .FirstOrDefault(File.Exists) ??
                Path.Combine(parentDirectory, "KartRiderU.exe");
        }

        private static string ComputeFileSha256(string path)
        {
            using FileStream stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }

        private static void ValidateCatalogSourceFingerprints(
            XElement root,
            string catalogPath)
        {
            string aaaHash = ValidateSha256Attribute(root, "sourceAaaSha256", required: true);
            string executableHash = ValidateSha256Attribute(
                root,
                "sourceExecutableSha256",
                required: false);
            string catalogDirectory = Path.GetDirectoryName(catalogPath) ?? string.Empty;
            if (!Path.GetFileName(catalogDirectory).Equals(
                    "Profile",
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string gameRoot = Path.GetDirectoryName(catalogDirectory) ?? string.Empty;
            string aaaPath = Path.Combine(gameRoot, "Data", "aaa.pk");
            if (File.Exists(aaaPath) &&
                !ComputeFileSha256(aaaPath).Equals(aaaHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "kart catalog aaa.pk fingerprint does not match this client");
            }

            string unpackedExecutablePath = Path.Combine(gameRoot, "KartRiderU.exe");
            if (!string.IsNullOrEmpty(executableHash) &&
                File.Exists(unpackedExecutablePath) &&
                !ComputeFileSha256(unpackedExecutablePath).Equals(
                    executableHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "kart catalog KartRiderU.exe fingerprint does not match this client");
            }
        }

        private static string ValidateSha256Attribute(
            XElement root,
            string attributeName,
            bool required)
        {
            string value = root.Attribute(attributeName)?.Value ?? string.Empty;
            if (string.IsNullOrEmpty(value) && !required)
            {
                return string.Empty;
            }
            if (value.Length != 64 || value.Any(character =>
                    !char.IsAsciiHexDigit(character)))
            {
                throw new InvalidDataException(
                    $"kart catalog has an invalid {attributeName} fingerprint");
            }
            return value;
        }

        private static void ValidateKartCatalogContents(
            string catalogRegion,
            IReadOnlyDictionary<int, string> names,
            IReadOnlyDictionary<string, XmlDocument> specs,
            IReadOnlyCollection<KartCatalogInventoryItem> inventory,
            IEnumerable<KartCatalogItemSymbol> itemSymbols,
            IReadOnlyCollection<KartCatalogAbilityRule> abilities)
        {
            if (!catalogRegion.Equals(KartCatalogRegion, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"kart catalog region must be {KartCatalogRegion}, not {catalogRegion}");
            }
            if (names.Count < MinimumKartCatalogNames || specs.Count < MinimumKartCatalogSpecs)
            {
                throw new InvalidDataException(
                    $"incomplete P5136 kart catalog (names={names.Count}, specs={specs.Count})");
            }
            if (!names.TryGetValue(1450, out string redLotusName) ||
                !redLotusName.Equals("shurikenV1", StringComparison.Ordinal) ||
                !names.TryGetValue(1453, out string goldenChickenName) ||
                !goldenChickenName.Equals("chicken_goldV1", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "kart catalog is missing the P5136 kart identity sentinels 1450/1453");
            }

            ValidateCatalogKartSlots(specs, redLotusName, 1450);
            ValidateCatalogKartSlots(specs, goldenChickenName, 1453);

            KartCatalogInventoryItem[] inventoryItems = inventory?.ToArray()
                ?? Array.Empty<KartCatalogInventoryItem>();
            int inventoryCategories = inventoryItems
                .Select(item => item.Category)
                .Distinct()
                .Count();
            int inventoryKarts = inventoryItems.Count(item => item.Category == 3);
            KartCatalogInventoryItem[] grantItems = inventoryItems
                .Where(KartCatalogInventory.IsGrantItem)
                .ToArray();
            int grantCategories = grantItems
                .Select(item => item.Category)
                .Distinct()
                .Count();
            int duplicateInventoryKeys = inventoryItems
                .GroupBy(item => (item.Category, item.Id))
                .Count(group => group.Count() > 1);
            if (inventoryItems.Length < MinimumKartCatalogInventoryItems ||
                inventoryCategories < MinimumKartCatalogInventoryCategories ||
                inventoryKarts < MinimumKartCatalogInventoryKarts ||
                grantItems.Length < MinimumKartCatalogGrantItems ||
                grantCategories < MinimumKartCatalogGrantCategories ||
                duplicateInventoryKeys != 0 ||
                inventoryItems.Any(item => item.Id == 0) ||
                inventoryItems.Any(item =>
                    item.Category == 3 && !names.ContainsKey(item.Id)) ||
                !inventoryItems.Any(item => item.Category == 3 && item.Id == 1450) ||
                !inventoryItems.Any(item => item.Category == 3 && item.Id == 1453))
            {
                throw new InvalidDataException(
                    $"incomplete P5136 inventory catalog (items={inventoryItems.Length}, " +
                    $"categories={inventoryCategories}, karts={inventoryKarts}, " +
                    $"grant={grantItems.Length}/{grantCategories} categories, " +
                    $"duplicates={duplicateInventoryKeys})");
            }

            KartCatalogItemSymbol[] symbols = itemSymbols?.ToArray()
                ?? Array.Empty<KartCatalogItemSymbol>();
            var symbolsByName = symbols.ToDictionary(
                symbol => symbol.Name,
                symbol => symbol.ItemId,
                StringComparer.Ordinal);
            if (symbols.Length < 36 ||
                !symbolsByName.TryGetValue("goldEggMine", out short goldEggMine) ||
                goldEggMine != 83 ||
                !symbolsByName.TryGetValue("superMagnet", out short superMagnet) ||
                superMagnet != 103 ||
                !symbolsByName.TryGetValue("siren", out short siren) ||
                siren != 24)
            {
                throw new InvalidDataException(
                    $"incomplete P5136 item symbol catalog (symbols={symbols.Length})");
            }

            int resolvedAbilities = abilities.Count(rule => rule.IsRuntimeResolved);
            if (abilities.Count < MinimumKartCatalogAbilities ||
                resolvedAbilities < MinimumResolvedKartCatalogAbilities ||
                !HasCatalogAbility(
                    abilities,
                    KartCatalogAbilityKind.TransformByKart,
                    1453,
                    8,
                    83,
                    100) ||
                !HasCatalogAbility(
                    abilities,
                    KartCatalogAbilityKind.TransformByKart,
                    1453,
                    5,
                    103,
                    100) ||
                !HasCatalogAbility(
                    abilities,
                    KartCatalogAbilityKind.FiringToGain,
                    1450,
                    5,
                    24,
                    100) ||
                !HasCatalogAbility(
                    abilities,
                    KartCatalogAbilityKind.FiringToGain,
                    1450,
                    7,
                    5,
                    33))
            {
                throw new InvalidDataException(
                    $"incomplete P5136 kart ability catalog " +
                    $"(abilities={abilities.Count}, resolved={resolvedAbilities})");
            }
        }

        private static void ValidateCatalogKartSlots(
            IReadOnlyDictionary<string, XmlDocument> specs,
            string kartName,
            int kartId)
        {
            if (!specs.TryGetValue(kartName, out XmlDocument spec) ||
                spec.GetElementsByTagName("BodyParam").Count == 0 ||
                spec.GetElementsByTagName("BodyParam")[0] is not XmlElement body ||
                body.GetAttribute("ItemSlotCapacity") != "3" ||
                body.GetAttribute("SpecialSlotCapacity") != "2")
            {
                throw new InvalidDataException(
                    $"kart catalog has an invalid P5136 slot spec for kart {kartId} ({kartName})");
            }
        }

        private static bool HasCatalogAbility(
            IEnumerable<KartCatalogAbilityRule> abilities,
            KartCatalogAbilityKind kind,
            ushort kartId,
            short sourceItemId,
            short targetItemId,
            byte probability)
        {
            return abilities.Any(rule =>
                rule.Kind == kind &&
                rule.KartId == kartId &&
                rule.SourceItemId == sourceItemId &&
                rule.TargetItemId == targetItemId &&
                rule.Probability == probability);
        }

        private static string ResolveAaaPkPath(string gameRootOrAaaPath)
        {
            if (string.IsNullOrWhiteSpace(gameRootOrAaaPath))
            {
                throw new ArgumentException("game root or aaa.pk path is required");
            }

            string fullPath = Path.GetFullPath(gameRootOrAaaPath);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }

            if (Directory.Exists(fullPath))
            {
                string underData = Path.Combine(fullPath, "Data", "aaa.pk");
                if (File.Exists(underData))
                {
                    return underData;
                }

                string directlyUnderRoot = Path.Combine(fullPath, "aaa.pk");
                if (File.Exists(directlyUnderRoot))
                {
                    return directlyUnderRoot;
                }
            }

            throw new FileNotFoundException("aaa.pk was not found", fullPath);
        }

        private static bool TryGetKartParamCandidate(
            string path,
            string catalogRegion,
            out string kartName,
            out int priority)
        {
            kartName = string.Empty;
            priority = 0;
            string prefix;
            if (path.StartsWith("kart_/", StringComparison.OrdinalIgnoreCase))
            {
                prefix = "kart_/";
            }
            else if (path.StartsWith("kart/", StringComparison.OrdinalIgnoreCase))
            {
                prefix = "kart/";
            }
            else
            {
                return false;
            }

            string relative = path.Substring(prefix.Length);
            int separator = relative.IndexOf('/');
            if (separator <= 0 || relative.IndexOf('/', separator + 1) >= 0)
            {
                return false;
            }

            kartName = relative.Substring(0, separator);
            string fileName = relative.Substring(separator + 1);
            priority = GetCatalogFilePriority(fileName, "param", catalogRegion);
            return priority > 0;
        }

        private static int GetCatalogFilePriority(
            string fileName,
            string stem,
            string catalogRegion)
        {
            int formatPriority = GetCatalogFileFormatPriority(fileName);
            if (formatPriority == 0)
            {
                return 0;
            }

            string nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            if (nameWithoutExtension.Equals(stem, StringComparison.OrdinalIgnoreCase))
            {
                return formatPriority;
            }

            string localeName = $"{stem}@{catalogRegion}";
            return nameWithoutExtension.Equals(localeName, StringComparison.OrdinalIgnoreCase)
                ? 100 + formatPriority
                : 0;
        }

        private static int GetCatalogFileFormatPriority(string fileName)
        {
            return Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".bml" => 1,
                ".kml" => 2,
                ".xml" => 3,
                _ => 0
            };
        }

        private static byte[] GetCatalogXml(string path, byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                throw new InvalidDataException($"empty RHO entry: {path}");
            }
            return Path.GetExtension(path).Equals(".bml", StringComparison.OrdinalIgnoreCase)
                ? BmlToXml(path, data)
                : ReplaceBytes(data);
        }

        private static XmlDocument LoadCatalogXmlDocument(byte[] data)
        {
            try
            {
                using MemoryStream stream = new MemoryStream(data, writable: false);
                XmlDocument document = new XmlDocument();
                document.Load(stream);
                return document;
            }
            catch (XmlException)
            {
                // A few Korean RHO5 overlays contain whitespace or a second XML
                // declaration before the actual document.  Normalize only in
                // memory; the packaged bytes remain untouched.
                Encoding encoding;
                if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
                {
                    encoding = Encoding.Unicode;
                }
                else if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF)
                {
                    encoding = Encoding.BigEndianUnicode;
                }
                else if (data.Length >= 2 && data[1] == 0)
                {
                    encoding = Encoding.Unicode;
                }
                else
                {
                    encoding = Encoding.UTF8;
                }

                string xml = encoding.GetString(data)
                    .TrimStart('\uFEFF', '\0', ' ', '\t', '\r', '\n');
                int declarationStart;
                while ((declarationStart = xml.IndexOf(
                    "<?xml",
                    StringComparison.OrdinalIgnoreCase)) >= 0)
                {
                    int declarationEnd = xml.IndexOf("?>", declarationStart, StringComparison.Ordinal);
                    if (declarationEnd < 0)
                    {
                        break;
                    }
                    xml = xml.Remove(declarationStart, declarationEnd + 2 - declarationStart);
                }

                XmlDocument document = new XmlDocument();
                document.LoadXml(xml.TrimStart('\uFEFF', '\0', ' ', '\t', '\r', '\n'));
                return document;
            }
        }

        private static byte[] ReplaceBytes(byte[] data)
        {
            byte[] oldBytes = new byte[] {
            0x3C, 0x00, 0x3F, 0x00, 0x78, 0x00, 0x6D, 0x00, 0x6C, 0x00, 0x20, 0x00,
            0x76, 0x00, 0x65, 0x00, 0x72, 0x00, 0x73, 0x00, 0x69, 0x00, 0x6F, 0x00, 0x6E, 0x00,
            0x3D, 0x00, 0x27, 0x00, 0x31, 0x00, 0x2E, 0x00, 0x30, 0x00, 0x27, 0x00, 0x20, 0x00,
            0x65, 0x00, 0x6E, 0x00, 0x63, 0x00, 0x6F, 0x00, 0x64, 0x00, 0x69, 0x00, 0x6E, 0x00,
            0x67, 0x00, 0x3D, 0x00, 0x27, 0x00, 0x55, 0x00, 0x54, 0x00, 0x46, 0x00, 0x2D, 0x00,
            0x31, 0x00, 0x36, 0x00, 0x27, 0x00, 0x3F, 0x00, 0x3E, 0x00, 0x0D, 0x00, 0x0A, 0x00,
            0x3C, 0x00, 0x3F, 0x00, 0x78, 0x00, 0x6D, 0x00, 0x6C, 0x00, 0x20, 0x00,
            0x76, 0x00, 0x65, 0x00, 0x72, 0x00, 0x73, 0x00, 0x69, 0x00, 0x6F, 0x00, 0x6E, 0x00,
            0x3D, 0x00, 0x27, 0x00, 0x31, 0x00, 0x2E, 0x00, 0x30, 0x00, 0x27, 0x00, 0x20, 0x00,
            0x65, 0x00, 0x6E, 0x00, 0x63, 0x00, 0x6F, 0x00, 0x64, 0x00, 0x69, 0x00, 0x6E, 0x00,
            0x67, 0x00, 0x3D, 0x00, 0x27, 0x00, 0x55, 0x00, 0x54, 0x00, 0x46, 0x00, 0x2D, 0x00,
            0x31, 0x00, 0x36, 0x00, 0x27, 0x00, 0x3F, 0x00, 0x3E, 0x00, 0x0D, 0x00, 0x0A, 0x00
        };

            byte[] newBytes = new byte[] {
            0x3C, 0x00, 0x3F, 0x00, 0x78, 0x00, 0x6D, 0x00, 0x6C, 0x00, 0x20, 0x00,
            0x76, 0x00, 0x65, 0x00, 0x72, 0x00, 0x73, 0x00, 0x69, 0x00, 0x6F, 0x00, 0x6E, 0x00,
            0x3D, 0x00, 0x27, 0x00, 0x31, 0x00, 0x2E, 0x00, 0x30, 0x00, 0x27, 0x00, 0x20, 0x00,
            0x65, 0x00, 0x6E, 0x00, 0x63, 0x00, 0x6F, 0x00, 0x64, 0x00, 0x69, 0x00, 0x6E, 0x00,
            0x67, 0x00, 0x3D, 0x00, 0x27, 0x00, 0x55, 0x00, 0x54, 0x00, 0x46, 0x00, 0x2D, 0x00,
            0x31, 0x00, 0x36, 0x00, 0x27, 0x00, 0x3F, 0x00, 0x3E, 0x00, 0x0D, 0x00, 0x0A, 0x00
        };
            int oldLength = oldBytes.Length;
            int newLength = newBytes.Length;
            int dataLength = data.Length;

            byte[] result = new byte[dataLength];
            int resultIndex = 0;
            int i = 0;

            while (i < dataLength)
            {
                bool found = true;
                for (int j = 0; j < oldLength; j++)
                {
                    if (i + j >= dataLength || data[i + j] != oldBytes[j])
                    {
                        found = false;
                        break;
                    }
                }

                if (found)
                {
                    for (int k = 0; k < newLength; k++)
                    {
                        result[resultIndex++] = newBytes[k];
                    }
                    i += oldLength;
                }
                else
                {
                    result[resultIndex++] = data[i++];
                }
            }

            Array.Resize(ref result, resultIndex);
            return result;
        }

        private static string ReplacePath(string file)
        {
            return file.IndexOf(".rho") > -1 ? file.Substring(0, file.IndexOf(".rho")).Replace("_", "/") + file.Substring(file.IndexOf(".rho") + 4) : file;
        }

        private static bool StreamsAreEqual(Stream stream1, Stream stream2)
        {
            const int bufferSize = 4096;
            byte[] buffer1 = new byte[bufferSize];
            byte[] buffer2 = new byte[bufferSize];

            try
            {
                int bytesRead1;
                int bytesRead2;

                do
                {
                    bytesRead1 = stream1.Read(buffer1, 0, bufferSize);
                    bytesRead2 = stream2.Read(buffer2, 0, bufferSize);

                    if (bytesRead1 != bytesRead2)
                        return false;

                    for (int i = 0; i < bytesRead1; i++)
                    {
                        if (buffer1[i] != buffer2[i])
                            return false;
                    }
                } while (bytesRead1 > 0);

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void ProcessTrackLocale(XmlDocument trackLocale)
        {
            if (trackLocale == null) return;

            var localeNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (XmlNode xn in trackLocale.GetElementsByTagName("track"))
            {
                XmlElement xe = xn as XmlElement;
                if (xe == null) continue;
                string blocked = xe.GetAttribute("blocked");
                if (blocked.Equals("true", StringComparison.OrdinalIgnoreCase)) continue;
                string id = xe.GetAttribute("id");
                if (string.IsNullOrWhiteSpace(id) || id.Contains("_S", StringComparison.OrdinalIgnoreCase)) continue;
                string name = xe.GetAttribute("name");
                bool basicAi = xe.GetAttribute("basicAi").Equals("true", StringComparison.OrdinalIgnoreCase);
                AddOrUpdateTrackListEntry(id, name, basicAi);
                localeNames[id] = name;
            }

            void processVariant(string elementName, string suffix)
            {
                foreach (XmlNode xn in trackLocale.GetElementsByTagName(elementName))
                {
                    XmlElement xe = xn as XmlElement;
                    if (xe == null) continue;
                    string blocked = xe.GetAttribute("blocked");
                    if (blocked.Equals("true", StringComparison.OrdinalIgnoreCase)) continue;
                    string refId = xe.GetAttribute("refId");
                    if (string.IsNullOrWhiteSpace(refId) || refId.Contains("_S", StringComparison.OrdinalIgnoreCase)) continue;
                    string variantId = $"{refId}_{suffix}";
                    bool basicAi = xe.GetAttribute("basicAi").Equals("true", StringComparison.OrdinalIgnoreCase);

                    string name;
                    if (elementName == "track_rvs")
                    {
                        if (localeNames.TryGetValue(refId, out var baseName) && !string.IsNullOrWhiteSpace(baseName))
                        {
                            name = $"[反]{baseName}";
                        }
                        else
                        {
                            name = string.Empty;
                        }
                    }
                    else
                    {
                        name = xe.GetAttribute("name");
                        if (string.IsNullOrWhiteSpace(name) && localeNames.TryGetValue(refId, out var baseName))
                        {
                            name = baseName;
                        }
                        else if (string.IsNullOrWhiteSpace(name))
                        {
                            name = string.Empty;
                        }
                    }

                    AddOrUpdateTrackListEntry(variantId, name, basicAi);
                }
            }

            processVariant("track_crz", "crz");
            processVariant("track_rvs", "rvs");
        }

        private static string ResolveVariantGameType(string trackIdentifier)
        {
            uint adler32Id = Adler32Helper.GenerateAdler32_UNICODE(trackIdentifier, 0);
            if (string.IsNullOrWhiteSpace(trackIdentifier)) return string.Empty;
            int suffixIndex = trackIdentifier.LastIndexOf('_');
            if (suffixIndex <= 0) return string.Empty;
            string baseId = trackIdentifier.Substring(0, suffixIndex);
            if (RandomTrack.TrackList.TryGetValue(adler32Id, out var baseTrack))
            {
                return baseTrack.gameType ?? string.Empty;
            }
            return string.Empty;
        }

        private static void AddOrUpdateTrackListEntry(string trackIdentifier, string name, bool basicAi = false)
        {
            uint adler32Id = Adler32Helper.GenerateAdler32_UNICODE(trackIdentifier, 0);
            string realName = string.IsNullOrWhiteSpace(name) ? string.Empty : name;
            if (RandomTrack.TrackList.ContainsKey(adler32Id))
            {
                Track existingTrack = RandomTrack.TrackList[adler32Id];
                if (!string.IsNullOrWhiteSpace(realName))
                {
                    existingTrack.Name = realName;
                }
                existingTrack.basicAi = basicAi;
                if (string.IsNullOrWhiteSpace(existingTrack.gameType))
                {
                    existingTrack.gameType = ResolveVariantGameType(trackIdentifier);
                }
            }
            else
            {
                RandomTrack.TrackList.Add(adler32Id, new Track
                {
                    hash = adler32Id,
                    ID = trackIdentifier,
                    Name = realName,
                    gameType = ResolveVariantGameType(trackIdentifier),
                    basicAi = basicAi
                });
            }
        }

        private static void ProcessTrackList(XmlDocument trackLocale)
        {
            if (trackLocale == null) return;

            var baseGameTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (XmlNode xn in trackLocale.GetElementsByTagName("track"))
            {
                XmlElement xe = xn as XmlElement;
                if (xe == null) continue;
                string id = xe.GetAttribute("id");
                if (string.IsNullOrWhiteSpace(id) || id.Contains("_S", StringComparison.OrdinalIgnoreCase)) continue;
                string gameType = xe.GetAttribute("gameType");
                string name = xe.GetAttribute("name");
                AddRandomTrackEntry(id, gameType, name);
                if (!baseGameTypes.ContainsKey(id))
                {
                    baseGameTypes.Add(id, gameType);
                }
            }

            void processVariant(string elementName, string suffix)
            {
                foreach (XmlNode xn in trackLocale.GetElementsByTagName(elementName))
                {
                    XmlElement xe = xn as XmlElement;
                    if (xe == null) continue;
                    string refId = xe.GetAttribute("refId");
                    if (string.IsNullOrWhiteSpace(refId) || refId.Contains("_S", StringComparison.OrdinalIgnoreCase)) continue;
                    string variantId = $"{refId}_{suffix}";
                    string gameType = baseGameTypes.TryGetValue(refId, out var gt) ? gt : string.Empty;
                    string name = xe.GetAttribute("name");
                    AddRandomTrackEntry(variantId, gameType, name);
                }
            }

            processVariant("track_crz", "crz");
            processVariant("track_rvs", "rvs");
        }

        private static void AddRandomTrackEntry(string trackIdentifier, string gameType, string name)
        {
            uint adler32Id = Adler32Helper.GenerateAdler32_UNICODE(trackIdentifier, 0);
            string realName = string.IsNullOrWhiteSpace(name) ? string.Empty : name;
            if (!RandomTrack.TrackList.ContainsKey(adler32Id))
            {
                if (string.IsNullOrWhiteSpace(gameType))
                {
                    gameType = ResolveVariantGameType(trackIdentifier);
                }
                RandomTrack.TrackList.Add(adler32Id, new Track
                {
                    hash = adler32Id,
                    ID = trackIdentifier,
                    Name = realName,
                    gameType = gameType
                });
            }
            else
            {
                Track existingTrack = RandomTrack.TrackList[adler32Id];
                if (!string.IsNullOrWhiteSpace(gameType))
                {
                    existingTrack.gameType = gameType;
                }
                else if (string.IsNullOrWhiteSpace(existingTrack.gameType))
                {
                    existingTrack.gameType = ResolveVariantGameType(trackIdentifier);
                }
                if (!string.IsNullOrWhiteSpace(realName))
                {
                    existingTrack.Name = realName;
                }
            }
        }

        private static byte[] BmlToXml(string path, byte[] bmlData)
        {
            if (Path.GetExtension(path).ToLower() == ".bml")
            {
                BinaryXmlDocument bxd = new BinaryXmlDocument();
                bxd.Read(Encoding.GetEncoding("UTF-16"), bmlData);
                string output_bml = bxd.RootTag.ToString();
                byte[] output_data = Encoding.GetEncoding("UTF-16").GetBytes(output_bml);
                return output_data;
            }
            else
            {
                return bmlData;
            }
        }

        private static List<int> CountChildren(XElement element, int level, List<int> childCounts)
        {
            var childCount = element.Elements().Count();
            childCounts.Add(childCount);
            foreach (var child in element.Elements()) CountChildren(child, level + 1, childCounts);
            return childCounts;
        }

        public static BinaryXmlTag GetAAATag(string input)
        {
            // Read the current aaa.pk content
            byte[] aaaPkData;
            try
            {
                using (FileStream fileStream = new FileStream(input, FileMode.Open))
                {
                    BinaryReader br = new BinaryReader(fileStream);
                    int dataLen = br.ReadInt32();
                    aaaPkData = br.ReadKRData(dataLen);
                    fileStream.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading aaa.pk: {ex.Message}");
                // If reading fails, return null
                return null;
            }

            BinaryXmlDocument bxmlDoc = new BinaryXmlDocument();
            bxmlDoc.Read(Encoding.GetEncoding("UTF-16"), aaaPkData);
            BinaryXmlTag rootTag = bxmlDoc.RootTag;
            return rootTag;
        }

        public static string GetRegionCode(string input)
        {
            // Extract regionCode from the XML
            string regionCode = "cn"; // Default to CN

            BinaryXmlTag rootTag = GetAAATag(input);
            if (rootTag == null)
            {
                return regionCode;
            }

            try
            {
                // Try to find the region code by looking at zeta folders
                BinaryXmlTag zetaFolder = null;
                foreach (BinaryXmlTag subtag in rootTag.Children)
                {
                    if (subtag.Name == "PackFolder" && subtag.GetAttribute("name") == "zeta")
                    {
                        zetaFolder = subtag;
                        break;
                    }
                }

                if (zetaFolder != null)
                {
                    foreach (BinaryXmlTag subtag in zetaFolder.Children)
                    {
                        if (subtag.Name == "PackFolder")
                        {
                            string folderName = subtag.GetAttribute("name");
                            if (folderName == "kr")
                            {
                                regionCode = "kr";
                                break;
                            }
                            else if (folderName == "cn")
                            {
                                regionCode = "cn";
                                break;
                            }
                            else if (folderName == "tw")
                            {
                                regionCode = "tw";
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting regionCode: {ex.Message}");
            }
            return regionCode;
        }
    }
}
