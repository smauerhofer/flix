using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace Flix.EvaluationInspector;

public sealed class EvaluationInspectorForm : Form
{
  private readonly TextBox _reportPath = new() { Width = 420 };
  private readonly TextBox _casePath = new() { Width = 420 };
  private readonly TextBox _search = new() { Width = 225, PlaceholderText = "Case ID, entity, value..." };
  private readonly ComboBox _decision = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
  private readonly ComboBox _tenant = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 125 };
  private readonly ComboBox _strategy = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
  private readonly ComboBox _diagnostic = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 225 };
  private readonly CheckBox _retrievalFailures = new() { Text = "Expected entity missing", AutoSize = true };
  private readonly CheckBox _clearOnly = new() { Text = "CLEAR only", AutoSize = true };
  private readonly Label _summary = new() { AutoSize = true };
  private readonly DataGridView _grid = new()
  {
    Dock = DockStyle.Fill, ReadOnly = true, MultiSelect = false,
    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
    AutoGenerateColumns = false, AllowUserToAddRows = false,
    AllowUserToDeleteRows = false, RowHeadersVisible = false
  };
  private readonly TextBox _details = new()
  {
    Dock = DockStyle.Fill, ReadOnly = true, Multiline = true,
    Font = new Font("Consolas", 10), WordWrap = false, ScrollBars = ScrollBars.Both
  };
  private readonly List<InspectionRow> _all = [];
  private readonly BindingList<InspectionRow> _visible = [];

  public EvaluationInspectorForm(string[] args)
  {
    Text = "FLIX — Evaluation Inspector";
    Width = 1550;
    Height = 960;
    MinimumSize = new Size(1050, 640);
    var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
    layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
    layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    Controls.Add(layout);

    var paths = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(8) };
    paths.Controls.Add(new Label { Text = "Evaluation CSV/JSON:", AutoSize = true, Margin = new Padding(3, 8, 3, 3) });
    paths.Controls.Add(_reportPath);
    paths.Controls.Add(MakeButton("Browse report", (_, _) => Browse(_reportPath, "Evaluation reports|*.csv;*.json|All files|*.*")));
    paths.Controls.Add(new Label { Text = "Cases JSONL:", AutoSize = true, Margin = new Padding(10, 8, 3, 3) });
    paths.Controls.Add(_casePath);
    paths.Controls.Add(MakeButton("Browse cases", (_, _) => Browse(_casePath, "JSONL files|*.jsonl|All files|*.*")));
    paths.Controls.Add(MakeButton("Load", (_, _) => LoadFiles()));
    layout.Controls.Add(paths, 0, 0);

    var filters = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(8) };
    filters.Controls.Add(_search);
    filters.Controls.Add(new Label { Text = "Decision", AutoSize = true, Margin = new Padding(8, 8, 3, 3) });
    filters.Controls.Add(_decision);
    filters.Controls.Add(new Label { Text = "Tenant", AutoSize = true, Margin = new Padding(8, 8, 3, 3) });
    filters.Controls.Add(_tenant);
    filters.Controls.Add(new Label { Text = "Strategy", AutoSize = true, Margin = new Padding(8, 8, 3, 3) });
    filters.Controls.Add(_strategy);
    filters.Controls.Add(new Label { Text = "Diagnostic", AutoSize = true, Margin = new Padding(8, 8, 3, 3) });
    filters.Controls.Add(_diagnostic);
    filters.Controls.Add(_retrievalFailures);
    filters.Controls.Add(_clearOnly);
    filters.Controls.Add(MakeButton("Next failure (F8)", (_, _) => NextFailure()));
    layout.Controls.Add(filters, 0, 1);

    AddColumn("CaseId", "Case", 110);
    AddColumn("Tenant", "Tenant", 100);
    AddColumn("Strategy", "Strategy", 155);
    AddColumn("Generation", "Variation", 125);
    AddColumn("Decision", "Decision", 85);
    AddColumn("ExpectedFound", "Entity found", 90);
    AddColumn("Retrieved", "Retrieved", 85);
    AddColumn("ExpectedScore", "Expected score", 105);
    AddColumn("Threshold", "Threshold", 85);
    AddColumn("Milliseconds", "Latency ms", 85);
    AddColumn("Reference", "Expected reference", 155);
    AddColumn("Diagnostic", "Diagnostic", 250);
    _grid.DataSource = _visible;
    _grid.SelectionChanged += (_, _) => ShowSelected();
    _grid.ColumnHeaderMouseClick += (_, e) => SortByColumn(e.ColumnIndex);
    var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 450 };
    split.Panel1.Controls.Add(_grid);
    split.Panel2.Controls.Add(_details);
    layout.Controls.Add(split, 0, 2);
    var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(10) };
    footer.Controls.Add(_summary);
    layout.Controls.Add(footer, 0, 3);

    _search.TextChanged += (_, _) => ApplyFilters();
    foreach (var combo in new[] { _decision, _tenant, _strategy, _diagnostic })
    {
      combo.SelectedIndexChanged += (_, _) => ApplyFilters();
    }
    _retrievalFailures.CheckedChanged += (_, _) => ApplyFilters();
    _clearOnly.CheckedChanged += (_, _) => ApplyFilters();
    KeyPreview = true;
    KeyDown += (_, e) =>
    {
      if (e.KeyCode == Keys.F8)
      {
        NextFailure();
        e.Handled = true;
      }
    };
    ResetChoices();
    if (args.Length > 0)
    {
      _reportPath.Text = args[0];
      if (args.Length > 1)
      {
        _casePath.Text = args[1];
      }
      Shown += (_, _) => LoadFiles();
    }
  }

  private static Button MakeButton(string label, EventHandler click)
  {
    var button = new Button { Text = label, AutoSize = true, Margin = new Padding(5, 1, 5, 1) };
    button.Click += click;
    return button;
  }

  private void AddColumn(string property, string header, int width)
  {
    _grid.Columns.Add(new DataGridViewTextBoxColumn
    {
      DataPropertyName = property, Name = property,
      HeaderText = header, Width = width, SortMode = DataGridViewColumnSortMode.Programmatic
    });
  }

  private void Browse(TextBox destination, string filter)
  {
    using var dialog = new OpenFileDialog { Filter = filter };
    if (dialog.ShowDialog(this) == DialogResult.OK)
    {
      destination.Text = dialog.FileName;
    }
  }

  private void ResetChoices()
  {
    SetChoices(_decision, ["All", "CLEAR", "REVIEW", "ERROR", "WHITELISTED"]);
    SetChoices(_tenant, ["All"]);
    SetChoices(_strategy, ["All"]);
    SetChoices(_diagnostic, ["All"]);
  }

  private static void SetChoices(ComboBox box, IEnumerable<string> choices)
  {
    box.Items.Clear();
    box.Items.AddRange(choices.Cast<object>().ToArray());
    box.SelectedIndex = 0;
  }

  private void LoadFiles()
  {
    try
    {
      var results = EvaluationReportLoader.LoadReport(_reportPath.Text);
      Dictionary<string, Flix.Core.PositiveEvaluationCase> cases =
        string.IsNullOrWhiteSpace(_casePath.Text)
        ? new Dictionary<string, Flix.Core.PositiveEvaluationCase>(StringComparer.Ordinal)
        : EvaluationReportLoader.LoadCases(_casePath.Text);
      _all.Clear();
      foreach (var result in results)
      {
        cases.TryGetValue(result.CaseId, out var caseInput);
        _all.Add(new InspectionRow { Result = result, Case = caseInput });
      }
      SetChoices(_tenant, ["All", .. _all.Select(x => x.Tenant).Distinct().OrderBy(x => x)]);
      SetChoices(_strategy, ["All", .. _all.Select(x => x.Strategy).Distinct().OrderBy(x => x)]);
      SetChoices(_diagnostic, ["All", .. _all.Select(x => x.Diagnostic)
        .Where(x => x.Length > 0).Distinct().OrderBy(x => x)]);
      ApplyFilters();
    }
    catch (Exception exception)
    {
      MessageBox.Show(this, exception.Message, "Cannot load evaluation", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
  }

  private void ApplyFilters()
  {
    if (_decision.SelectedItem is null || _tenant.SelectedItem is null ||
      _strategy.SelectedItem is null || _diagnostic.SelectedItem is null)
    {
      return;
    }
    string decision = _decision.Text;
    string tenant = _tenant.Text;
    string strategy = _strategy.Text;
    string diagnostic = _diagnostic.Text;
    string search = _search.Text.Trim();
    IEnumerable<InspectionRow> items = _all.Where(row =>
      (decision == "All" || row.Decision == decision) &&
      (tenant == "All" || row.Tenant == tenant) &&
      (strategy == "All" || row.Strategy == strategy) &&
      (diagnostic == "All" || row.Diagnostic == diagnostic) &&
      (!_clearOnly.Checked || row.Decision == "CLEAR") &&
      (!_retrievalFailures.Checked || !row.ExpectedFound) &&
      (search.Length == 0 || new[] { row.CaseId, row.Reference, row.Incoming,
        row.Diagnostic, row.Tenant, row.Strategy }.Any(x => x.Contains(search, StringComparison.OrdinalIgnoreCase))));
    _visible.RaiseListChangedEvents = false;
    _visible.Clear();
    foreach (var row in items)
    {
      _visible.Add(row);
    }
    _visible.RaiseListChangedEvents = true;
    _visible.ResetBindings();
    _summary.Text = $"Showing {_visible.Count:N0} / {_all.Count:N0} cases | " +
      $"CLEAR: {_visible.Count(x => x.Decision == "CLEAR"):N0} | " +
      $"Missing expected entity: {_visible.Count(x => !x.ExpectedFound):N0} | " +
      $"P95 of displayed: {Percentile(_visible.Select(x => x.Milliseconds), 0.95):F2} ms";
    if (_visible.Count > 0)
    {
      _grid.ClearSelection();
      _grid.Rows[0].Selected = true;
      _grid.CurrentCell = _grid.Rows[0].Cells[0];
    }
    else
    {
      _details.Text = "No cases match the selected filters.";
    }
    ShowSelected();
  }

  private static double Percentile(IEnumerable<double> values, double p)
  {
    double[] ordered = values.OrderBy(x => x).ToArray();
    if (ordered.Length == 0)
    {
      return 0;
    }
    return ordered[(int)Math.Ceiling(p * ordered.Length) - 1];
  }

  private void SortByColumn(int columnIndex)
  {
    var column = _grid.Columns[columnIndex];
    var selected = (_grid.CurrentRow?.DataBoundItem as InspectionRow)?.CaseId;
    List<InspectionRow> ordered = column.DataPropertyName switch
    {
      "Milliseconds" => _visible.OrderByDescending(x => x.Milliseconds).ToList(),
      "ExpectedScore" => _visible.OrderBy(x => x.ExpectedScore ?? -1).ToList(),
      "Threshold" => _visible.OrderBy(x => x.Threshold ?? -1).ToList(),
      "Decision" => _visible.OrderBy(x => x.Decision).ToList(),
      "Strategy" => _visible.OrderBy(x => x.Strategy).ToList(),
      "Diagnostic" => _visible.OrderBy(x => x.Diagnostic).ToList(),
      _ => _visible.OrderBy(x => x.CaseId).ToList()
    };
    _visible.RaiseListChangedEvents = false;
    _visible.Clear();
    foreach (var item in ordered)
    {
      _visible.Add(item);
    }
    _visible.RaiseListChangedEvents = true;
    _visible.ResetBindings();
    int index = ordered.FindIndex(x => x.CaseId == selected);
    if (index >= 0)
    {
      _grid.CurrentCell = _grid.Rows[index].Cells[0];
    }
  }

  private void NextFailure()
  {
    if (_visible.Count == 0)
    {
      return;
    }
    int start = _grid.CurrentRow?.Index ?? -1;
    for (int offset = 1; offset <= _visible.Count; offset++)
    {
      int index = (start + offset) % _visible.Count;
      var item = _visible[index];
      if (item.Decision is "CLEAR" or "ERROR" || !item.ExpectedFound)
      {
        _grid.ClearSelection();
        _grid.Rows[index].Selected = true;
        _grid.CurrentCell = _grid.Rows[index].Cells[0];
        _grid.FirstDisplayedScrollingRowIndex = index;
        ShowSelected();
        return;
      }
    }
    MessageBox.Show(this, "No failing cases in the current selection.");
  }

  private void ShowSelected()
  {
    if (_grid.CurrentRow?.DataBoundItem is not InspectionRow row)
    {
      return;
    }
    var r = row.Result;
    var c = row.Case;
    var text = new StringBuilder();
    text.AppendLine($"CASE                 {r.CaseId}");
    text.AppendLine($"TENANT / SNAPSHOT    {r.TenantId} / {r.SnapshotVersion}");
    text.AppendLine($"REFERENCE LIST       {r.List}");
    text.AppendLine($"STRATEGY             {r.Strategy}");
    text.AppendLine($"GENERATION           {r.GenerationType}");
    text.AppendLine($"EXPECTED REFERENCE   {r.ExpectedSourceId} / {r.ExpectedEntityId}");
    text.AppendLine($"INPUT VALUE          {c?.Field?.Value ?? "[Load the originating cases JSONL to view]"}");
    text.AppendLine($"INPUT FIELD          {c?.Field?.Type ?? r.Strategy}");
    text.AppendLine($"INPUT PARTY          {c?.Field?.PartyId ?? "[unavailable]"}");
    text.AppendLine($"MESSAGE ID           {c?.MessageId ?? "[unavailable]"}");
    text.AppendLine($"ORIGINAL VALUE       [Not stored in evaluation output; requires generator provenance] ");
    text.AppendLine();
    text.AppendLine($"DECISION             {r.Decision}");
    text.AppendLine($"REVIEW               {r.ReviewTriggered}");
    text.AppendLine($"EXPECTED ENTITY HIT  {r.ExpectedEntityFound}");
    text.AppendLine($"CANDIDATE RETRIEVED  {r.CandidateRetrieved?.ToString() ?? "[unavailable]"}");
    text.AppendLine($"BEST TRADITIONAL     {r.BestTraditionalScore?.ToString("F6") ?? "[unavailable]"}");
    text.AppendLine($"EXPECTED SCORE       {r.ExpectedEntityScore?.ToString("F6") ?? "[unavailable]"}");
    text.AppendLine($"REVIEW THRESHOLD     {r.ReviewThreshold?.ToString("F6") ?? "[unavailable]"}");
    text.AppendLine($"WHITELIST OVERRIDE   {r.WhitelistOverrideApplied?.ToString() ?? "[unavailable]"}");
    text.AppendLine($"COUNTRY VALUE HIT    {r.CountryValueDetected?.ToString() ?? "[not applicable]"}");
    text.AppendLine($"COUNTRY REF IN INDEX {r.CountryEntityPresentInIndex?.ToString() ?? "[not applicable]"}");
    text.AppendLine($"LATENCY              {r.ElapsedMilliseconds:F3} ms");
    text.AppendLine($"DIAGNOSTIC           {r.Diagnostic ?? string.Empty}");
    text.AppendLine($"ERROR                {r.Error ?? string.Empty}");
    text.AppendLine();
    text.AppendLine("Candidate lists, per-algorithm scores, and original pre-mutation values");
    text.AppendLine("are not in the existing evaluation output. No values are inferred.");
    _details.Text = text.ToString();
  }
}
