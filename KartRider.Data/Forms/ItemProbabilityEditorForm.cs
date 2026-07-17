using KartRider.Compatibility;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace KartRider
{
    internal sealed class ItemProbabilityEditorForm : Form
    {
        private static readonly IReadOnlyDictionary<string, string> KoreanItemNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["banana"] = "바나나",
                ["cloud2"] = "구름",
                ["shield"] = "실드",
                ["emp"] = "전자파",
                ["devil"] = "악마",
                ["guideRocket"] = "유도 미사일",
                ["ufo"] = "UFO",
                ["barricade"] = "바리케이드",
                ["rocket"] = "미사일",
                ["waterBomb"] = "물폭탄",
                ["waterFly"] = "물파리",
                ["thunderbolt"] = "번개",
                ["booster"] = "부스터",
                ["magnet"] = "자석",
                ["scanning"] = "스캔",
                ["slotLock"] = "슬롯 잠금",
                ["angel"] = "천사",
                ["timeBomb"] = "시한폭탄"
            };

        private readonly ItemProbabilityConfiguration defaults;
        private readonly DataGridView individualGrid = CreateGrid();
        private readonly DataGridView teamGrid = CreateGrid();
        private readonly ComboBox rankBandComboBox = new ComboBox();
        private readonly Label sourceLabel = new Label();

        public ItemProbabilityConfiguration Result { get; private set; }

        public ItemProbabilityEditorForm(
            ItemProbabilityConfiguration configured,
            ItemProbabilityConfiguration clientDefaults,
            string defaultSource)
        {
            defaults = clientDefaults.Clone();
            ItemProbabilityConfiguration initial = configured != null &&
                configured.Individual.Count > 0 &&
                configured.Team.Count > 0
                ? configured.Clone()
                : defaults.Clone();
            initial.RankBand = configured?.RankBand ?? ItemProbabilityRankBand.Live;

            Text = "아이템전 확률 설정";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(760, 560);
            ClientSize = new Size(900, 650);
            Font = new Font("맑은 고딕", 9F);

            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                ColumnCount = 1,
                RowCount = 4
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            FlowLayoutPanel rankRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            rankRow.Controls.Add(new Label
            {
                Text = "적용할 순위 가중치",
                AutoSize = true,
                Margin = new Padding(0, 7, 8, 0)
            });
            rankBandComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            rankBandComboBox.Width = 230;
            rankBandComboBox.Items.AddRange(new object[]
            {
                new RankBandChoice(ItemProbabilityRankBand.Live, "실시간 순위 자동 적용 (권장)"),
                new RankBandChoice(ItemProbabilityRankBand.Top, "1위"),
                new RankBandChoice(ItemProbabilityRankBand.High, "상위"),
                new RankBandChoice(ItemProbabilityRankBand.Middle, "중위"),
                new RankBandChoice(ItemProbabilityRankBand.Low, "하위"),
                new RankBandChoice(ItemProbabilityRankBand.Combined, "전체 구간 합산")
            });
            rankBandComboBox.SelectedItem = rankBandComboBox.Items
                .Cast<RankBandChoice>()
                .First(choice => choice.Value == initial.RankBand);
            rankRow.Controls.Add(rankBandComboBox);
            root.Controls.Add(rankRow, 0, 0);

            sourceLabel.Text =
                "실시간 순위는 1위를 별도 적용하고 나머지를 상위/중위/하위로 균등 분할합니다.\r\n" +
                "가중치는 같은 열 안에서의 상대 비율이며 0은 해당 아이템을 제외합니다.\r\n" +
                $"클라이언트 기본 확률표: {defaultSource}";
            sourceLabel.AutoSize = true;
            sourceLabel.ForeColor = Color.DimGray;
            sourceLabel.Margin = new Padding(0, 6, 0, 10);
            root.Controls.Add(sourceLabel, 0, 1);

            TabControl tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(CreateTab("개인 아이템전", individualGrid));
            tabs.TabPages.Add(CreateTab("팀 아이템전", teamGrid));
            root.Controls.Add(tabs, 0, 2);

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 10, 0, 0)
            };
            Button saveButton = new Button
            {
                Text = "저장",
                AutoSize = true,
                Padding = new Padding(12, 4, 12, 4)
            };
            saveButton.Click += SaveButton_Click;
            Button cancelButton = new Button
            {
                Text = "취소",
                AutoSize = true,
                DialogResult = DialogResult.Cancel,
                Padding = new Padding(12, 4, 12, 4)
            };
            Button loadDefaultsButton = new Button
            {
                Text = "클라이언트 원본값 불러오기",
                AutoSize = true,
                Padding = new Padding(8, 4, 8, 4)
            };
            loadDefaultsButton.Click += (_, _) => FillFromConfiguration(defaults);
            Button automaticButton = new Button
            {
                Text = "원본 자동 사용",
                AutoSize = true,
                Padding = new Padding(8, 4, 8, 4)
            };
            automaticButton.Click += AutomaticButton_Click;
            actions.Controls.Add(saveButton);
            actions.Controls.Add(cancelButton);
            actions.Controls.Add(loadDefaultsButton);
            actions.Controls.Add(automaticButton);
            root.Controls.Add(actions, 0, 3);

            AcceptButton = saveButton;
            CancelButton = cancelButton;
            Controls.Add(root);
            FillFromConfiguration(initial);
        }

        private static DataGridView CreateGrid()
        {
            DataGridView grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = SystemColors.Window,
                BorderStyle = BorderStyle.FixedSingle,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect
            };
            grid.Columns.Add(CreateColumn("itemId", "ID", readOnly: true, fillWeight: 45));
            grid.Columns.Add(CreateColumn("itemName", "아이템", readOnly: true, fillWeight: 130));
            grid.Columns.Add(CreateColumn("top", "1위", readOnly: false, fillWeight: 65));
            grid.Columns.Add(CreateColumn("high", "상위", readOnly: false, fillWeight: 65));
            grid.Columns.Add(CreateColumn("middle", "중위", readOnly: false, fillWeight: 65));
            grid.Columns.Add(CreateColumn("low", "하위", readOnly: false, fillWeight: 65));
            grid.DataError += (_, eventArgs) => eventArgs.ThrowException = false;
            return grid;
        }

        private static DataGridViewTextBoxColumn CreateColumn(
            string name,
            string header,
            bool readOnly,
            float fillWeight)
        {
            return new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                ReadOnly = readOnly,
                FillWeight = fillWeight,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
        }

        private static TabPage CreateTab(string text, Control content)
        {
            TabPage page = new TabPage(text) { Padding = new Padding(8) };
            page.Controls.Add(content);
            return page;
        }

        private void FillFromConfiguration(ItemProbabilityConfiguration configuration)
        {
            FillGrid(individualGrid, configuration.Individual);
            FillGrid(teamGrid, configuration.Team);
        }

        private static void FillGrid(
            DataGridView grid,
            IEnumerable<ItemProbabilityEntry> entries)
        {
            grid.Rows.Clear();
            foreach (ItemProbabilityEntry entry in entries.OrderBy(entry => entry.ItemId))
            {
                string displayName = KoreanItemNames.TryGetValue(entry.Name ?? string.Empty, out string korean)
                    ? korean
                    : string.IsNullOrWhiteSpace(entry.Name) ? $"아이템 {entry.ItemId}" : entry.Name;
                int rowIndex = grid.Rows.Add(
                    entry.ItemId,
                    displayName,
                    entry.TopWeight,
                    entry.HighWeight,
                    entry.MiddleWeight,
                    entry.LowWeight);
                grid.Rows[rowIndex].Tag = entry.Name ?? string.Empty;
            }
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            try
            {
                ItemProbabilityConfiguration result = new ItemProbabilityConfiguration
                {
                    RankBand = SelectedRankBand(),
                    Individual = ReadGrid(individualGrid, "개인전"),
                    Team = ReadGrid(teamGrid, "팀전")
                };
                ItemProbabilityService.Validate(result, allowEmptyTables: false);
                Result = result;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception exception) when (
                exception is FormatException ||
                exception is OverflowException ||
                exception is InvalidOperationException ||
                exception is System.IO.InvalidDataException)
            {
                MessageBox.Show(
                    this,
                    exception.Message,
                    "아이템 확률 설정 오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void AutomaticButton_Click(object sender, EventArgs e)
        {
            Result = new ItemProbabilityConfiguration
            {
                RankBand = SelectedRankBand()
            };
            DialogResult = DialogResult.OK;
            Close();
        }

        private ItemProbabilityRankBand SelectedRankBand()
        {
            return rankBandComboBox.SelectedItem is RankBandChoice choice
                ? choice.Value
                : ItemProbabilityRankBand.Live;
        }

        private static List<ItemProbabilityEntry> ReadGrid(
            DataGridView grid,
            string label)
        {
            List<ItemProbabilityEntry> entries = new List<ItemProbabilityEntry>();
            foreach (DataGridViewRow row in grid.Rows)
            {
                short itemId = Convert.ToInt16(row.Cells[0].Value, CultureInfo.InvariantCulture);
                entries.Add(new ItemProbabilityEntry
                {
                    ItemId = itemId,
                    Name = row.Tag as string ?? string.Empty,
                    TopWeight = ReadWeight(row.Cells[2], label, itemId, "1위"),
                    HighWeight = ReadWeight(row.Cells[3], label, itemId, "상위"),
                    MiddleWeight = ReadWeight(row.Cells[4], label, itemId, "중위"),
                    LowWeight = ReadWeight(row.Cells[5], label, itemId, "하위")
                });
            }
            return entries;
        }

        private static int ReadWeight(
            DataGridViewCell cell,
            string label,
            short itemId,
            string rank)
        {
            string value = Convert.ToString(cell.Value, CultureInfo.InvariantCulture)?.Trim();
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed))
            {
                throw new FormatException(
                    $"{label} 아이템 {itemId}의 {rank} 가중치는 0 이상의 정수여야 합니다.");
            }
            return parsed;
        }

        private sealed class RankBandChoice
        {
            public RankBandChoice(ItemProbabilityRankBand value, string text)
            {
                Value = value;
                Text = text;
            }

            public ItemProbabilityRankBand Value { get; }

            public string Text { get; }

            public override string ToString() => Text;
        }
    }
}
