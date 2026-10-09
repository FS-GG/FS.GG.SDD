#!/usr/bin/env python3
"""Run the existing no-network feed/identity refusal controls against the actual adapter."""
import runpy
from pathlib import Path
runpy.run_path(str(Path(__file__).with_name('release-four-package-controls.py')), run_name='__main__')
