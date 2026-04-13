using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;
namespace MandelbrotDemo
{
  internal static class MandelbrotUtils
  {
    // Computes the Mandelbrot set sequentially (single-threaded) for the defined viewport.
    // Each pixel (px, py) maps to a point c = (x0 + y0*i) in the complex plane,
    // then iterates z_(n+1) = z_n^2 + c starting from z_0 = 0.
    // Returns a flat int[height * width] array where each value is the number of iterations
    // before |z| > 2 (escaped to infinity), or maxIterations if the point never escaped (inside the set).
    public static int[] ComputeMandelbrot(
      int width,
      int height,
      int maxIterations,
      double centerX,
      double centerY,
      double scale
    )
    {
      int[] data = new int[height * width];

      // How many units in the complex plane one pixel represents
      double step = scale / width;
      // Top-left corner of the viewport in the complex plane
      double startX = centerX - (width / 2.0) * step;
      double startY = centerY - (height / 2.0) * step;

      for (int py = 0; py < height; py++)
      {
        double y0 = startY + py * step; // Imaginary component of c for this row
        int rowOffset = py * width;     // Flat-array offset for this row

        for (int px = 0; px < width; px++)
        {
          // Real component of c for this pixel — full complex point: c = x0 + y0*i
          double x0 = startX + px * step;

          // z starts at 0+0i; x = Re(z), y = Im(z)
          double x = 0.0;
          double y = 0.0;
          double xx = 0.0; // cached x^2 — reused in the escape check and next iteration
          double yy = 0.0; // cached y^2
          int iteration = 0;

          // Iterate z = z^2 + c until escape or maxIterations is reached.
          // Escape condition: |z|^2 = x^2 + y^2 > 4  →  |z| > 2 (point diverges to infinity).
          // Expanding z^2 + c with z = x + y*i and c = x0 + y0*i:
          //   Re(z^2 + c) = x^2 - y^2 + x0
          //   Im(z^2 + c) = 2*x*y   + y0
          // y must be updated before x because the old x is needed to compute the new y.
          while (xx + yy <= 4.0 && iteration < maxIterations)
          {
            y = 2.0 * x * y + y0; // Im(z^2 + c) — computed first, uses old x
            x = xx - yy + x0;     // Re(z^2 + c)

            xx = x * x;
            yy = y * y;

            iteration++;
          }

          // If iteration == maxIterations, the point likely belongs to the Mandelbrot set.
          // Otherwise, the value encodes how quickly it diverged — used for coloring.
          data[rowOffset + px] = iteration;
        }
      }
      return data;
    }

    // Parallel version of ComputeMandelbrot — identical math, rows distributed across threads.
    // Each row is an independent unit of work: it reads only its own y0 and writes
    // exclusively to its own region [rowOffset .. rowOffset+width) in the shared data array.
    // No synchronization is needed because row regions never overlap.
    // MaxDegreeOfParallelism limits concurrent threads to MandelbrotConfig.Threads.
    public static int[] ComputeMandelbrotParallel(
      int width,
      int height,
      int maxIterations,
      double centerX,
      double centerY,
      double scale)
    {
      int[] data = new int[width * height];

      double step = scale / width;
      double startX = centerX - (width / 2.0) * step;
      double startY = centerY - (height / 2.0) * step;

      var options = new ParallelOptions
      {
        MaxDegreeOfParallelism = MandelbrotConfig.Threads
      };

      // Each Parallel.For iteration processes one row on a thread-pool thread.
      // All variables inside the lambda are thread-local — no data races.
      Parallel.For(0, height, options, py =>
      {
        double y0 = startY + py * step; // Imaginary component of c for this row
        int rowOffset = py * width;     // Unique flat-array region — no overlap with other rows

        for (int px = 0; px < width; px++)
        {
          // Real component of c for this pixel — c = x0 + y0*i
          double x0 = startX + px * step;

          double x = 0.0;
          double y = 0.0;
          double xx = 0.0; // cached x^2
          double yy = 0.0; // cached y^2
          int iteration = 0;

          // Same iteration as the sequential version — see ComputeMandelbrot for the full explanation.
          while (xx + yy <= 4.0 && iteration < maxIterations)
          {
            y = 2.0 * x * y + y0; // Im(z^2 + c) — uses old x
            x = xx - yy + x0;     // Re(z^2 + c)

            xx = x * x;
            yy = y * y;

            iteration++;
          }

          data[rowOffset + px] = iteration;
        }
      });

      return data;
    }

