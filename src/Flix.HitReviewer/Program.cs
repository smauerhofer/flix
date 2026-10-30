using System.Windows.Forms;

namespace Flix.HitReviewer;

internal class Program
{
  [STAThread]
  private static void Main()
  {
    ApplicationConfiguration.Initialize();
    Application.Run(new HitReviewForm());
  }
}
