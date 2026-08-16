"""
Builds a standalone inference-server executable via PyInstaller.

Usage:
    python build_inference_server.py

Produces <repo_root>/build/server/ai_boss_inference_server(.exe) — the
build/ folder is created automatically if it doesn't exist yet. The build
does NOT bundle the policies/ folder — copy it next to the built executable
manually, so the policy can be swapped without rebuilding:

    build/
      server/
        ai_boss_inference_server.exe
        policies/
          final_policy.pt
"""

import os

import PyInstaller.__main__

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))  # .../Game/server
REPO_ROOT = os.path.dirname(os.path.dirname(SCRIPT_DIR))  # .../AI_Roguelike
BUILD_ROOT = os.path.join(REPO_ROOT, "build")
DIST_PATH = os.path.join(BUILD_ROOT, "server")
WORK_PATH = os.path.join(BUILD_ROOT, "_pyinstaller_work")

os.makedirs(DIST_PATH, exist_ok=True)
os.makedirs(WORK_PATH, exist_ok=True)

PyInstaller.__main__.run([
    "inference_server.py",
    "--name=ai_boss_inference_server",
    "--onefile",
    "--console",
    "--clean",
    "--noconfirm",
    f"--distpath={DIST_PATH}",
    f"--workpath={WORK_PATH}",
])
