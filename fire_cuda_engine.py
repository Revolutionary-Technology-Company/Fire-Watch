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
    # prange instructs Numba to distribute this loop across available CPU threads
    for y in prange(height):
        for x in range(width):
            # 24bpp BGR byte indexing
            base_idx = (y * width + x) * 3
            b = frame_bytes[base_idx]
            g = frame_bytes[base_idx + 1]
            r = frame_bytes[base_idx + 2]

            # Equation 1: RGB Fire Spectrum Rules
            if (r > g) and (g > b) and (r > red_threshold):
                # Equation 2: YCbCr Space Mathematical Conversions
                y_val  = (0.299 * r) + (0.587 * g) + (0.114 * b)
                cb_val = (-0.1687 * r) - (0.3313 * g) + (0.5 * b) + 128
                cr_val = (0.5 * r) - (0.4187 * g) - (0.0813 * b) + 128

                if (y_val >= cb_val) and (cr_val >= cb_val):
                    fire_pixel_count += 1
                    
    return (fire_pixel_count / (width * height)) * 100.0


# -------------------------------------------------------------------------
# METHOD 2: NVIDIA CUDA GPU ACCELERATED KERNEL (Fastest for heavy camera arrays)
# -------------------------------------------------------------------------
@cuda.jit
def _cuda_fire_kernel(frame_bytes, output_matrix, width, height, red_threshold):
    """
    Massively parallel GPU kernel executing one thread per pixel.
    """
    # Map thread to pixel coordinates
    x, y = cuda.grid(2)

    if x < width and y < height:
        base_idx = (y * width + x) * 3
        b = frame_bytes[base_idx]
        g = frame_bytes[base_idx + 1]
        r = frame_bytes[base_idx + 2]

        if (r > g) and (g > b) and (r > red_threshold):
            y_val  = (0.299 * r) + (0.587 * g) + (0.114 * b)
            cb_val = (-0.1687 * r) - (0.3313 * g) + (0.5 * b) + 128
            cr_val = (0.5 * r) - (0.4187 * g) - (0.0813 * b) + 128

            if (y_val >= cb_val) and (cr_val >= cb_val):
                output_matrix[y, x] = 1 # Mark pixel as matching fire spectrum
            else:
                output_matrix[y, x] = 0
        else:
            output_matrix[y, x] = 0

def analyze_frame_nvidia_cuda(frame_bytes, width, height, red_threshold=190):
    """
    Manages host-to-device memory copy routines for NVIDIA CUDA computation.
    """
    # 1. Allocate device memory and send raw frame matrix to GPU VRAM
    d_frame = cuda.to_device(frame_bytes)
    d_output = cuda.device_array((height, width), dtype=np.uint8)

    # 2. Configure 2D CUDA processing blocks and grids (32x32 structure optimizes warps)
    threads_per_block = (32, 32)
    blocks_per_grid_x = int(np.ceil(width / threads_per_block[0]))
    blocks_per_grid_y = int(np.ceil(height / threads_per_block[1]))
    blocks_per_grid = (blocks_per_grid_x, blocks_per_grid_y)

    # 3. Fire the hardware kernel stream execution
    _cuda_fire_kernel[blocks_per_grid, threads_per_block](d_frame, d_output, width, height, red_threshold)

    # 4. Copy data back to host RAM and calculate density total
    h_output = d_output.copy_to_host()
    fire_pixels = np.sum(h_output)
    
    return (fire_pixels / (width * height)) * 100.0
