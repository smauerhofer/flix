using Flix.Core;
using System.Drawing;
using System.Windows.Forms;

namespace Flix.HitReviewer;

public sealed class HitReviewForm : Form
{
  private readonly TextBox _input = new() { Width = 510 };
  private readonly TextBox _folder = new() { Width = 510 };
  private readonly TextBox _whitelist = new() { Width = 510 };
  private readonly TextBox _actor = new() { Width = 200 };
  private readonly TextBox _details = new() { Multiline = true, ReadOnly = true,
    ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill, Font = new Font("Consolas", 10) };
  private readonly TextBox _comments = new() { Multiline = true, ScrollBars = ScrollBars.Vertical,
    Dock = DockStyle.Fill };
  private readonly CheckBox _addWhitelist = new() { Text = "Propose inactive whitelist group on Release", AutoSize = true };
  private readonly Label _progress = new() { AutoSize = true };
  private readonly List<HitCase> _queue = [];
  private ReviewCaseProcessor? _processor;
  private int _position;

  public HitReviewForm()
  {
    Text = "Flix — Hit Investigation";
    Width = 1100;
    Height = 850;
    MinimumSize = new Size(800, 600);
    var main = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
    main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    main.RowStyles.Add(new RowStyle(SizeType.Percent, 66));
    main.RowStyles.Add(new RowStyle(SizeType.Percent, 34));
    main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    Controls.Add(main);
    var settings = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true,
      WrapContents = true, Padding = new Padding(8) };
    settings.Controls.Add(new Label { Text = "Hit/investigate JSONL:", AutoSize = true });
    settings.Controls.Add(_input);
    settings.Controls.Add(MakeButton("Open...", ChooseInput));
    settings.Controls.Add(new Label { Text = "Output folder:", AutoSize = true });
    settings.Controls.Add(_folder);
    settings.Controls.Add(MakeButton("Folder...", ChooseFolder));
    settings.Controls.Add(new Label { Text = "Whitelist JSON:", AutoSize = true });
    settings.Controls.Add(_whitelist);
    settings.Controls.Add(MakeButton("Whitelist...", ChooseWhitelist));
    settings.Controls.Add(new Label { Text = "Reviewer:", AutoSize = true });
    settings.Controls.Add(_actor);
    settings.Controls.Add(MakeButton("Load queue", LoadQueue));
    settings.Controls.Add(_progress);
    main.Controls.Add(settings, 0, 0);
    main.Controls.Add(_details, 0, 1);
    var commentPane = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
    commentPane.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    commentPane.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
    commentPane.Controls.Add(new Label { Text = "New comment (previous comments are preserved in the case):",
      AutoSize = true }, 0, 0);
    commentPane.Controls.Add(_comments, 0, 1);
    main.Controls.Add(commentPane, 0, 2);
    var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(8) };
    actions.Controls.Add(_addWhitelist);
    actions.Controls.Add(MakeButton("Release", (_, _) => Process("RELEASE")));
    actions.Controls.Add(MakeButton("Reject", (_, _) => Process("REJECT")));
    actions.Controls.Add(MakeButton("Investigate", (_, _) => Process("INVESTIGATE")));
    main.Controls.Add(actions, 0, 3);
    _folder.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FlixReviews");
    _whitelist.Text = Path.Combine(_folder.Text, "whitelist.json");
    RefreshDisplay();
  }

  private static Button MakeButton(string label, EventHandler handler)
  {
    var button = new Button { Text = label, AutoSize = true };
    button.Click += handler;
    return button;
  }

  private void ChooseInput(object? sender, EventArgs e)
  {
    using var dialog = new OpenFileDialog { Filter = "JSONL files (*.jsonl)|*.jsonl|All files (*.*)|*.*" };
    if (dialog.ShowDialog(this) == DialogResult.OK)
    {
      _input.Text = dialog.FileName;
    }
  }

  private void ChooseWhitelist(object? sender, EventArgs e)
  {
    using var dialog = new SaveFileDialog { Filter = "JSON files (*.json)|*.json", FileName = "whitelist.json" };
    if (dialog.ShowDialog(this) == DialogResult.OK)
    {
      _whitelist.Text = dialog.FileName;
    }
  }

  private void ChooseFolder(object? sender, EventArgs e)
  {
    using var dialog = new FolderBrowserDialog();
    if (dialog.ShowDialog(this) == DialogResult.OK)
    {
      _folder.Text = dialog.SelectedPath;
    }
  }

  private void LoadQueue(object? sender, EventArgs e)
  {
    try
    {
      string folder = Path.GetFullPath(_folder.Text);
      var paths = new ReviewFilePaths
      {
        Release = Path.Combine(folder, "release.jsonl"),
        Reject = Path.Combine(folder, "reject.jsonl"),
        Investigate = Path.Combine(folder, "investigate.jsonl"),
        Processed = Path.Combine(folder,
          "processed-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(_input.Text))))[..16] + ".jsonl"),
        Whitelist = Path.GetFullPath(_whitelist.Text)
      };
      _processor = new ReviewCaseProcessor(paths);
      _queue.Clear();
      _queue.AddRange(HitCaseFile.Read(Path.GetFullPath(_input.Text))
        .Where(hit => !_processor.IsProcessed(hit.CaseId)));
      _position = 0;
      RefreshDisplay();
    }
    catch (Exception ex)
    {
      MessageBox.Show(this, ex.Message, "Unable to load queue", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
  }

  private void Process(string action)
  {
    if (_processor is null || _position >= _queue.Count)
    {
      return;
    }
    try
    {
      HitCase hit = _queue[_position];
      string actor = _actor.Text.Trim();
      bool add = action == "RELEASE" && _addWhitelist.Checked;
      if (add)
      {
        DialogResult confirmation = MessageBox.Show(this,
          "Create an INACTIVE whitelist proposal? It must be reviewed and activated separately.",
          "Whitelist proposal", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirmation != DialogResult.Yes)
        {
          return;
        }
      }
      _processor.Apply(hit, action, actor, _comments.Text, add);
      _position++;
      _comments.Clear();
      _addWhitelist.Checked = false;
      RefreshDisplay();
    }
    catch (Exception ex)
    {
      MessageBox.Show(this, ex.Message, "Case not processed", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
  }

  private void RefreshDisplay()
  {
    _progress.Text = $"Case {Math.Min(_position + 1, _queue.Count)} / {_queue.Count}";
    if (_position >= _queue.Count)
    {
      _details.Text = "No pending cases. Load a hit or investigate file to begin.";
      return;
    }
    HitCase hit = _queue[_position];
    var lines = new List<string>
    {
      $"Case: {hit.CaseId}",
      $"Message: {hit.Message?.MessageId}",
      $"Created UTC: {hit.CreatedAtUtc:O}",
      $"Decision: {hit.Decision?.Decision}",
      $"Snapshot: {hit.Decision?.SnapshotVersion}",
      $"Reason: {hit.Decision?.Reason}", "", "SIDES"
    };
    foreach (var side in hit.Decision?.Sides ?? [])
    {
      lines.Add($"  {side.Side}: {side.Decision}  normal={side.NormalHit} black={side.BlacklistHit} white={side.WhitelistHit}");
    }
    lines.Add("");
    lines.Add("ORIGINAL STRUCTURED MESSAGE");
    foreach (var field in hit.Message?.Fields ?? [])
    {
      lines.Add($"  {field.PartyId} / {field.Type}: {field.Value}");
    }
    lines.Add("");
    lines.Add("MATCHING CRITERIA");
    foreach (var m in hit.Decision?.NameMatches ?? [])
    {
      lines.Add("  NAME: " + m);
    }
    foreach (var m in hit.Decision?.FieldMatches ?? [])
    {
      lines.Add($"  {m.PartyId} / {m.FieldType}: {m.ReferenceValue} [{m.EntityId}] score={m.Score:F3}");
    }
    foreach (var m in hit.Decision?.BlacklistMatches ?? [])
    {
      lines.Add($"  BLACKLIST {m.PartyId} / {m.FieldType}: {m.ReferenceValue} [{m.EntityId}]");
    }
    foreach (var m in hit.Decision?.WhitelistMatches ?? [])
    {
      lines.Add($"  WHITELIST {m.PartyId} / {m.FieldType}: {m.ReferenceValue} [{m.EntityId}]");
    }
    foreach (var finding in hit.Decision?.ValidationFindings ?? [])
    {
      lines.Add($"  DATA QUALITY {finding.PartyId}: {finding.Code}: {finding.Message}");
    }
    lines.Add("");
    lines.Add("PREVIOUS COMMENTS");
    foreach (var c in hit.Comments ?? [])
    {
      lines.Add($"  {c.AtUtc:u} {c.Actor}: {c.Text}");
    }
    _details.Lines = lines.ToArray();
  }
}
