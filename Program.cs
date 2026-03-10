using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace MandelbrotDemo
{
    internal static class Program
    { 
        // Enum to differentiate between intro and outro messages in the console output
        private enum PrintMode { Intro, Outro };
        private static bool _cmdOnly; // Flag to indicate whether to only print benchmark results to the console without showing the image
        // [STAThread] sets the COM threading model to Single-Threaded Apartment.
        // Windows Forms requires this because UI controls must be created and accessed on a single thread.
        [STAThread]
        
       
        public static void Main(string[] args)
        {
            _cmdOnly = Array.Exists(args, a => a == "--cmd"); // If --cmd is passed, only print benchmark results to the console without showing the image.
            
            // Run a sequential (single-threaded) benchmark and write results to a report file
            if (args.Length > 0 && args[0] == "--benchmark")
            {
                WriteLine();
                MandelbrotUtils.RunBenchmark();
                MandelbrotUtils.WriteReport(_cmdOnly);
                WriteLine(PrintMode.Outro);
                return;
            }

            // Run a parallel benchmark with the given thread count (default 2 if omitted)
            if (args.Length > 0 && args[0] == "--benchmark-par")
            {
                MandelbrotConfig.Parallel = true;
                MandelbrotConfig.Threads = args.Length > 1 ? int.Parse(args[1]) : 2;
                WriteLine();
                MandelbrotUtils.RunBenchmark();
                MandelbrotUtils.WriteReport(_cmdOnly);
                WriteLine(PrintMode.Outro);
                return;
            }

            // Compute and display the Mandelbrot image sequentially (no benchmark output)
            if (args.Length > 0 && args[0] == "--image")
            {
                WriteLine();
                MandelbrotImage();
                return;
            }

            // Compute and display the Mandelbrot image in parallel with the given thread count
            if (args.Length > 0 && args[0] == "--image-par")
            {
                MandelbrotConfig.Parallel = true;
                MandelbrotConfig.Threads = args.Length > 1 ? int.Parse(args[1]) : 2;
                WriteLine();
                MandelbrotImage();
                return;
            }

            // Default (no arguments): run sequential benchmark, write report, then show the imag
            MandelbrotUtils.RunBenchmark();
            MandelbrotUtils.WriteReport(_cmdOnly);
            WriteLine();
            MandelbrotImage();
            WriteLine(PrintMode.Outro);
        }

        // Helper method to print a separator line and either the intro message (resolution and iterations) or outro message (report filename) to the console.
        private static void WriteLine(PrintMode mode = PrintMode.Intro)
        {
            Console.WriteLine(new string('-', 40));
            if (!_cmdOnly)
            {
                Console.WriteLine(mode == PrintMode.Intro 
                    ? $"resolution: {MandelbrotConfig.WidthPx}x{MandelbrotConfig.HeightPx} | iterations: {MandelbrotConfig.MaxIterations}"
                    : $"report file: {MandelbrotConfig.ReportFilename}");
            }
        }

        // Computes the Mandelbrot set once and opens the result in a window.
        // Only the compute step (iterating over pixels) is timed — bitmap creation is excluded
        // because it is a rendering concern, not the parallelism being measured.
        private static void MandelbrotImage()
        {
            var sw = Stopwatch.StartNew();
            int[] data;

            // Compute iteration counts for every pixel — sequential or parallel based on config
            if (MandelbrotConfig.Parallel)
            {
                data = MandelbrotUtils.ComputeMandelbrotParallel(
                    MandelbrotConfig.WidthPx,
                    MandelbrotConfig.HeightPx,
                    MandelbrotConfig.MaxIterations,
                    MandelbrotConfig.CenterX,
                    MandelbrotConfig.CenterY,
                    MandelbrotConfig.Scale
                );
            }
            else
            {
                data = MandelbrotUtils.ComputeMandelbrot(
                    MandelbrotConfig.WidthPx,
                    MandelbrotConfig.HeightPx,
                    MandelbrotConfig.MaxIterations,
                    MandelbrotConfig.CenterX,
                    MandelbrotConfig.CenterY,
                    MandelbrotConfig.Scale
                );
            }
            sw.Stop(); // Only the compute step is timed

            // Convert the raw iteration data into a colored Bitmap
            Bitmap bitmap;

            if (MandelbrotConfig.Parallel)
            {
                // Fast path: LockBits + parallel row rendering
                bitmap = MandelbrotUtils.CreateBitmapFromIterationsParallel(
                    data,
                    MandelbrotConfig.WidthPx,
                    MandelbrotConfig.HeightPx,
                    MandelbrotConfig.MaxIterations
                );
            }
            else 
            {
                // Simple path: sequential SetPixel
                bitmap = MandelbrotUtils.CreateBitmapFromIterations(
                    data,
                    MandelbrotConfig.WidthPx,
                    MandelbrotConfig.HeightPx,
                    MandelbrotConfig.MaxIterations
                );
            }

            // Open the bitmap in a Windows Forms window; compute time appears in the title bar
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MandelbrotForm(bitmap, sw.Elapsed.TotalMilliseconds));
        }
    }
}