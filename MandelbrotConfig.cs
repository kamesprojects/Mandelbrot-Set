using System.Security.Cryptography.X509Certificates;

namespace MandelbrotDemo
{
  internal static class MandelbrotConfig
  {
    // Configuration parameters for Mandelbrot rendering
    // SMALL: 800x600
    public const int WidthPx = 800; // Image width in pixels
    public const int HeightPx = 600; // Image height in pixels

    // HD: 1920x1080
    // public const int WidthPx = 1920;
    // public const int HeightPx = 1080;

    // 2K: 2560x1440
    // public const int WidthPx = 2560;
    // public const int HeightPx = 1440;

    // 4K: 3840x2160
    // public const int WidthPx = 3840;
    // public const int HeightPx = 2160;

    // Maximum iterations for Mandelbrot calculation (detail level)
    public const int MaxIterations = 500; // Lower iterations for faster rendering (but less detail)
    // public const int MaxIterations = 1000; // Default iterations for good detail without excessive compute time
    // public const int MaxIterations = 2000; // Higher iterations for more detail (but slower)
    // public const int MaxIterations = 5000; // Very high iterations for extreme detail (much slower)

    // Viewport parameters - these define the area of the complex plane we are visualizing
    public const double CenterX = -0.5; // Center of the view in the complex plane (real part)
    public const double CenterY = 0.0; // Center of the view in the complex plane (imaginary part)
    public const double Scale = 3.0; // How much of the complex plane to show

    // Performance testing parameters
    public const int WarmupRuns = 2; // Number of warmup runs before timing
    public const int Runs = 5; // Number of timed renders for averaging

    public static bool Parallel = false; // Flag to indicate whether to use parallel computation (can be set from command line)

    public static int Threads = 2; // Number of threads to use for parallel computation (default 2, can be set from command line)

    // Array to store individual run times for performance report
    public static readonly long[] times = new long[Runs];

    // Filename for performance report
    public static string ReportFilename => $"results-{(Parallel ? $"parallel-{Threads}threads" : "sequential")}-{WarmupRuns}warmup-{Runs}runs-{WidthPx}x{HeightPx}.txt";
  }
}