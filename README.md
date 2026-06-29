# Fire-Watch: Genetec Edwards FireWatch Bridge

**Fire-Watch** is a high-speed, hardware-accelerated middleware service designed as a supplemental life-safety system for clinical and hospital environments. It acts as a real-time bridge that monitors live video streams from Genetec Security Center, mathematically processes frames for visual fire signatures, and dynamically forwards alerts to Edwards FireWorks incident management workstations and Univac Aegis endpoints.

## Core Features

* **Hardware-Accelerated Vision Pipeline:** Utilizes a highly optimized Python-based detection engine (`fire_cuda_engine.py`) that applies continuous fire spectrum color and luminance equations. It automatically leverages NVIDIA GPU (CUDA) vectorization for heavy camera arrays, or falls back to multi-core parallel CPU processing (Numba NJIT) if a GPU is unavailable.
* **Genetec Security Center Integration:** Natively subscribes to system-wide live frame video streaming directly from the Genetec Archiver.
* **Edwards FireWorks Interoperability:** Transmits visual fire alarms formatted as strict `[STX]` CSV payloads to the Edwards FireWorks generic TCP ASCII driver. Also features a built-in 60-second supervision heartbeat to provide fail-open troubleshooting capabilities.
* **Univac Aegis Endpoint Integration:** Maps proprietary Edwards states into a standardized 1-10 Aegis severity scale and routes event payloads securely to Aegis endpoints via REST.
* **Hot-Swappable Configuration:** The `appsettings.json` file controls camera-to-zone routing. The service features a built-in directory watcher that instantly parses and applies structural routing changes mid-shift without requiring a service restart.
* **Interactive Documentation Engine:** Includes a Typer and Rich-powered CLI engine (`doc_engine.py`) for querying enterprise installation manuals directly within the terminal shell.

## Prerequisites

* **Operating System:** Windows Server 2019 / 2022 Datacenter or Windows 10/11 Professional (64-bit).
* **Hardware (Preferred):** Dedicated NVIDIA Enterprise GPU (Quadro, Tesla, RTX) with Compute Capability 6.0+. 
* **Hardware (Fallback):** Multi-core Intel Xeon / AMD EPYC (Minimum 4 physical cores allocated).
* **Runtimes:** Python 3.10+ (added to System PATH) and Microsoft .NET Runtime (64-bit Core 6/8).
* **Python Packages:** `numpy`, `numba`, and `llvmlite`.

## Installation & Deployment

1.  **Compile the Service:** Build the C# solution using `.NET` in Release mode and place the compiled `GenetecEdwardsBridge.exe` along
