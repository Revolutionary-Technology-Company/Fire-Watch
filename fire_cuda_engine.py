#!/usr/bin/env python3
"""
High-Speed Hardware Accelerated Fire Detection Stream Processor.
Applies continuous fire spectrum color and luminance equations to raw image frames
using multi-core parallel CPU (Numba NJIT) or NVIDIA GPU (CUDA) vectorization.
"""

import sys
import argparse
import numpy as np
from numba import njit, prange, cuda

# -------------------------------------------------------------------------
# METHOD 1: MULTICORE PARALLEL CPU ENGINE (Fastest for servers without GPUs)
# -------------------------------------------------------------------------
@njit(parallel=True, cache=True)
def analyze_frame_parallel_cpu(frame_bytes, width, height, red_threshold=190):
    """
    Applies fire equations across all CPU cores simultaneously using parallel loops.
    """
    fire_pixel_count = 0
    # prange instructs Numba to distribute this outer loop across available CPU threads
    for y in prange(height):
        for x in range(width):
            # 24bpp BGR byte indexing layout configuration
            base_idx = (y * width + x) * 3
            b = frame_bytes[base_idx]
            g = frame_bytes[base_idx + 1]
            r = frame_bytes[base_idx + 2]

            # Equation 1: Standard RGB Fire Spectrum Thresholds Rule (R > G > B)
            if (r > g) and (g > b) and (r > red_threshold):
                # Equation 2: YCbCr Space Mathematical ITU-R BT.601 Conversions
                y_val  = (0.299 * r) + (0.587 * g) + (0.114 * b)
                cb_val = (-0.1687 * r) - (0.3313 * g) + (0.5 * b) + 128
                cr_val = (0.5 * r) - (0.4187 * g) - (0.0813 * b) + 128

                # Fire profile matching rule constraints
                if (y_val >= cb_val) and (cr_val >= cb_val):
                    fire_pixel_count += 1
                    
    return (fire_pixel_count / (width * height)) * 100.0


# -------------------------------------------------------------------------
# METHOD 2: NVIDIA CUDA GPU ACCELERATED KERNEL (Fastest for heavy camera arrays)
# -------------------------------------------------------------------------
@cuda.jit
def _cuda_fire_kernel(frame_bytes, output_matrix, width, height, red_threshold):
    """
    Massively parallel GPU kernel executing one dedicated thread per physical pixel.
    """
    # Dynamically map the executing GPU thread to the exact 2D pixel coordinates
    x, y = cuda.grid(2)

    if x < width and y < height:
        base_idx = (y * width + x) * 3
        b = frame_bytes[base_idx]
        g = frame_bytes[base_idx + 1]
        r = frame_bytes[base_idx + 2]

        # Evaluate Fire Color Spectrum Conditions inside GPU core space
        if (r > g) and (g > b) and (r > red_threshold):
            y_val  = (0.299 * r) + (0.587 * g) + (0.114 * b)
            cb_val = (-0.1687 * r) - (0.3313 * g) + (0.5 * b) + 128
            cr_val = (0.5 * r) - (0.4187 * g) - (0.0813 * b) + 128

            if (y_val >= cb_val) and (cr_val >= cb_val):
                output_matrix[y, x] = 1 # Mark pixel location as matching fire profile
            else:
                output_matrix[y, x] = 0
        else:
            output_matrix[y, x] = 0


def analyze_frame_nvidia_cuda(frame_bytes, width, height, red_threshold=190):
    """
    Manages host-to-device memory allocation routines for hardware acceleration.
    """
    # 1. Allocate device VRAM memory and push the raw frame matrix array to the GPU card
    d_frame = cuda.to_device(frame_bytes)
    d_output = cuda.device_array((height, width), dtype=np.uint8)

    # 2. Configure 2D thread execution grids (32x32 blocks perfectly align with NVIDIA Warps)
    threads_per_block = (32, 32)
    blocks_per_grid_x = int(np.ceil(width / threads_per_block[0]))
    blocks_per_grid_y = int(np.ceil(height / threads_per_block[1]))
    blocks_per_grid = (blocks_per_grid_x, blocks_per_grid_y)

    # 3. Launch the unmanaged hardware execution stream threads
    _cuda_fire_kernel[blocks_per_grid, threads_per_block](d_frame, d_output, width, height, red_threshold)

    # 4. Synchronize threads and copy output byte tracking back to Host system RAM
    h_output = d_output.copy_to_host()
    fire_pixels = np.sum(h_output)
    
    return (fire_pixels / (width * height)) * 100.0


# -------------------------------------------------------------------------
# CORE ENTRY INTERFACE AND PARSING ROUTINES
# -------------------------------------------------------------------------
def main():
    # Initialize the CLI argument parser engine to handle constraints sent by the C# bridge
    parser = argparse.ArgumentParser(description="High-Speed Hardware Accelerated Fire Detection Stream Processor")
    parser.add_argument("--width", type=int, required=True, help="Width configuration dimensions of incoming frame image matrix")
    parser.add_argument("--height", type=int, required=True, help="Height configuration dimensions of incoming frame image matrix")
    parser.add_argument("--use_gpu", type=bool, default=True, help="Force execution on NVIDIA CUDA kernels if hardware is present")
    
    args = parser.parse_args()
    width = args.width
    height = args.height
    
    # Enforce strict 24bpp BGR array allocation checks (3 bytes per pixel)
    expected_bytes_count = width * height * 3

    try:
        # Block and read raw unmanaged binary image frame straight from the C# stdin stream channel
        raw_frame_bytes = sys.stdin.buffer.read(expected_bytes_count)
        
        # Validation Guard: ensure data packet wasn't truncated during pipeline transitions
        if len(raw_frame_bytes) != expected_bytes_count:
            sys.stderr.write(f"Error: Ingested stream byte count ({len(raw_frame_bytes)}) mismatched layout bounds ({expected_bytes_count}).\n")
            print("0.0")
            sys.exit(1)

        # Map the read buffer matrix instantaneously into a highly optimized contiguous NumPy array
        frame_array = np.frombuffer(raw_frame_bytes, dtype=np.uint8)
        calculated_density = 0.0

        # Select matching vector path based on real-time server hardware asset presence flags
        if args.use_gpu and cuda.is_available():
            # Trigger the Massive Parallel NVIDIA CUDA GPU Hardware Vector Kernel
            calculated_density = analyze_frame_nvidia_cuda(frame_array, width, height)
        else:
            # Fall back seamlessly to CPU parallel scaling if an NVIDIA card isn't deployed on the target server rack
            calculated_density = analyze_frame_parallel_cpu(frame_array, width, height)

        # Flush scalar densities to stdout channel for the C# listener to ingest instantly
        print(f"{calculated_density:.4f}")
        sys.stdout.flush()

    except Exception as ex:
        # Defensively insulate execution loop errors to prevent dropping the Windows Service background worker
        sys.stderr.write(f"Critical execution error inside processing kernel logic loops: {str(ex)}\n")
        print("0.0")
        sys.exit(1)


if __name__ == "__main__":
    main()
