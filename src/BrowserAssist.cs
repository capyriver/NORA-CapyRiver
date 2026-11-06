namespace LocalChat;
static class BrowserAssist {
 public static bool CanObserve(bool hasBrowser,Settings s)=>hasBrowser&&s.ToolMode&&s.UnifiedAgent;
 public static void Enable(Settings s){s.ToolMode=true;s.UnifiedAgent=true;s.ExperimentalProfileId="";}
}
