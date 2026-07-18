using KartRider.Compatibility;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace KartRider
{
    internal sealed class RandomTrackEditorForm : Form
    {
        private readonly Korean5136RandomTrackCatalog catalog;
        private readonly Dictionary<(byte GameType, uint Selector), PoolSelection> selections;
        private readonly ComboBox poolComboBox = new ComboBox();
        private readonly CheckedListBox trackListBox = new CheckedListBox();
        private readonly Label selectionStatusLabel = new Label();
        private bool loadingPool;

        internal RandomTrackEditorForm(
            Korean5136RandomTrackCatalog catalog,
            RandomTrackConfiguration configured)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            selections = CreateSelections(configured);

            Text = "랜덤 맵 목록 설정";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(720, 560);
            ClientSize = new Size(820, 640);
            Font = new Font("맑은 고딕", 9F);

            BuildLayout();
            PopulatePools();
        }

        public RandomTrackConfiguration Result { get; private set; }

        private void BuildLayout()
        {
            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                ColumnCount = 1,
                RowCount = 5
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            root.Controls.Add(new Label
            {
                Text = "설정할 랜덤 맵 목록을 고른 뒤 포함할 맵을 선택하세요.",
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 8)
            }, 0, 0);

            TableLayoutPanel poolRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 2,
                Margin = new Padding(0, 0, 0, 6)
            };
            poolRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            poolRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            poolRow.Controls.Add(new Label
            {
                Text = "랜덤 목록",
                AutoSize = true,
                Margin = new Padding(0, 6, 10, 0)
            }, 0, 0);
            poolComboBox.Dock = DockStyle.Fill;
            poolComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            poolComboBox.SelectedIndexChanged += PoolComboBox_SelectedIndexChanged;
            poolRow.Controls.Add(poolComboBox, 1, 0);
            root.Controls.Add(poolRow, 0, 1);

            selectionStatusLabel.AutoSize = true;
            selectionStatusLabel.ForeColor = Color.DimGray;
            selectionStatusLabel.Margin = new Padding(0, 0, 0, 8);
            root.Controls.Add(selectionStatusLabel, 0, 2);

            trackListBox.Dock = DockStyle.Fill;
            trackListBox.CheckOnClick = true;
            trackListBox.IntegralHeight = false;
            trackListBox.HorizontalScrollbar = false;
            trackListBox.ItemCheck += TrackListBox_ItemCheck;
            root.Controls.Add(trackListBox, 0, 3);

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = true,
                Padding = new Padding(0, 10, 0, 0)
            };

            Button saveButton = CreateButton("저장", SaveButton_Click);
            Button cancelButton = new Button
            {
                Text = "취소",
                AutoSize = true,
                DialogResult = DialogResult.Cancel,
                Padding = new Padding(12, 4, 12, 4)
            };
            Button clientDefaultsButton = CreateButton(
                "클라이언트 기본값",
                ClientDefaultsButton_Click);
            Button selectAllButton = CreateButton("모두 선택", SelectAllButton_Click);
            Button clearAllButton = CreateButton("모두 해제", ClearAllButton_Click);

            actions.Controls.Add(saveButton);
            actions.Controls.Add(cancelButton);
            actions.Controls.Add(clientDefaultsButton);
            actions.Controls.Add(clearAllButton);
            actions.Controls.Add(selectAllButton);
            root.Controls.Add(actions, 0, 4);

            AcceptButton = saveButton;
            CancelButton = cancelButton;
            Controls.Add(root);
        }

        private static Button CreateButton(string text, EventHandler handler)
        {
            Button button = new Button
            {
                Text = text,
                AutoSize = true,
                Padding = new Padding(10, 4, 10, 4)
            };
            button.Click += handler;
            return button;
        }

        private Dictionary<(byte GameType, uint Selector), PoolSelection> CreateSelections(
            RandomTrackConfiguration configured)
        {
            RandomTrackConfiguration snapshot = configured?.Clone()
                ?? new RandomTrackConfiguration();
            var configuredPools = (snapshot.Pools ?? new List<RandomTrackPoolOverride>())
                .Where(pool => pool != null)
                .GroupBy(pool => (pool.GameType, pool.Selector))
                .ToDictionary(group => group.Key, group => group.Last());
            var result = new Dictionary<(byte GameType, uint Selector), PoolSelection>();

            foreach (Korean5136RandomTrackPool pool in catalog.Pools)
            {
                HashSet<string> compatibleIds = catalog.GetCompatibleTracks(pool)
                    .Select(track => track.Id)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                bool hasOverride = configuredPools.TryGetValue(
                    (pool.GameType, pool.Selector),
                    out RandomTrackPoolOverride configuredPool);
                IEnumerable<string> selectedIds = hasOverride
                    ? configuredPool.TrackIds ?? new List<string>()
                    : pool.DefaultTrackIds;

                result[(pool.GameType, pool.Selector)] = new PoolSelection(
                    useClientDefaults: !hasOverride,
                    selectedIds.Where(compatibleIds.Contains));
            }

            return result;
        }

        private void PopulatePools()
        {
            loadingPool = true;
            try
            {
                poolComboBox.Items.Clear();
                foreach (Korean5136RandomTrackPool pool in catalog.Pools)
                {
                    poolComboBox.Items.Add(new PoolChoice(pool));
                }
            }
            finally
            {
                loadingPool = false;
            }

            if (poolComboBox.Items.Count > 0)
            {
                poolComboBox.SelectedIndex = 0;
            }
            else
            {
                selectionStatusLabel.Text = "클라이언트에서 사용할 수 있는 랜덤 목록을 찾지 못했습니다.";
            }
        }

        private void PoolComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!loadingPool)
            {
                LoadSelectedPool();
            }
        }

        private void LoadSelectedPool()
        {
            if (poolComboBox.SelectedItem is not PoolChoice choice ||
                !selections.TryGetValue(choice.Key, out PoolSelection selection))
            {
                return;
            }

            loadingPool = true;
            try
            {
                trackListBox.Items.Clear();
                foreach (Korean5136RandomTrackDefinition track in
                         catalog.GetCompatibleTracks(choice.Pool))
                {
                    TrackChoice trackChoice = new TrackChoice(track);
                    int index = trackListBox.Items.Add(trackChoice);
                    trackListBox.SetItemChecked(
                        index,
                        selection.SelectedTrackIds.Contains(track.Id));
                }
            }
            finally
            {
                loadingPool = false;
            }

            UpdateSelectionStatus(selection);
        }

        private void TrackListBox_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (loadingPool ||
                poolComboBox.SelectedItem is not PoolChoice poolChoice ||
                trackListBox.Items[e.Index] is not TrackChoice trackChoice ||
                !selections.TryGetValue(poolChoice.Key, out PoolSelection selection))
            {
                return;
            }

            selection.UseClientDefaults = false;
            if (e.NewValue == CheckState.Checked)
            {
                selection.SelectedTrackIds.Add(trackChoice.Track.Id);
            }
            else
            {
                selection.SelectedTrackIds.Remove(trackChoice.Track.Id);
            }

            BeginInvoke(new Action(() => UpdateSelectionStatus(selection)));
        }

        private void SelectAllButton_Click(object sender, EventArgs e)
        {
            SetAllChecks(true);
        }

        private void ClearAllButton_Click(object sender, EventArgs e)
        {
            SetAllChecks(false);
        }

        private void SetAllChecks(bool isChecked)
        {
            if (poolComboBox.SelectedItem is not PoolChoice choice ||
                !selections.TryGetValue(choice.Key, out PoolSelection selection))
            {
                return;
            }

            selection.UseClientDefaults = false;
            selection.SelectedTrackIds.Clear();
            loadingPool = true;
            try
            {
                for (int index = 0; index < trackListBox.Items.Count; index++)
                {
                    if (trackListBox.Items[index] is TrackChoice trackChoice && isChecked)
                    {
                        selection.SelectedTrackIds.Add(trackChoice.Track.Id);
                    }
                    trackListBox.SetItemChecked(index, isChecked);
                }
            }
            finally
            {
                loadingPool = false;
            }
            UpdateSelectionStatus(selection);
        }

        private void ClientDefaultsButton_Click(object sender, EventArgs e)
        {
            if (poolComboBox.SelectedItem is not PoolChoice choice ||
                !selections.TryGetValue(choice.Key, out PoolSelection selection))
            {
                return;
            }

            selection.UseClientDefaults = true;
            selection.SelectedTrackIds.Clear();
            foreach (string id in choice.Pool.DefaultTrackIds)
            {
                selection.SelectedTrackIds.Add(id);
            }
            LoadSelectedPool();
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            foreach (PoolChoice choice in poolComboBox.Items.Cast<PoolChoice>())
            {
                PoolSelection selection = selections[choice.Key];
                if (!selection.UseClientDefaults && selection.SelectedTrackIds.Count == 0)
                {
                    MessageBox.Show(
                        this,
                        $"'{choice.Pool.KoreanName}' 목록에서 맵을 1개 이상 선택하세요.",
                        "랜덤 맵 목록 확인",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    poolComboBox.SelectedItem = choice;
                    return;
                }
            }

            var result = new RandomTrackConfiguration
            {
                Pools = poolComboBox.Items
                    .Cast<PoolChoice>()
                    .Select(choice => (Choice: choice, Selection: selections[choice.Key]))
                    .Where(item => !item.Selection.UseClientDefaults)
                    .Select(item => new RandomTrackPoolOverride
                    {
                        GameType = item.Choice.Pool.GameType,
                        Selector = item.Choice.Pool.Selector,
                        TrackIds = catalog.GetCompatibleTracks(item.Choice.Pool)
                            .Where(track => item.Selection.SelectedTrackIds.Contains(track.Id))
                            .Select(track => track.Id)
                            .ToList()
                    })
                    .ToList()
            };
            result.Validate();
            Result = result.Clone();
            DialogResult = DialogResult.OK;
            Close();
        }

        private void UpdateSelectionStatus(PoolSelection selection)
        {
            selectionStatusLabel.Text = selection.UseClientDefaults
                ? $"클라이언트 기본값 사용 중 · {selection.SelectedTrackIds.Count}개 선택"
                : $"사용자 지정 목록 · {selection.SelectedTrackIds.Count}개 선택";
        }

        private sealed class PoolSelection
        {
            public PoolSelection(bool useClientDefaults, IEnumerable<string> selectedIds)
            {
                UseClientDefaults = useClientDefaults;
                SelectedTrackIds = new HashSet<string>(
                    selectedIds ?? Enumerable.Empty<string>(),
                    StringComparer.OrdinalIgnoreCase);
            }

            public bool UseClientDefaults { get; set; }

            public HashSet<string> SelectedTrackIds { get; }
        }

        private sealed class PoolChoice
        {
            public PoolChoice(Korean5136RandomTrackPool pool)
            {
                Pool = pool;
            }

            public Korean5136RandomTrackPool Pool { get; }

            public (byte GameType, uint Selector) Key => (Pool.GameType, Pool.Selector);

            public override string ToString() => Pool.KoreanName;
        }

        private sealed class TrackChoice
        {
            public TrackChoice(Korean5136RandomTrackDefinition track)
            {
                Track = track;
            }

            public Korean5136RandomTrackDefinition Track { get; }

            public override string ToString() => Track.KoreanName;
        }
    }
}
