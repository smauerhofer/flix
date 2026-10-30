using System.Windows.Forms;

namespace Flix.EvaluationInspector;

internal static class Program
{
  [STAThread]
  private static void Main(string[] args)
  {
    ApplicationConfiguration.Initialize();
    Application.Run(new EvaluationInspectorForm(args));
  }
}