    // Converts the iteration-count array from ComputeMandelbrot into a colored Bitmap (sequential, simple path).
    // Points that reached maxIterations (inside the set) are colored black.
    // All other points are colored by ColorFromIteration based on their escape speed.
    // Uses SetPixel — straightforward but slow due to per-pixel lock/unlock overhead.
    public static Bitmap CreateBitmapFromIterations(int[] data, int width, int height, int maxIterations)
    {
      Bitmap bmp = new Bitmap(width, height);

      for (int py = 0; py < height; py++)
      {
        for (int px = 0; px < width; px++)
        {
          int iteration = data[py * width + px];
          if (iteration == maxIterations)
          {
            // Point did not escape — inside the Mandelbrot set
            bmp.SetPixel(px, py, Color.Black);
          }
          else
          {
            ColorFromIteration(iteration, maxIterations, out byte r, out byte g, out byte b);
            bmp.SetPixel(px, py, Color.FromArgb(r, g, b));
          }
        }
      }
      return bmp;
    }

    // Converts the iteration-count array into a colored Bitmap using direct memory access (fast path).
    // Instead of calling SetPixel for every pixel (which locks/unlocks the bitmap on each call), we:
    //   1. Lock the entire bitmap once with LockBits to get a raw pointer to its pixel memory.
    //   2. Write all pixels into a managed byte[] staging buffer.
    //   3. Bulk-copy the buffer to unmanaged bitmap memory with a single Marshal.Copy call.
    //   4. Unlock the bitmap.
    // Pixel format is BGRA (32 bpp): each pixel occupies 4 bytes stored as [B, G, R, A].
    // Stride = bytes per row, which may be wider than width*4 due to memory alignment padding.
    // Rendering is parallel or sequential depending on MandelbrotConfig.Parallel.
    public static Bitmap CreateBitmapFromIterationsParallel(int[] data, int width, int height, int maxIterations)
    {
      Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
      BitmapData bmpData = bmp.LockBits(
        new Rectangle(0, 0, width, height),
        ImageLockMode.WriteOnly,
        bmp.PixelFormat
      );

      int stride = bmpData.Stride;   // Bytes per row (includes any alignment padding)
      IntPtr ptr = bmpData.Scan0;    // Pointer to the first byte of pixel data in unmanaged memory
      int byteCount = stride * height;
      byte[] pixels = new byte[byteCount]; // Managed staging buffer — filled here, then bulk-copied

      var options = new ParallelOptions
      {
        MaxDegreeOfParallelism = MandelbrotConfig.Threads
      };

      // Renders one row of pixels into the staging buffer.
      // Each row writes to a distinct byte range [rowStart .. rowStart+stride), so there are no data races
      // when multiple threads call this concurrently with different py values.
      void renderRow(int py)
      {
        int rowStart = py * stride;    // Byte offset of this row in the pixel buffer
        int dataRowStart = py * width; // Index of the first iteration value for this row
        for (int px = 0; px < width; px++)
        {
          int iteration = data[dataRowStart + px];
          int offset = rowStart + px * 4; // 4 bytes per pixel (BGRA format)

          if (iteration == maxIterations)
          {
            // Point inside the set — black, fully opaque
            pixels[offset] = 0;   // Blue
            pixels[offset + 1] = 0;   // Green
            pixels[offset + 2] = 0;   // Red
            pixels[offset + 3] = 255; // Alpha
          }
          else
          {
            ColorFromIteration(iteration, maxIterations, out byte r, out byte g, out byte b);
            pixels[offset] = b;   // Blue  (BGRA: blue is stored first in memory)
            pixels[offset + 1] = g;   // Green
            pixels[offset + 2] = r;   // Red
            pixels[offset + 3] = 255; // Alpha — fully opaque
          }
        }
      }

      if (MandelbrotConfig.Parallel)
      {
        Parallel.For(0, height, options, renderRow);
      }
      else
      {
        for (int py = 0; py < height; py++)
        {
          renderRow(py);
        }
      }
      // Bulk-copy all rendered pixels from the managed staging buffer to unmanaged bitmap memory
      System.Runtime.InteropServices.Marshal.Copy(pixels, 0, ptr, byteCount);
      bmp.UnlockBits(bmpData);
      return bmp;
    }

