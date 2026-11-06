namespace LocalChat;
static class TokenProfile {
 // One-time, no context/RAM increase: replace only the old 512-output default.
 public static bool Apply(Settings s){if(s.TokenRevision>=1)return false;if(s.Mode!=0&&s.ContextTokens>=4096&&s.MaxTokens==512)s.MaxTokens=1024;s.TokenRevision=1;return true;}
}
