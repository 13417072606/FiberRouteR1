namespace FiberRoute.Publisher;
internal static class Program
{
    [STAThread]static void Main(){ApplicationConfiguration.Initialize();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);Application.ThreadException+=(_,e)=>MessageBox.Show(e.Exception.Message,"管理方发布工具",MessageBoxButtons.OK,MessageBoxIcon.Error);Application.Run(new PublisherForm());}
}
