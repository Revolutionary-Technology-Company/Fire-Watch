using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace GenetecEdwardsBridge
{
    public class FireDetectionEngine
    {
        // Adjustable parameters for site tuning
        private const double TriggerPercentage = 1.5; // Trigger alarm if more than 1.5% of the frame contains fire
        
        // Path tracking to the NJIT / Multicore / CUDA Python processor script
        private readonly string _pythonScriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fire_cuda_engine.py");

        /// <summary>
        /// Extracts raw pixel matrices and streams them into the CUDA-accelerated Numba Python pipeline.
        /// Returns true if a fire trigger threshold condition is mathematically satisfied.
        /// </summary>
        public unsafe bool AnalyzeFrameForFire(Bitmap frame, out double flameDensity)
        {
            flameDensity = 0.0;
            if (frame == null) return false;

            int width = frame.Width;
            int height = frame.Height;

            // 1. Lock bitmap bits into system RAM for swift array translation
            BitmapData bitmapData = frame.LockBits(
                new Rectangle(0, 0, width, height), 
                ImageLockMode.ReadOnly, 
                PixelFormat.Format24bppRgb
            );

            // Calculate exact size and extract raw unmanaged memory bytes into a managed array container
            int totalByteCount = bitmapData.Stride * height;
            byte[] rawImageBytes = new byte[totalByteCount];
            Marshal.Copy(bitmapData.Scan0, rawImageBytes, 0, totalByteCount);
            
            // Crucial: Unlock bits immediately to maintain strict life-safety memory efficiency and prevent OS leaks
            frame.UnlockBits(bitmapData);

            try
            {
                // 2. Configure an ultra-low overhead Python process wrapper
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "python.exe",
                    // Pass dimensions as command line flags so the Python side knows exactly how to slice the incoming byte stream
                    Arguments = $"`"{_pythonScriptPath}`" --width {width} --height {height}",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(startInfo))
                {
                    if (process != null)
                    {
                        // 3. Directly stream the binary byte matrix over the pipeline stdin channel to bypass overhead constraints
                        using (BinaryWriter streamWriter = new BinaryWriter(process.StandardInput.BaseStream))
                        {
                            streamWriter.Write(rawImageBytes);
                            streamWriter.Flush();
                        }

                        // 4. Capture the calculated density scalar passed back by Numba/CUDA via stdout
                        string outputResult = process.StandardOutput.ReadLine();
                        
                        if (!string.IsNullOrWhiteSpace(outputResult) && double.TryParse(outputResult, out double parsedDensity))
                        {
                            flameDensity = parsedDensity;
                        }
                        else
                        {
                            // Capture standard execution error logs if Python outputs execution structural issues
                            string operationalErrors = process.StandardError.ReadToEnd();
                            if (!string.IsNullOrWhiteSpace(operationalErrors))
                            {
                                Trace.WriteLine($"CUDA Python processing loop stderr warning: {operationalErrors}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Critical hardware acceleration handoff pipeline failure: {ex.Message}");
                return false;
            }

            // 5. Evaluate if density percentage crosses the plant engineering trigger threshold boundaries
            return flameDensity >= TriggerPercentage;
        }
    }
}

using System.Collections.Generic;

namespace GenetecEdwardsBridge
{
    public class FireWatchConfig
    {
        public GenetecSettings GenetecConfig { get; set; }
        public EdwardsSettings EdwardsConfig { get; set; }
        public List<CameraMapping> HospitalMap { get; set; }
    }

    public class GenetecSettings
    {
        public string DirectoryServer { get; set; }
        public string ServiceUser { get; set; }
        public string ServicePassword { get; set; }
        public string KiwiFireEventGuid { get; set; }
    }

    public class EdwardsSettings
    {
        public string ReceiverIp { get; set; }
        public int ReceiverPort { get; set; }
        public int HeartbeatIntervalSeconds { get; set; }
    }

    public class CameraMapping
    {
        public string CameraGuid { get; set; }
        public string GenetecName { get; set; }
        public string EdwardsNode { get; set; }
        public string EdwardsZone { get; set; }
        public string PhysicalRoom { get; set; }
    }
}
