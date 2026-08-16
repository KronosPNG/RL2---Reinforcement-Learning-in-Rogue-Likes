"""
Standalone entry point for a packaged, inference-only server build.

Hardcodes --mode=inference against policies/final_policy.pt (resolved next to
this script/executable, not the current working directory) and forces CPU
execution

This is the target for the PyInstaller build (see build_inference_server.py)
that the game launches as a companion process. For interactive/dev use,
`python main.py --mode=... ` remains the normal path — this file exists so a
packaged build needs zero command-line arguments.
"""

import os
import sys

# Must run before torch is imported anywhere. main.py (and utils/device.py)
# import torch at module load time, so this has to be the very first thing
# that executes, ahead of the `from main import main` below.
os.environ["CUDA_VISIBLE_DEVICES"] = ""

# Resolve paths relative to the executable/script location, not the process's
# working directory, so this works regardless of where the game launches it
# from. sys.frozen/sys.executable is how a PyInstaller-built exe reports its
# own location; falls back to this script's own directory for `python
# inference_server.py` dev runs.
if getattr(sys, "frozen", False):
    BASE_DIR = os.path.dirname(sys.executable)
else:
    BASE_DIR = os.path.dirname(os.path.abspath(__file__))

POLICY_PATH = os.path.join(BASE_DIR, "policies", "final_policy.pt")

if not os.path.isfile(POLICY_PATH):
    print(f"[ERROR] No policy found at {POLICY_PATH}")
    print("Expected a 'policies' folder containing final_policy.pt next to this executable.")
    sys.exit(1)

sys.argv = [sys.argv[0], "--mode=inference", f"--policy-path={POLICY_PATH}"]

import asyncio
from main import main

if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        pass  # Clean exit, shutdown already handled inside main()
