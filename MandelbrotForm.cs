using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace MandelbrotDemo
{
  // Windows Form that displays a rendered Mandelbrot bitmap.
  // Shows the compute time in the title bar and allows saving the image as PNG via Ctrl+S.
  public class MandelbrotForm : Form
  {
    private readonly Bitmap _bitmap;
    private readonly double _totalMs; // Compute time in milliseconds, displayed in the title bar

    // Initializes the form with the pre-rendered bitmap and its associated compute time.
    public MandelbrotForm(Bitmap bitmap, double totalMs)
    {
      _bitmap = bitmap;
      _totalMs = totalMs;

      Text = $"Mandelbrot Set | Compute time: {_totalMs:F2} ms | Ctrl+S = save PNG";
      ClientSize = new Size(MandelbrotConfig.WidthPx, MandelbrotConfig.HeightPx);
      DoubleBuffered = true; // Renders to an off-screen buffer first to prevent flickering
      KeyPreview = true;     // Ensures the form receives key events before its child controls

      Paint += MandelbrotForm_Paint;
      KeyDown += MandelbrotForm_KeyDown;
    }

    // Draws the Mandelbrot bitmap onto the form's client area on every repaint.
    private void MandelbrotForm_Paint(object? sender, PaintEventArgs e)
    {
      e.Graphics.DrawImage(_bitmap, 0, 0);
    }

    // Handles Ctrl+S: saves the displayed bitmap as a PNG in the mandelbrot-images/ folder.
    // The filename encodes the resolution and compute time for easy identification.
    // If a file with the same name already exists it is skipped — no silent overwrite.
    private void MandelbrotForm_KeyDown(object? sender, KeyEventArgs e)
    {
      if (e.Control && e.KeyCode == Keys.S)
      {
        string subFolder1 = MandelbrotConfig.Parallel ? "parallel" : "sequential";
        string subFolder2 = MandelbrotConfig.WidthPx >= 3840 ? "3840x2160" :
                            MandelbrotConfig.WidthPx >= 2560 ? "2560x1440" :
                            MandelbrotConfig.WidthPx >= 1920 ? "1920x1080" : "800x600";
        string subFolder3 = $"threads-{MandelbrotConfig.Threads}";
        string dir = Path.Combine(Directory.GetCurrentDirectory(), "mandelbrot-images", subFolder1, subFolder2);
        string parallelDir = Path.Combine(dir, subFolder3);

        Directory.CreateDirectory(dir); // Ensure the directory exists
        Directory.CreateDirectory(parallelDir); // Ensure the parallel directory exists

        string filename = $"mandelbrot-{MandelbrotConfig.WidthPx}x{MandelbrotConfig.HeightPx}-{(MandelbrotConfig.Parallel ? $"parallel-threads-{MandelbrotConfig.Threads}" : "sequential")}-iterations-{MandelbrotConfig.MaxIterations}-total-{_totalMs:F2}ms.png";

        // Construct the full path for the image file based on whether it's parallel or sequential
        string path = MandelbrotConfig.Parallel 
          ? Path.Combine(parallelDir, filename) // Save in the parallel subfolder (e.g. parallel/3840x2160/threads-4/) for parallel runs
          : Path.Combine(dir, filename); // Save in the main resolution folder for sequential runs (e.g. sequential/3840x2160/)

        // Skip if a file with this exact name already exists (same resolution + compute time)
        if (!File.Exists(path))
        {
          _bitmap.Save(path, ImageFormat.Png);
          MessageBox.Show($"Saved to {path}", "Saved");
          return;
        }

        MessageBox.Show($"Already exists: {path}", "Skipped");
      }
    }
  }
}
