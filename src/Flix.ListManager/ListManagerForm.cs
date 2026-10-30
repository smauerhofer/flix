using Flix.Core;
using System.ComponentModel;

namespace Flix.ListManager;

public sealed class ListManagerForm : Form
{
  private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
  private readonly DataGridView _blackGrid = MakeGrid();
  private readonly DataGridView _whiteGrid = MakeGrid();
  private readonly DataGridView _criteriaGrid = MakeGrid();
  private readonly TextBox _blackSearch = new() { Width = 250 };
  private readonly TextBox _whiteSearch = new() { Width = 250 };
  private List<BlacklistEntry> _black = [];
  private List<WhitelistGroupEntry> _white = [];
  private string? _blackPath;
  private string? _whitePath;
  private bool _dirtyBlack;
  private bool _dirtyWhite;

  public ListManagerForm()
  {
    Text = "flix — List Management";
    Width = 1160;
    Height = 780;
    MinimumSize = new Size(800, 550);
    _tabs.TabPages.Add(BuildBlacklistTab());
    _tabs.TabPages.Add(BuildWhitelistTab());
    Controls.Add(_tabs);
    _blackSearch.TextChanged += (_, _) => RefreshBlacklist();
    _whiteSearch.TextChanged += (_, _) => RefreshWhitelist();
    _blackGrid.CellValueChanged += (_, _) => _dirtyBlack = true;
    _whiteGrid.CellValueChanged += (_, _) => _dirtyWhite = true;
    _criteriaGrid.CellValueChanged += (_, _) => _dirtyWhite = true;
    _whiteGrid.SelectionChanged += (_, _) => RefreshCriteria();
    FormClosing += OnClosing;
    RefreshBlacklist();
    RefreshWhitelist();
  }

  private static DataGridView MakeGrid()
  {
    return new DataGridView
    {
      Dock = DockStyle.Fill,
      MultiSelect = true,
      SelectionMode = DataGridViewSelectionMode.FullRowSelect,
      AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
      AllowUserToAddRows = false,
      AllowUserToDeleteRows = false,
      RowHeadersVisible = false
    };
  }

  private TabPage BuildBlacklistTab()
  {
    var page = new TabPage("Blacklist");
    var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
    panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
    var toolbar = Toolbar();
    AddButton(toolbar, "Open", OpenBlacklist);
    AddButton(toolbar, "Save", SaveBlacklist);
    AddButton(toolbar, "Add", AddBlacklist);
    AddButton(toolbar, "Remove", RemoveBlacklist);
    AddButton(toolbar, "Activate", () => SetBlacklistActive(true));
    AddButton(toolbar, "Deactivate", () => SetBlacklistActive(false));
    toolbar.Controls.Add(new Label { Text = "Search", AutoSize = true, Margin = new Padding(15, 10, 2, 0) });
    toolbar.Controls.Add(_blackSearch);
    panel.Controls.Add(toolbar, 0, 0);
    panel.Controls.Add(_blackGrid, 0, 1);
    page.Controls.Add(panel);
    return page;
  }

