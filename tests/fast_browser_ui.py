"""Fast browser workspace smoke check; shares the canonical Sources/Results assertions."""
from pathlib import Path
import runpy

runpy.run_path(str(Path(__file__).with_name("browser_ui.py")), run_name="__main__")
