namespace LocalChat;
// Only timestamps are stored. Keystrokes, form values and passwords are never recorded.
sealed class BrowserControlLease {
 public bool Manual {get;private set;}
 public DateTime LastActivity {get;private set;}
 public void Activity(DateTime now){Manual=true;LastActivity=now;}
 public void Resume(){Manual=false;}
 public bool Tick(DateTime now){if(!Manual||now-LastActivity<TimeSpan.FromSeconds(10))return false;Manual=false;return true;}
 public int Remaining(DateTime now)=>Manual?Math.Max(0,(int)Math.Ceiling(10-(now-LastActivity).TotalSeconds)):0;
}