  private TabPage BuildWhitelistTab()
  {
    var page = new TabPage("Whitelist groups");
    var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3 };
    panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    panel.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
    panel.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
    var toolbar = Toolbar();
    AddButton(toolbar, "Open", OpenWhitelist);
    AddButton(toolbar, "Save", SaveWhitelist);
    AddButton(toolbar, "Add group", AddGroup);
    AddButton(toolbar, "Remove group", RemoveGroups);
    AddButton(toolbar, "Activate", () => SetGroupsActive(true));
    AddButton(toolbar, "Deactivate", () => SetGroupsActive(false));
    toolbar.Controls.Add(new Label { Text = "Search", AutoSize = true, Margin = new Padding(15, 10, 2, 0) });
    toolbar.Controls.Add(_whiteSearch);
    var criterionPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
    criterionPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    criterionPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
    var actions = Toolbar();
    actions.Controls.Add(new Label { Text = "Selected group's criteria", AutoSize = true, Margin = new Padding(4, 10, 12, 0) });
    AddButton(actions, "Add criterion", AddCriterion);
    AddButton(actions, "Remove criterion", RemoveCriteria);
    AddButton(actions, "Activate criterion", () => SetCriteriaActive(true));
    AddButton(actions, "Deactivate criterion", () => SetCriteriaActive(false));
    criterionPanel.Controls.Add(actions, 0, 0);
    criterionPanel.Controls.Add(_criteriaGrid, 0, 1);
    panel.Controls.Add(toolbar, 0, 0);
    panel.Controls.Add(_whiteGrid, 0, 1);
    panel.Controls.Add(criterionPanel, 0, 2);
    page.Controls.Add(panel);
    return page;
  }

  private static FlowLayoutPanel Toolbar()
  {
    return new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Padding = new Padding(4) };
  }

  private static void AddButton(FlowLayoutPanel bar, string title, Action action)
  {
    var button = new Button { Text = title, AutoSize = true };
    button.Click += (_, _) => action();
    bar.Controls.Add(button);
  }

  private static List<T> Selected<T>(DataGridView grid)
  {
    return grid.SelectedRows.Cast<DataGridViewRow>()
      .Select(x => x.DataBoundItem).OfType<T>().Distinct().ToList();
  }

  private WhitelistGroupEntry? CurrentGroup()
  {
    return Selected<WhitelistGroupEntry>(_whiteGrid).FirstOrDefault();
  }

  private void RefreshBlacklist()
  {
    string term = _blackSearch.Text.Trim();
    var filtered = _black.Where(x => term.Length == 0 ||
      (x.EntityId + " " + x.SourceId + " " + x.Type + " " + x.Value)
      .Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
    _blackGrid.DataSource = new BindingList<BlacklistEntry>(filtered);
    if (_blackGrid.Columns.Contains("Id"))
    {
      _blackGrid.Columns["Id"]!.Visible = false;
    }
  }

  private void RefreshWhitelist()
  {
    string term = _whiteSearch.Text.Trim();
    var filtered = _white.Where(x => term.Length == 0 ||
      (x.EntityId + " " + x.SourceId + " " + x.Description + " " +
        string.Join(" ", x.Criteria.Select(c => c.Type + " " + c.Value)))
      .Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
    _whiteGrid.DataSource = new BindingList<WhitelistGroupEntry>(filtered);
    if (_whiteGrid.Columns.Contains("Id"))
    {
      _whiteGrid.Columns["Id"]!.Visible = false;
    }
    if (_whiteGrid.Columns.Contains("Criteria"))
    {
      _whiteGrid.Columns["Criteria"]!.Visible = false;
    }
    RefreshCriteria();
  }

  private void RefreshCriteria()
  {
    _criteriaGrid.DataSource = new BindingList<WhitelistCriterion>(CurrentGroup()?.Criteria ?? []);
    if (_criteriaGrid.Columns.Contains("Id"))
    {
      _criteriaGrid.Columns["Id"]!.Visible = false;
    }
  }

  private static string? PickFile(bool save, string? current)
  {
    using FileDialog dialog = save ? new SaveFileDialog() : new OpenFileDialog();
    dialog.Filter = "JSON files (*.json)|*.json";
    dialog.FileName = current is null ? "" : Path.GetFileName(current);
    if (current is not null)
    {
      dialog.InitialDirectory = Path.GetDirectoryName(current);
    }
    return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : null;
  }

  private void OpenBlacklist()
  {
    string? path = PickFile(false, _blackPath);
    if (path is null)
    {
      return;
    }
    try
    {
      var entries = BlacklistFile.Read(path);
      ReferenceListValidation.ValidateBlacklist(entries);
      _black = entries;
      _blackPath = path;
      _dirtyBlack = false;
      RefreshBlacklist();
    }
    catch (Exception ex)
    {
      ShowError(ex);
    }
  }

  private void SaveBlacklist()
  {
    string? path = _blackPath ?? PickFile(true, null);
    if (path is null)
    {
      return;
    }
    try
    {
      _blackGrid.EndEdit();
      BlacklistFile.Write(path, _black);
      _blackPath = path;
      _dirtyBlack = false;
    }
    catch (Exception ex)
    {
      ShowError(ex);
    }
  }

  private void AddBlacklist()
  {
    string? entity = TextPrompt.Ask("New blacklist entry", "Entity ID");
    if (string.IsNullOrWhiteSpace(entity))
    {
      return;
    }
    string? type = TextPrompt.Ask("New blacklist entry", "Field type (NAME, IBAN, ACCOUNT, BIC, LEI, COUNTRY, ADDRESS)", "NAME");
    if (type is null)
    {
      return;
    }
    string? value = TextPrompt.Ask("New blacklist entry", "Field value");
    if (string.IsNullOrWhiteSpace(value))
    {
      return;
    }
    _black.Add(new BlacklistEntry { EntityId = entity, Type = type, Value = value });
    _dirtyBlack = true;
    RefreshBlacklist();
  }

  private void RemoveBlacklist()
  {
    foreach (BlacklistEntry entry in Selected<BlacklistEntry>(_blackGrid))
    {
      _black.Remove(entry);
      _dirtyBlack = true;
    }
    RefreshBlacklist();
  }

  private void SetBlacklistActive(bool value)
  {
    foreach (BlacklistEntry entry in Selected<BlacklistEntry>(_blackGrid))
    {
      entry.Active = value;
      _dirtyBlack = true;
    }
    RefreshBlacklist();
  }

  private void OpenWhitelist()
  {
    string? path = PickFile(false, _whitePath);
    if (path is null)
    {
      return;
    }
    try
    {
      var groups = WhitelistFile.Read(path);
      ReferenceListValidation.ValidateWhitelist(groups);
      _white = groups;
      _whitePath = path;
      _dirtyWhite = false;
      RefreshWhitelist();
    }
    catch (Exception ex)
    {
      ShowError(ex);
    }
  }

  private void SaveWhitelist()
  {
    string? path = _whitePath ?? PickFile(true, null);
    if (path is null)
    {
      return;
    }
    try
    {
      _whiteGrid.EndEdit();
      _criteriaGrid.EndEdit();
      WhitelistFile.Write(path, _white);
      _whitePath = path;
      _dirtyWhite = false;
    }
    catch (Exception ex)
    {
      ShowError(ex);
    }
  }

  private void AddGroup()
  {
    string? id = TextPrompt.Ask("New whitelist group", "Entity ID");
    if (string.IsNullOrWhiteSpace(id))
    {
      return;
    }
    string? description = TextPrompt.Ask("New whitelist group", "Description", "Approved party");
    if (description is null)
    {
      return;
    }
    _white.Add(new WhitelistGroupEntry { EntityId = id, Description = description });
    _dirtyWhite = true;
    RefreshWhitelist();
  }

  private void RemoveGroups()
  {
    foreach (WhitelistGroupEntry group in Selected<WhitelistGroupEntry>(_whiteGrid))
    {
      _white.Remove(group);
      _dirtyWhite = true;
    }
    RefreshWhitelist();
  }

  private void SetGroupsActive(bool active)
  {
    foreach (WhitelistGroupEntry group in Selected<WhitelistGroupEntry>(_whiteGrid))
    {
      group.Active = active;
      _dirtyWhite = true;
    }
    RefreshWhitelist();
  }

  private void AddCriterion()
  {
    WhitelistGroupEntry? group = CurrentGroup();
    if (group is null)
    {
      MessageBox.Show("Select one whitelist group first.");
      return;
    }
    string? type = TextPrompt.Ask("New criterion", "Field type", "NAME");
    if (type is null)
    {
      return;
    }
    string? value = TextPrompt.Ask("New criterion", "Value");
    if (string.IsNullOrWhiteSpace(value))
    {
      return;
    }
    group.Criteria.Add(new WhitelistCriterion { Type = type, Value = value });
    _dirtyWhite = true;
    RefreshCriteria();
  }

  private void RemoveCriteria()
  {
    WhitelistGroupEntry? group = CurrentGroup();
    if (group is null)
    {
      return;
    }
    foreach (WhitelistCriterion criterion in Selected<WhitelistCriterion>(_criteriaGrid))
    {
      group.Criteria.Remove(criterion);
      _dirtyWhite = true;
    }
    RefreshCriteria();
  }

  private void SetCriteriaActive(bool active)
  {
    foreach (WhitelistCriterion criterion in Selected<WhitelistCriterion>(_criteriaGrid))
    {
      criterion.Active = active;
      _dirtyWhite = true;
    }
    RefreshCriteria();
  }

  private static void ShowError(Exception ex)
  {
    MessageBox.Show(ex.Message, "Validation / file error", MessageBoxButtons.OK, MessageBoxIcon.Error);
  }

  private void OnClosing(object? sender, FormClosingEventArgs e)
  {
    if ((_dirtyBlack || _dirtyWhite) &&
      MessageBox.Show("There are unsaved changes. Exit without saving?", "flix", MessageBoxButtons.YesNo,
        MessageBoxIcon.Warning) != DialogResult.Yes)
    {
      e.Cancel = true;
    }
  }
}