    // Maps an escape-speed iteration count to an RGB color using a smooth gradient.
    // t = iteration / maxIterations is a normalized escape speed in [0, 1]:
    //   t ≈ 0  →  point escaped immediately (far outside the set)
    //   t ≈ 1  →  point escaped very slowly (near the boundary)
    // The three channels use polynomial basis functions to produce a smooth color
    // gradient instead of a uniform or banded color map.
    // Points that never escaped (iteration == maxIterations) are colored black by the caller.
    public static void ColorFromIteration(int iteration, int maxIterations, out byte r, out byte g, out byte b)
    {
      // Normalized escape speed: 0 = instant escape, 1 = very slow escape
      double t = (double)iteration / maxIterations;
      
      // Stretch the gradient: the square root pulls colors out of the dark much faster,
      // ensuring a beautiful rich dark blue color for points with a fast escape.
      t = Math.Sqrt(t);

      // Classic Wikipedia coloring polynomial:
      // Small t (fast escape) gives high blue. 
      // Medium t gives light blue / white.
      // High t (slow escape near boundary) gives yellow/black.
      r = (byte)(9 * (1 - t) * t * t * t * 255);
      g = (byte)(15 * (1 - t) * (1 - t) * t * t * 255);
      b = (byte)(8.5 * (1 - t) * (1 - t) * (1 - t) * t * 255);

      // --- Alternative color maps ---
      
      // Red background (Fire colors):
      // r = (byte)(8.5 * (1 - t) * (1 - t) * (1 - t) * t * 255);
      // g = (byte)(15 * (1 - t) * (1 - t) * t * t * 255);
      // b = (byte)(9 * (1 - t) * t * t * t * 255);

      // Green background (Toxic/Matrix colors):
      // r = (byte)(9 * (1 - t) * t * t * t * 255);
      // g = (byte)(8.5 * (1 - t) * (1 - t) * (1 - t) * t * 255);
      // b = (byte)(15 * (1 - t) * (1 - t) * t * t * 255);
    }

    // Runs the Mandelbrot computation benchmark according to MandelbrotConfig settings.
    // Performs WarmupRuns untimed iterations first to let the JIT fully optimize the hot path,
    // then performs Runs timed iterations and stores elapsed times in MandelbrotConfig.times.
    // Dispatches to sequential or parallel compute depending on MandelbrotConfig.Parallel.
    public static void RunBenchmark()
    {
      Stopwatch sw = new();

      // Warmup: the JIT compiles and optimizes the method on first execution.
      // Without warmup, the first measured run would include compilation overhead and be unfairly slow.
      for (int i = 0; i < MandelbrotConfig.WarmupRuns; i++)
      {
        if (MandelbrotConfig.Parallel)
          ComputeMandelbrotParallel(
              MandelbrotConfig.WidthPx,
              MandelbrotConfig.HeightPx,
              MandelbrotConfig.MaxIterations,
              MandelbrotConfig.CenterX,
              MandelbrotConfig.CenterY,
              MandelbrotConfig.Scale);
        else
          ComputeMandelbrot(
              MandelbrotConfig.WidthPx,
              MandelbrotConfig.HeightPx,
              MandelbrotConfig.MaxIterations,
              MandelbrotConfig.CenterX,
              MandelbrotConfig.CenterY,
              MandelbrotConfig.Scale);
      }

      // Timed runs for performance measurement
      for (int i = 0; i < MandelbrotConfig.Runs; i++)
      {
        sw.Restart();

        if (MandelbrotConfig.Parallel)
        {
          ComputeMandelbrotParallel(
              MandelbrotConfig.WidthPx,
              MandelbrotConfig.HeightPx,
              MandelbrotConfig.MaxIterations,
              MandelbrotConfig.CenterX,
              MandelbrotConfig.CenterY,
              MandelbrotConfig.Scale);
        }
        else
        {
          ComputeMandelbrot(
              MandelbrotConfig.WidthPx,
              MandelbrotConfig.HeightPx,
              MandelbrotConfig.MaxIterations,
              MandelbrotConfig.CenterX,
              MandelbrotConfig.CenterY,
              MandelbrotConfig.Scale);
        }
        sw.Stop();

        // Store the elapsed time for this run
        MandelbrotConfig.times[i] = sw.ElapsedMilliseconds;
      }
    }

