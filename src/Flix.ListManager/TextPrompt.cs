namespace Flix.ListManager;

public static class TextPrompt
{
  public static string? Ask(string title, string label, string initial = "")
  {
    using var form = new Form { Text = title, Width = 520, Height = 170, StartPosition = FormStartPosition.CenterParent };
    var instruction = new Label { Text = label, Left = 12, Top = 12, Width = 470 };
    var input = new TextBox { Left = 12, Top = 36, Width = 470, Text = initial };
    var ok = new Button { Text = "OK", Left = 310, Top = 75, Width = 80, DialogResult = DialogResult.OK };
    var cancel = new Button { Text = "Cancel", Left = 402, Top = 75, Width = 80, DialogResult = DialogResult.Cancel };
    form.Controls.AddRange([instruction, input, ok, cancel]);
    form.AcceptButton = ok;
    form.CancelButton = cancel;
    return form.ShowDialog() == DialogResult.OK ? input.Text.Trim() : null;
  }
}
