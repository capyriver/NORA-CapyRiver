"""Regression checks for direct startup without a separate splash/loading screen."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
host = (ROOT / "src" / "WebViewHost.cs").read_text(encoding="utf-8")
program = (ROOT / "src" / "Program.cs").read_text(encoding="utf-8")
assert not (ROOT / "src" / "StartupView.cs").exists(), "obsolete startup-view source must stay removed"
assert "startupPanel" not in host + program and "InitStartupPanel" not in host + program
assert "WatchStartup" not in host and "Подготавливаю интерфейс" not in host
assert '"https://nora-ui.local/index.html"' in host
assert "IsLocalUiAddress(e.Uri)" in host and "IsLocalUiAddress(e.Source)" in host
assert "HostResourceAccessKind.DenyCors" in host
assert "noraI18nOriginals" in (ROOT / "src" / "ui.html").read_text(encoding="utf-8")
assert 'Path.Combine(root,"webview-ui-3.3")' in host
assert "NavigationCompleted+=" in host and "ProcessFailed+=" in host
assert "case \"uiReady\":CompleteStartup()" in program
assert "File.Delete" not in host and "Directory.Delete" not in host
print("10 startup regression checks passed; no separate loading screen remains")
