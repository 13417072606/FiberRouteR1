using System.Globalization;
using FiberRoute.Core;

namespace FiberRoute.App;

public sealed partial class MainForm
{
    const string LastAutoCheckDay="online-auto-check-day";
    const string PendingProgram="online-pending-program";
    const string PendingData="online-pending-data";
    readonly Label updateBadge=new(){Text="● 更新",AutoSize=true,Visible=false,ForeColor=Color.FromArgb(204,45,45),BackColor=Color.White,Font=new Font("Microsoft YaHei UI",9,FontStyle.Bold),Cursor=Cursors.Hand};
    readonly ToolTip updateBadgeHint=new();
    readonly CancellationTokenSource updateMonitorLifetime=new();
    System.Windows.Forms.Timer? updateIdleTimer;
    ReleaseManifest? discoveredUpdates;
    bool checkingUpdates;

    void StartDailyUpdateCheck()
    {
        RefreshUpdateBadge();
        FormClosed+=(_,_)=>{updateIdleTimer?.Stop();updateIdleTimer?.Dispose();updateMonitorLifetime.Cancel();updateBadgeHint.Dispose();};
        if(!UpdateReminderPolicy.ShouldCheck(DateTime.Now,store.Setting(LastAutoCheckDay)))return;
        if(!busy){BeginInvoke((Action)(()=>_ = CheckForPublishedUpdates()));return;}
        // Wait only for startup work to finish; this timer never polls the network.
        updateIdleTimer=new System.Windows.Forms.Timer{Interval=1000};
        updateIdleTimer.Tick+=(_,_)=>{if(busy)return;updateIdleTimer.Stop();updateIdleTimer.Dispose();updateIdleTimer=null;_ = CheckForPublishedUpdates();};
        updateIdleTimer.Start();
    }

    void RefreshUpdateBadge()
    {
        string? program=store.Setting(PendingProgram),data=store.Setting(PendingData);
        if(program is {Length:>0}&&!Updates.IsNewProgram(program,ProgramVersion)){store.SetSetting(PendingProgram,"");program=null;}
        if(data is {Length:>0}&&data==store.Setting("remoteResourceVersion")){store.SetSetting(PendingData,"");data=null;}
        updateBadge.Visible=UpdateReminderPolicy.HasPending(program,data,ProgramVersion,store.Setting("remoteResourceVersion"));
        updateBadgeHint.SetToolTip(updateBadge,"待更新："+(program is {Length:>0}?"软件 R"+program+" ":"")+(data is {Length:>0}?"数据 "+data:"")+"。点击查看。");
    }

    async Task CheckForPublishedUpdates()
    {
        if(IsDisposed||checkingUpdates||busy)return;
        checkingUpdates=true;
        try
        {
            // Future activation can replace this gate. Today every configured client is eligible.
            if(!CanReceiveOnlineUpdates())return;
            var config=Updates.LoadConfig(UpdateConfiguration);
            if(config==null||!UpdateReminderPolicy.ShouldCheck(DateTime.Now,store.Setting(LastAutoCheckDay)))return;
            // Mark the attempt before requesting the network. Reopening today never auto-checks
            // again, even when the network is unavailable; manual checks remain available.
            store.SetSetting(LastAutoCheckDay,DateTime.Now.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture));
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(updateMonitorLifetime.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            var manifest=await Updates.Check(config,timeout.Token);
            if(IsDisposed||updateMonitorLifetime.IsCancellationRequested)return;
            var software=manifest.Program is { } program&&Updates.IsNewProgram(program.Version,ProgramVersion)?program:null;
            var data=manifest.Resources is { } resources&&resources.Version!=store.Setting("remoteResourceVersion")?resources:null;
            discoveredUpdates=software!=null||data!=null?new ReleaseManifest(software,data):null;
            store.SetSetting(PendingProgram,software?.Version??"");
            store.SetSetting(PendingData,data?.Version??"");
            RefreshUpdateBadge();
        }
        catch(OperationCanceledException)when(updateMonitorLifetime.IsCancellationRequested){}
        catch(Exception){/* A failed daily check stays silent; the user can check manually. */}
        finally{checkingUpdates=false;}
    }

    bool CanReceiveOnlineUpdates()=>true;
}