    // Returns the median of all recorded run times in MandelbrotConfig.times.
    // Median is preferred over the arithmetic mean for benchmarks because it is resistant
    // to outliers caused by OS scheduling, garbage collection pauses, or other interference.
    // For an even number of samples, returns the average of the two middle values.
    public static double Median()
    {
      long[] sorted = [.. MandelbrotConfig.times];
      Array.Sort(sorted);

      int mid = sorted.Length / 2;

      return sorted.Length % 2 == 0
        ? (sorted[mid - 1] + sorted[mid]) / 2.0
        : sorted[mid];
    }

    // Writes the benchmark results to a text file in results/sequential/ or results/parallel/.
    // Each run is prepended — new results appear at the top, and all previous results
    // are kept below, so the file acts as a cumulative history of all benchmark runs.
    public static void WriteReport(bool cmdOnly = false)
    {
      if (cmdOnly)
      {
        // Write the report to the console instead of a file
        Console.WriteLine($"=== {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
        Console.WriteLine($"Mandelbrot {MandelbrotConfig.WidthPx}x{MandelbrotConfig.HeightPx}, maxIter={MandelbrotConfig.MaxIterations}");
        Console.WriteLine($"{(MandelbrotConfig.Parallel ? $"Parallel, Threads: {MandelbrotConfig.Threads}" : "Sequential")}");
        Console.WriteLine(new string('-', 40));
        Console.WriteLine($"Warmups: {MandelbrotConfig.WarmupRuns}");
        Console.WriteLine($"Runs: {MandelbrotConfig.Runs}");
        Console.WriteLine($"Median Time: {Median():F2} ms");
        Console.WriteLine(new string('-', 40));
        for (int i = 0; i < MandelbrotConfig.times.Length; i++)
          Console.WriteLine($"Run {i + 1}: {MandelbrotConfig.times[i]} ms");
        Console.Out.Flush();
        return;
      }

      // Write the report to a file in the results directory, organized by execution mode and resolution.
      string subFolder = MandelbrotConfig.Parallel ? "parallel" : "sequential"; // Subfolder based on execution mode
      string subFolder2 = $"{MandelbrotConfig.WidthPx}x{MandelbrotConfig.HeightPx}"; // Subfolder for the current resolution (e.g., "1920x1080")
      string dir = Path.Combine(Directory.GetCurrentDirectory(), "results", subFolder); // Base directory for results (e.g., "results/parallel")
      string parallelDir = Path.Combine(dir, subFolder2); // Subdirectory for the current resolution (e.g., "results/parallel/1920x1080")

      Directory.CreateDirectory(dir); // Ensure the output directory exists
      if (MandelbrotConfig.Parallel)
        Directory.CreateDirectory(parallelDir); // Ensure the subdirectory for the current resolution exists

      // Full path to the report file according to the execution mode (parallel/sequential) and resolution ()
      string fullPath = MandelbrotConfig.Parallel
        ? Path.Combine(parallelDir, MandelbrotConfig.ReportFilename)
        : Path.Combine(dir, MandelbrotConfig.ReportFilename);

      // Read previous content so the new run can be prepended above it
      string existing = File.Exists(fullPath)
        ? File.ReadAllText(fullPath)
        : string.Empty;

      using StreamWriter writer = new(fullPath, append: false);
      writer.WriteLine($"=== {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
      writer.WriteLine($"Mandelbrot {MandelbrotConfig.WidthPx}x{MandelbrotConfig.HeightPx}, maxIter={MandelbrotConfig.MaxIterations}");
      writer.WriteLine($"{(MandelbrotConfig.Parallel ? $"Parallel, Threads: {MandelbrotConfig.Threads}" : "Sequential")}");
      writer.WriteLine(new string('-', 40));
      writer.WriteLine($"Warmups: {MandelbrotConfig.WarmupRuns}");
      writer.WriteLine($"Runs: {MandelbrotConfig.Runs}");
      writer.WriteLine($"Median Time: {Median():F2} ms");
      writer.WriteLine();
      writer.WriteLine(new string('-', 40));
      for (int i = 0; i < MandelbrotConfig.times.Length; i++)
      {
        writer.WriteLine($"Run {i + 1}: {MandelbrotConfig.times[i]} ms");
      }
      writer.WriteLine();
      writer.WriteLine(new string('-', 40));
      writer.WriteLine();
      writer.Write(existing);
    }
  }
}